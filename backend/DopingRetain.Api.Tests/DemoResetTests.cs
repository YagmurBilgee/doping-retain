using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.Topics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Tests;

public sealed class DemoResetTests : IDisposable
{
    private const string Trigonometry = "ayt-mat-trigonometri";
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResetAt = SeededAt.AddDays(3);

    private static readonly (string QuestionId, int Correct)[] Questions = [("trig-q1", 3), ("trig-q2", 0), ("trig-q3", 1)];

    private readonly TemporaryDatabase _database = new();

    public DemoResetTests()
    {
        using var db = _database.CreateContext();
        SeedData.Initialize(db, SeededAt);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Reset_AfterCompletedReview_RestoresInitialTopicValuesAtResetTime()
    {
        await Complete(await StartAndAnswer(3));

        await Reset(ResetAt);

        Assert.Equal(FreshSeedMemory(ResetAt), ReadMemory(_database));
        using var db = _database.CreateContext();
        var topics = await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, ResetAt);
        Assert.Equal(new[] { 46, 55, 86, 51 }, topics.Topics.Select(topic => topic.Retention));
        Assert.Equal(
            new[] { RiskLevel.HighRisk, RiskLevel.ReviewSoon, RiskLevel.Safe, RiskLevel.ReviewSoon },
            topics.Topics.Select(topic => topic.RiskLevel));
        Assert.Equal(ResetAt.AddDays(-11), topics.Topics[0].LastStudiedAt);
        Assert.Equal(Trigonometry, topics.HighestRiskTopicId);
        // Completion and reset each advanced the version once.
        Assert.Equal(2, db.StudentTopicStates.Single(s => s.TopicId == Trigonometry).Version);
    }

    [Fact]
    public async Task Reset_RemovesSessionsAndAnswers_KeepsContentAndStudent()
    {
        await Complete(await StartAndAnswer(2));
        await StartAndAnswer(1, answerCount: 1);

        await Reset(ResetAt);

        using var db = _database.CreateContext();
        Assert.Equal((0, 0), (db.ReviewSessions.Count(), db.SessionAnswers.Count()));
        Assert.Equal((1, 4, 3, 4), (db.Students.Count(), db.Topics.Count(), db.QuizQuestions.Count(), db.StudentTopicStates.Count()));
        Assert.Equal(new[] { "1/2", "√3/2", "0", "1" }, db.QuizQuestions.Single(q => q.Id == "trig-q1").Options);
    }

