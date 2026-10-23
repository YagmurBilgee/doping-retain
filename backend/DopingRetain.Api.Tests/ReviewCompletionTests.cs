using System.Text.Json;
using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.Tests.Fixtures;
using DopingRetain.Api.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DopingRetain.Api.Tests;

public sealed class ReviewCompletionTests : IDisposable
{
    private const string Trigonometry = "ayt-mat-trigonometri";
    private const double Tolerance = 1e-9;

    // Seeding and completing at the same instant gives t = 11 days exactly, as in the TypeScript fixture.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private static readonly (string QuestionId, int Correct, int Wrong)[] Questions =
    [
        ("trig-q1", 3, 0),
        ("trig-q2", 0, 1),
        ("trig-q3", 1, 0),
    ];

    private readonly TemporaryDatabase _database = new();

    public ReviewCompletionTests()
    {
        using var db = _database.CreateContext();
        SeedData.Initialize(db, Now);
    }

    public void Dispose() => _database.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Complete_Trigonometry_MatchesTypeScriptOutcomeAndPersistsIt(int score)
    {
        var expected = MemoryModelFixture.Instance.Reviews.Single(c => c.Name == $"Trigonometri {score}/3").Expected;
        var sessionId = await StartAndAnswer(correctCount: score);

        var result = await Complete(sessionId);

        Assert.Equal(200, result.StatusCode);
        var outcome = result.Value!;
        Assert.Equal((score, 3), (outcome.Score, outcome.Total));
        Assert.Equal(expected.Review.Tier, outcome.Review.Tier);
        Assert.Equal(expected.Review.NextReviewInDays, outcome.Review.NextReviewInDays);
        Assert.Equal(expected.Review.NewStrength, outcome.Review.NewStrength, Tolerance);
        Assert.Equal(expected.RetentionAtNextReview, outcome.RetentionAtNextReview);
        Assert.Equal(expected.RetentionGain, outcome.RetentionGain);
        Assert.Equal(expected.RiskAfter, outcome.RiskAfter);
        var riskBeforeReview = MemoryModelFixture.Instance.TopicRisks.Single(c => c.Name == Trigonometry).Expected;
        Assert.Equal(riskBeforeReview, outcome.RiskBefore);
        Assert.Equal(outcome.RetentionGain, outcome.RiskAfter.Retention - outcome.RiskBefore.Retention);

        var topic = outcome.UpdatedTopic;
        Assert.Equal((Trigonometry, 0d, Now), (topic.Id, topic.LastStudiedDaysAgo, topic.LastStudiedAt));
        Assert.Equal(expected.UpdatedTopic.CurrentStrength, topic.CurrentStrength, Tolerance);
        Assert.Equal(expected.UpdatedTopic.InitialRecall!.Value, topic.InitialRecall, Tolerance);
        Assert.Equal(expected.UpdatedTopic.PreviousSuccessRate, topic.PreviousSuccessRate, Tolerance);
        Assert.Equal(expected.UpdatedTopic.ReviewCount, topic.ReviewCount);
        Assert.Equal(expected.UpdatedTopic.NeedsFollowUp ?? false, topic.NeedsFollowUp);

        // NextReviewAt follows the 2 / 7 / 14-day rule from the completion instant.
        Assert.Equal(Now, outcome.CompletedAt);
        Assert.Equal(Now.AddDays(expected.Review.NextReviewInDays), outcome.NextReviewAt);

        using var db = _database.CreateContext();
        var state = db.StudentTopicStates.Single(s => s.TopicId == Trigonometry);
        Assert.Equal(
            (topic.CurrentStrength, topic.InitialRecall, Now, topic.ReviewCount, topic.PreviousSuccessRate, topic.NeedsFollowUp, 1),
            (state.Strength, state.InitialRecall, state.LastStudiedAt, state.ReviewCount, state.SuccessRate, state.NeedsFollowUp, state.Version));
        var session = db.ReviewSessions.Single();
        Assert.Equal(
            (ReviewSessionStatus.Completed, (DateTimeOffset?)Now, (DateTimeOffset?)outcome.NextReviewAt),
            (session.Status, session.CompletedAt, session.NextReviewAt));
    }

