using System.Text.Json;
using System.Text.Json.Serialization;
using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Reviews;
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
builder.Services.AddScoped<ReviewSessionService>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    SeedData.Initialize(db, clock.GetUtcNow());
    if (clock != TimeProvider.System) DemoClock.EnsureNotBeforeLastStudied(db, clock.GetUtcNow());
}

app.Logger.LogInformation("Database: {DatabasePath}; clock: {Clock}", databasePath,
    clock == TimeProvider.System ? "system UTC" : $"fixed {clock.GetUtcNow():O}");

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

app.MapGet("/api/topics", (AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken) =>
    TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, timeProvider.GetUtcNow(), cancellationToken));

app.MapGet("/api/topics/{id}", async (string id, AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken) =>
    await TopicsQuery.GetTopicAsync(db, SeedData.DemoStudentId, id, timeProvider.GetUtcNow(), cancellationToken) is { } topic
        ? Results.Ok(topic)
        : Results.Json(new ApiError("topic_not_found", $"Topic '{id}' does not exist."), statusCode: StatusCodes.Status404NotFound));

app.MapPost("/api/topics/{id}/review-sessions", async (string id, ReviewSessionService sessions, CancellationToken cancellationToken) =>
    (await sessions.StartOrResumeAsync(id, cancellationToken)).ToHttpResult());

app.MapPost("/api/review-sessions/{id:guid}/answers",
    async (Guid id, SubmitAnswerRequest request, ReviewSessionService sessions, CancellationToken cancellationToken) =>
        (await sessions.SubmitAnswerAsync(id, request, cancellationToken)).ToHttpResult());

app.MapPost("/api/review-sessions/{id:guid}/complete", async (Guid id, ReviewSessionService sessions, CancellationToken cancellationToken) =>
    (await sessions.CompleteAsync(id, cancellationToken)).ToHttpResult());

app.MapDemoReset();

app.Run();
