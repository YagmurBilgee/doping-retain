using System.Text.Json;
using System.Text.Json.Serialization;
using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.Students;
using DopingRetain.Api.TestResults;
using DopingRetain.Api.Topics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Relative paths resolve against the content root (the project folder under `dotnet run`).
var databasePath = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Database:Path"]!);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
var clock = DemoClock.Create(builder.Configuration[DemoClock.FixedUtcNowKey], builder.Environment.IsDevelopment());
builder.Services.AddSingleton(clock);
var retentionOptions = (builder.Configuration.GetSection(RetentionModelOptions.SectionName).Get<RetentionModelOptions>()
    ?? new RetentionModelOptions()).Validated();
builder.Services.AddSingleton(retentionOptions);
var scheduleOptions = (builder.Configuration.GetSection(ReviewScheduleOptions.SectionName).Get<ReviewScheduleOptions>()
    ?? new ReviewScheduleOptions()).Validated(retentionOptions);
builder.Services.AddSingleton(scheduleOptions);
builder.Services.AddScoped<ReviewSessionService>();
builder.Services.AddScoped<TestResultService>();
builder.Services.AddScoped<DemoEditorService>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    SeedData.Initialize(db, clock.GetUtcNow(), retentionOptions);
    if (clock != TimeProvider.System) DemoClock.EnsureNotBeforeRecordedTests(db, clock.GetUtcNow());
}

app.Logger.LogInformation(
    "Database: {DatabasePath}; clock: {Clock}; half-life {HalfLifeDays} d, review threshold {ReviewThreshold}, extended interval {ExtendedIntervalDays} d; demo endpoints {DemoEndpoints}",
    databasePath, clock == TimeProvider.System ? "system UTC" : $"fixed {clock.GetUtcNow():O}",
    retentionOptions.HalfLifeDays, retentionOptions.ReviewThreshold, scheduleOptions.ExtendedIntervalDays,
    DemoEndpoints.IsEnabled(app.Configuration) ? "enabled" : "disabled");

// Errors use the same { code, message } body as the endpoints, including unreadable request bodies.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var (statusCode, error) = context.Features.Get<IExceptionHandlerFeature>()?.Error is BadHttpRequestException badRequest
        ? (badRequest.StatusCode, new ApiError("invalid_request", "The request body could not be read as JSON."))
        : (StatusCodes.Status500InternalServerError, new ApiError("internal_error", "An unexpected error occurred."));
    context.Response.StatusCode = statusCode;
    await context.Response.WriteAsJsonAsync(error);
}));

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/students", async (AppDbContext db, IConfiguration configuration, CancellationToken cancellationToken) =>
    new StudentsResponse(await StudentScope.ListAsync(db, cancellationToken), DemoEndpoints.IsEnabled(configuration)));

// Every student-specific route names its student; there is no authentication, the id is the demo selection.
app.MapGet("/api/students/{studentId}/topics", async (
        string studentId, AppDbContext db, TimeProvider timeProvider, RetentionModelOptions options, CancellationToken cancellationToken) =>
    await StudentScope.CheckStudentAsync(db, studentId, cancellationToken) is { } failure
        ? failure.ToHttpResult()
        : Results.Ok(await TopicsQuery.GetTopicsAsync(db, studentId, timeProvider.GetUtcNow(), options, cancellationToken)));

app.MapGet("/api/students/{studentId}/topics/{topicId}", async (
        string studentId, string topicId, AppDbContext db, TimeProvider timeProvider, RetentionModelOptions options, CancellationToken cancellationToken) =>
    await StudentScope.CheckTopicAsync(db, studentId, topicId, cancellationToken) is { } failure
        ? failure.ToHttpResult()
        : Results.Ok(await TopicsQuery.GetTopicAsync(db, studentId, topicId, timeProvider.GetUtcNow(), options, cancellationToken)));

app.MapGet("/api/students/{studentId}/weekly-summary", async (
        string studentId, AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken) =>
    await StudentScope.CheckStudentAsync(db, studentId, cancellationToken) is { } failure
        ? failure.ToHttpResult()
        : Results.Ok(await WeeklySummaryQuery.GetAsync(db, studentId, timeProvider.GetUtcNow(), cancellationToken)));

// Demo integration contract for normal test results (see TestResultService); not an existing company integration.
app.MapPost("/api/students/{studentId}/topics/{topicId}/test-results", async (
        string studentId, string topicId, RecordTestResultRequest request, TestResultService results, CancellationToken cancellationToken) =>
    (await results.RecordNormalTestAsync(studentId, topicId, request, cancellationToken)).ToHttpResult());

app.MapPost("/api/students/{studentId}/topics/{topicId}/review-sessions", async (
        string studentId, string topicId, ReviewSessionService sessions, CancellationToken cancellationToken) =>
    (await sessions.StartOrResumeAsync(studentId, topicId, cancellationToken)).ToHttpResult());

app.MapPost("/api/students/{studentId}/review-sessions/{id:guid}/answers", async (
        string studentId, Guid id, SubmitAnswerRequest request, ReviewSessionService sessions, CancellationToken cancellationToken) =>
    (await sessions.SubmitAnswerAsync(studentId, id, request, cancellationToken)).ToHttpResult());

app.MapPost("/api/students/{studentId}/review-sessions/{id:guid}/complete", async (
        string studentId, Guid id, ReviewSessionService sessions, CancellationToken cancellationToken) =>
    (await sessions.CompleteAsync(studentId, id, cancellationToken)).ToHttpResult());

app.MapDemoEndpoints();

app.Run();