    [Fact]
    public async Task Complete_WithUnansweredQuestions_Returns409AndChangesNothing()
    {
        var sessionId = await StartAndAnswer(correctCount: 2, answerCount: 2);
        var before = Snapshot();

        var result = await Complete(sessionId);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("session_incomplete", result.Error!.Code);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task Complete_Again_AfterRestart_ReturnsStoredOutcomeWithoutUpdatingMemory()
    {
        var sessionId = await StartAndAnswer(correctCount: 3);
        var first = await Complete(sessionId);
        var afterFirst = Snapshot();

        // A new context, service and later clock stand in for an API restart; a recalculation would differ.
        var retry = await Complete(sessionId, Now.AddDays(5));

        Assert.Equal(200, retry.StatusCode);
        Assert.Equal(Serialize(first.Value), Serialize(retry.Value));
        Assert.Equal(afterFirst, Snapshot());
        Assert.Equal((2, 1), ReadReviewCountAndVersion());
    }

    [Fact]
    public async Task Complete_Concurrently_UpdatesMemoryOnceAndReturnsOneOutcome()
    {
        var sessionId = await StartAndAnswer(correctCount: 2);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => Complete(sessionId))));

        Assert.All(results, result => Assert.Equal(200, result.StatusCode));
        Assert.Single(results.Select(result => Serialize(result.Value)).Distinct());
        Assert.Equal((2, 1), ReadReviewCountAndVersion());
    }

    [Fact]
    public async Task Complete_WithStaleState_Returns409WithoutPartialWrites()
    {
        var sessionId = await StartAndAnswer(correctCount: 3);
        using (var db = _database.CreateContext())
        {
            // Memory state moved on after the session started (e.g. a future reset).
            db.Database.ExecuteSql($"UPDATE StudentTopicStates SET Version = Version + 1 WHERE TopicId = {Trigonometry}");
        }
        var before = Snapshot();

        var result = await Complete(sessionId);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("stale_session", result.Error!.Code);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task Answer_WhenSessionClosesBetweenReadsAndInsert_IsRejected()
    {
        var sessionId = await StartAndAnswer(correctCount: 2, answerCount: 2);
        var interceptor = new BeforeFirstInsert(() =>
        {
            using var other = _database.CreateContext();
            other.ReviewSessions.Single().Status = ReviewSessionStatus.Completed;
            other.SaveChanges();
        });

        var result = await Answer(sessionId, "trig-q3", 1, interceptor);

        Assert.True(interceptor.Ran);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("session_closed", result.Error!.Code);
        Assert.Equal(2, CountAnswers());
    }

    [Fact]
    public async Task Answer_AfterCompletion_RetryKeepsStoredFeedbackAndChangeIsRejected()
    {
        var sessionId = await StartAndAnswer(correctCount: 3);
        await Complete(sessionId);

        var retry = await Answer(sessionId, "trig-q1", 3);
        var change = await Answer(sessionId, "trig-q1", 0);

        Assert.Equal((200, true), (retry.StatusCode, retry.Value!.IsCorrect));
        Assert.Equal((409, "answer_conflict"), (change.StatusCode, change.Error!.Code));
        Assert.Equal(3, CountAnswers());
    }

    [Fact]
    public async Task AnswerAndCompletion_InParallel_NeverLoseOrLateAcceptAnAnswer()
    {
        for (var run = 0; run < 10; run++)
        {
            using var database = new TemporaryDatabase();
            using (var db = database.CreateContext()) SeedData.Initialize(db, Now);
            var sessionId = await StartAndAnswer(correctCount: 2, answerCount: 2, database);

            var answerTask = Task.Run(() => Answer(sessionId, "trig-q3", 1, database: database));
            var completeTask = Task.Run(() => Complete(sessionId, database: database));
            var (answer, completion) = (await answerTask, await completeTask);

            // Completion cannot succeed without the third answer, so that answer is always accepted.
            Assert.Equal(200, answer.StatusCode);
            if (completion.StatusCode == 200) Assert.Equal((3, 3), (completion.Value!.Score, completion.Value.Total));
            else Assert.Equal("session_incomplete", completion.Error!.Code);
        }
    }

    [Fact]
    public async Task Topics_ReflectPersistedStateAfterCompletion()
    {
        var expected = MemoryModelFixture.Instance.Reviews.Single(c => c.Name == "Trigonometri 3/3").Expected;
        await Complete(await StartAndAnswer(correctCount: 3));

        using var db = _database.CreateContext();
        var now = await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, Now);
        var atNextReview = await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, Now.AddDays(14));

        var trigonometry = now.Topics.Single(t => t.Id == Trigonometry);
        Assert.Equal((100, RiskLevel.Safe, Now, 2), (trigonometry.Retention, trigonometry.RiskLevel, trigonometry.LastStudiedAt, trigonometry.ReviewCount));
        Assert.Equal(expected.UpdatedTopic.PreviousSuccessRate, trigonometry.SuccessRate, Tolerance);
        Assert.Null(now.HighestRiskTopicId);
        Assert.Equal(0, now.HighRiskCount);
        Assert.Equal(expected.RetentionAtNextReview, atNextReview.Topics.Single(t => t.Id == Trigonometry).Retention);
    }

    [Fact]
    public void Initialize_OnPhase4Database_FailsWithoutAlteringIt()
    {
        using (var db = _database.CreateContext())
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE StudentTopicStates DROP COLUMN Version;");
        }

        using (var db = _database.CreateContext())
        {
            var error = Assert.Throws<InvalidOperationException>(() => SeedData.Initialize(db, Now));
            Assert.Contains("lacks columns: StudentTopicStates.Version", error.Message);
        }

        using var check = _database.CreateContext();
        Assert.Equal(0, check.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM pragma_table_info('StudentTopicStates') WHERE name = 'Version'").Single());
    }

    private async Task<Guid> StartAndAnswer(int correctCount, int answerCount = 3, TemporaryDatabase? database = null)
    {
        database ??= _database;
        Guid sessionId;
        using (var db = database.CreateContext())
        {
            sessionId = (await new ReviewSessionService(db, ClockAt(Now)).StartOrResumeAsync(Trigonometry)).Value!.SessionId;
        }

        foreach (var (question, index) in Questions.Take(answerCount).Select((question, index) => (question, index)))
        {
            var selected = index < correctCount ? question.Correct : question.Wrong;
            Assert.Equal(200, (await Answer(sessionId, question.QuestionId, selected, database: database)).StatusCode);
        }
        return sessionId;
    }

    private async Task<ServiceResult<ReviewCompletionResponse>> Complete(
        Guid sessionId, DateTimeOffset? at = null, TemporaryDatabase? database = null)
    {
        using var db = (database ?? _database).CreateContext();
        return await new ReviewSessionService(db, ClockAt(at ?? Now)).CompleteAsync(sessionId);
    }

    private async Task<ServiceResult<AnswerFeedbackDto>> Answer(
        Guid sessionId, string questionId, int selectedIndex, IInterceptor? interceptor = null, TemporaryDatabase? database = null)
    {
        using var db = interceptor is null ? (database ?? _database).CreateContext() : _database.CreateContext(interceptor);
        return await new ReviewSessionService(db, ClockAt(Now)).SubmitAnswerAsync(sessionId, new SubmitAnswerRequest(questionId, selectedIndex));
    }

    /// <summary>Every persisted value completion may write: memory state and session completion fields.</summary>
    private string Snapshot()
    {
        using var db = _database.CreateContext();
        var states = db.StudentTopicStates.AsNoTracking().AsEnumerable().OrderBy(s => s.TopicId)
            .Select(s => $"{s.TopicId}|{s.Strength}|{s.InitialRecall}|{s.LastStudiedAt:O}|{s.ReviewCount}|{s.SuccessRate}|{s.NeedsFollowUp}|{s.Version}");
        var sessions = db.ReviewSessions.AsNoTracking().AsEnumerable().OrderBy(s => s.Id)
            .Select(s => $"{s.Id}|{s.Status}|{s.CompletedAt:O}|{s.NextReviewAt:O}|{s.OutcomeJson}");
        return string.Join("\n", states.Concat(sessions));
    }

    private (int ReviewCount, int Version) ReadReviewCountAndVersion()
    {
        using var db = _database.CreateContext();
        var state = db.StudentTopicStates.Single(s => s.TopicId == Trigonometry);
        return (state.ReviewCount, state.Version);
    }

    private int CountAnswers()
    {
        using var db = _database.CreateContext();
        return db.SessionAnswers.Count();
    }

    private static TimeProvider ClockAt(DateTimeOffset instant) => DemoClock.Create(instant.ToString("O"), isDevelopment: true);

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