    [Fact]
    public async Task Reset_Repeated_IsSafeAndKeepsInitialValues()
    {
        await Reset(ResetAt);
        await Reset(ResetAt);

        Assert.Equal(FreshSeedMemory(ResetAt), ReadMemory(_database));
        using var db = _database.CreateContext();
        Assert.All(db.StudentTopicStates, state => Assert.Equal(2, state.Version));
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void ResetEndpoint_IsMappedOnlyInDevelopment(string environment, bool mapped)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        builder.Services.AddSingleton(TimeProvider.System);
        var app = builder.Build();

        app.MapDemoReset();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText);
        Assert.Equal(mapped, routes.Contains(DemoReset.Path));
    }

    [Fact]
    public async Task Start_WithStaleOpenSession_AbandonsItAndOpensAFreshUsableSession()
    {
        var staleId = await StartAndAnswer(2, answerCount: 1);
        BumpVersion();

        var restart = await Start();

        Assert.Equal(201, restart.StatusCode);
        var freshId = restart.Value!.SessionId;
        Assert.NotEqual(staleId, freshId);
        Assert.Empty(restart.Value.Answers);
        Assert.Equal(ReviewSessionStatus.Abandoned, ReadStatus(staleId));
        Assert.Equal(freshId, (await Start()).Value!.SessionId);

        foreach (var (questionId, correct) in Questions)
        {
            Assert.Equal(200, (await Answer(freshId, questionId, correct)).StatusCode);
        }
        Assert.Equal(200, (await Complete(freshId)).StatusCode);
    }

    [Fact]
    public async Task AbandonedSession_RejectsAnswersAndCompletion_WithoutTouchingMemory()
    {
        var staleId = await StartAndAnswer(3, answerCount: 2);
        BumpVersion();
        await Start();
        var before = ReadMemory(_database);

        var newAnswer = await Answer(staleId, "trig-q3", 1);
        var retry = await Answer(staleId, "trig-q1", 3);
        var completion = await Complete(staleId);

        Assert.Equal((409, "session_abandoned"), (newAnswer.StatusCode, newAnswer.Error!.Code));
        Assert.Equal((409, "session_abandoned"), (retry.StatusCode, retry.Error!.Code));
        Assert.Equal((409, "session_abandoned"), (completion.StatusCode, completion.Error!.Code));
        Assert.Equal(before, ReadMemory(_database));
    }

    [Fact]
    public async Task SessionRemovedByReset_ReturnsNotFoundForAnswerAndCompletion()
    {
        var sessionId = await StartAndAnswer(3);
        await Reset(ResetAt);

        var answer = await Answer(sessionId, "trig-q1", 3);
        var completion = await Complete(sessionId);

        Assert.Equal((404, "session_not_found"), (answer.StatusCode, answer.Error!.Code));
        Assert.Equal((404, "session_not_found"), (completion.StatusCode, completion.Error!.Code));
        Assert.Equal(FreshSeedMemory(ResetAt), ReadMemory(_database));
    }

    [Fact]
    public async Task Answer_WhenResetRunsBetweenReadsAndInsert_ReturnsNotFoundAndStoresNothing()
    {
        var sessionId = await StartAndAnswer(2, answerCount: 2);
        var interceptor = new BeforeFirstInsert(() => Reset(ResetAt));

        using var db = _database.CreateContext(interceptor);
        var result = await new ReviewSessionService(db, ClockAt(SeededAt))
            .SubmitAnswerAsync(sessionId, new SubmitAnswerRequest("trig-q3", 1));

        Assert.True(interceptor.Ran);
        Assert.Equal((404, "session_not_found"), (result.StatusCode, result.Error!.Code));
        using var check = _database.CreateContext();
        Assert.Equal(0, check.SessionAnswers.Count());
    }

    [Fact]
    public async Task ResetAndCompletion_InParallel_NeverResurrectPreResetState()
    {
        for (var run = 0; run < 10; run++)
        {
            using var database = new TemporaryDatabase();
            using (var db = database.CreateContext()) SeedData.Initialize(db, SeededAt);
            var sessionId = await StartAndAnswer(3, database: database);

            var completion = Task.Run(() => Complete(sessionId, database));
            var reset = Task.Run(() => Reset(ResetAt, database));
            await Task.WhenAll(completion, reset);

            // Either completion committed first and reset undid it, or it found the session gone.
            Assert.Contains((await completion).StatusCode, new[] { 200, 404 });
            Assert.Equal(FreshSeedMemory(ResetAt), ReadMemory(database));
            using var check = database.CreateContext();
            Assert.Equal(0, check.ReviewSessions.Count());
        }
    }

    private async Task<Guid> StartAndAnswer(int correctCount, int answerCount = 3, TemporaryDatabase? database = null)
    {
        var sessionId = (await Start(database)).Value!.SessionId;
        foreach (var (question, index) in Questions.Take(answerCount).Select((question, index) => (question, index)))
        {
            var selected = index < correctCount ? question.Correct : (question.Correct + 1) % 4;
            Assert.Equal(200, (await Answer(sessionId, question.QuestionId, selected, database)).StatusCode);
        }
        return sessionId;
    }

    private async Task<ServiceResult<ReviewSessionResponse>> Start(TemporaryDatabase? database = null)
    {
        using var db = (database ?? _database).CreateContext();
        return await new ReviewSessionService(db, ClockAt(SeededAt)).StartOrResumeAsync(Trigonometry);
    }

    private async Task<ServiceResult<AnswerFeedbackDto>> Answer(
        Guid sessionId, string questionId, int selectedIndex, TemporaryDatabase? database = null)
    {
        using var db = (database ?? _database).CreateContext();
        return await new ReviewSessionService(db, ClockAt(SeededAt))
            .SubmitAnswerAsync(sessionId, new SubmitAnswerRequest(questionId, selectedIndex));
    }

    private async Task<ServiceResult<ReviewCompletionResponse>> Complete(Guid sessionId, TemporaryDatabase? database = null)
    {
        using var db = (database ?? _database).CreateContext();
        return await new ReviewSessionService(db, ClockAt(SeededAt)).CompleteAsync(sessionId);
    }

    private async Task Reset(DateTimeOffset at, TemporaryDatabase? database = null)
    {
        using var db = (database ?? _database).CreateContext();
        await DemoReset.ResetAsync(db, at);
    }

    private void BumpVersion()
    {
        using var db = _database.CreateContext();
        db.Database.ExecuteSql($"UPDATE StudentTopicStates SET Version = Version + 1 WHERE TopicId = {Trigonometry}");
    }

    private ReviewSessionStatus ReadStatus(Guid sessionId)
    {
        using var db = _database.CreateContext();
        return db.ReviewSessions.Single(session => session.Id == sessionId).Status;
    }

    /// <summary>Memory values of a database seeded fresh at <paramref name="at"/>: the reset target.</summary>
    private static List<string> FreshSeedMemory(DateTimeOffset at)
    {
        using var fresh = new TemporaryDatabase();
        using (var db = fresh.CreateContext()) SeedData.Initialize(db, at);
        return ReadMemory(fresh);
    }

    /// <summary>Learning values of every topic state; Version is a concurrency token, not a learning value.</summary>
    private static List<string> ReadMemory(TemporaryDatabase database)
    {
        using var db = database.CreateContext();
        return db.StudentTopicStates
            .AsNoTracking()
            .AsEnumerable()
            .OrderBy(state => state.TopicId)
            .Select(state => $"{state.StudentId}|{state.TopicId}|{state.Strength}|{state.InitialRecall}|{state.LastStudiedAt:O}|" +
                $"{state.ReviewCount}|{state.SuccessRate}|{state.NeedsFollowUp}")
            .ToList();
    }

    private static TimeProvider ClockAt(DateTimeOffset instant) => DemoClock.Create(instant.ToString("O"), isDevelopment: true);
}
