using System.Text.Json;
using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.TestResults;
using DopingRetain.Api.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DopingRetain.Api.Tests;

public sealed class ReviewCompletionTests : IDisposable
{
    private const string Trigonometry = "ayt-mat-trigonometri";
    private const double Tolerance = 1e-9;

    // Seeding and completing at the same instant: Trigonometri's synthetic 18/20 normal test is due.
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

    // The quiz score itself is the new observation: 3/3 → 10 days, 2/3 → 10 · log2(0.667 / 0.5) ≈ 4.15 days,
    // 1/3 and 0/3 are at or below the threshold and need support now.
    [Theory]
    [InlineData(3, ReviewStatus.Scheduled)]
    [InlineData(2, ReviewStatus.Scheduled)]
    [InlineData(1, ReviewStatus.SupportNeeded)]
    [InlineData(0, ReviewStatus.SupportNeeded)]
    public async Task Complete_Trigonometry_StoresActualScoreAndSchedulesFromTheCurve(int score, ReviewStatus status)
    {
        var sessionId = await StartAndAnswer(correctCount: score);

        var result = await Complete(sessionId);

        Assert.Equal(200, result.StatusCode);
        var outcome = result.Value!;
        var accuracy = score / 3.0;
        var expectedNextReviewAt = accuracy > 0.5 ? Now.AddDays(10 * Math.Log2(accuracy / 0.5)) : Now;
        Assert.Equal((score, 3), (outcome.Score, outcome.Total));
        Assert.Equal(status, outcome.ReviewStatus);
        Assert.Equal(new TestResultDto(TestType.ReminderMiniQuiz, Now, score, 3), outcome.TestResult);
        Assert.Equal(Now, outcome.CompletedAt);
        Assert.Equal(expectedNextReviewAt, outcome.NextReviewAt);
        Assert.True(outcome.NextReviewAt >= outcome.CompletedAt);
        Assert.Equal(new CompletedTopicDto(Trigonometry, "Trigonometri", "AYT Matematik", Now), outcome.UpdatedTopic);

        using var db = _database.CreateContext();
        var state = db.StudentTopicStates.Single(s => s.TopicId == Trigonometry);
        Assert.Equal(((DateTimeOffset?)Now, 1), (state.LastReviewedAt, state.Version));
        var session = db.ReviewSessions.Single();
        Assert.Equal(
            (ReviewSessionStatus.Completed, (DateTimeOffset?)Now, (DateTimeOffset?)outcome.NextReviewAt),
            (session.Status, session.CompletedAt, session.NextReviewAt));
        // The original normal test stays; the mini quiz is added next to it with its real score and sample size.
        Assert.Equal(
            new[] { $"NormalTest|{SeedCompletedAt(Trigonometry):O}|18/20|", $"ReminderMiniQuiz|{Now:O}|{score}/3|{sessionId}" },
            ReadTestResults());
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
        Assert.Equal((1, 1), ReadMiniQuizCountAndVersion());
    }

    [Fact]
    public async Task Complete_Concurrently_UpdatesMemoryOnceAndReturnsOneOutcome()
    {
        var sessionId = await StartAndAnswer(correctCount: 2);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => Complete(sessionId))));

        Assert.All(results, result => Assert.Equal(200, result.StatusCode));
        Assert.Single(results.Select(result => Serialize(result.Value)).Distinct());
        Assert.Equal((1, 1), ReadMiniQuizCountAndVersion());
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
    public async Task Topics_ReflectTheMiniQuizAsLatestTestAndItsReviewDate()
    {
        await Complete(await StartAndAnswer(correctCount: 3));

        using var db = _database.CreateContext();
        var now = await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, Now, TestOptions.Default);
        var beforeThreshold = await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, Now.AddDays(10).AddMinutes(-1), TestOptions.Default);
        var atThreshold = await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, Now.AddDays(10), TestOptions.Default);

        var trigonometry = now.Topics.Single(t => t.Id == Trigonometry);
        Assert.Equal(ReviewStatus.Scheduled, trigonometry.ReviewStatus);
        Assert.Equal(new TestResultDto(TestType.ReminderMiniQuiz, Now, 3, 3), trigonometry.LatestTest);
        Assert.Equal(((DateTimeOffset?)Now, (DateTimeOffset?)Now.AddDays(10)), (trigonometry.LastReviewedAt, trigonometry.NextReviewAt));
        // Trigonometri leaves the due list; the other two due topics stay, in their order.
        Assert.Equal(new[] { "tyt-bio-hucre-bolunmeleri", "tyt-fiz-hareket-ve-kuvvet" }, now.DueTopicIds);

        Assert.Equal(ReviewStatus.Scheduled, beforeThreshold.Topics.Single(t => t.Id == Trigonometry).ReviewStatus);
        Assert.Equal(ReviewStatus.Due, atThreshold.Topics.Single(t => t.Id == Trigonometry).ReviewStatus);
        // Ten days on, every seeded normal test has crossed the threshold too; the earlier a topic crossed,
        // the further its recall has decayed, so it comes first. Trigonometri crosses exactly now.
        Assert.Equal(
            new[] { "tyt-bio-hucre-bolunmeleri", "tyt-fiz-hareket-ve-kuvvet", "tyt-tur-sozcukte-anlam", "tyt-kim-atom-ve-periyodik-sistem", Trigonometry },
            atThreshold.DueTopicIds);
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

    [Fact]
    public void Initialize_OnPhase5Database_FailsWithoutCreatingTestResults()
    {
        // A Phase 5 file has no TestResults table and no LastReviewedAt column; its old memory values
        // cannot be turned into test results, so the file is rejected rather than migrated.
        using (var db = _database.CreateContext())
        {
            db.Database.ExecuteSqlRaw("DROP TABLE TestResults; ALTER TABLE StudentTopicStates DROP COLUMN LastReviewedAt;");
        }

        using (var db = _database.CreateContext())
        {
            var error = Assert.Throws<InvalidOperationException>(() => SeedData.Initialize(db, Now));
            Assert.Contains("StudentTopicStates.LastReviewedAt", error.Message);
            Assert.Contains("TestResults.CorrectCount", error.Message);
            Assert.Contains("Point Database:Path at a new file", error.Message);
        }

        using var check = _database.CreateContext();
        Assert.Equal(0, check.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM sqlite_master WHERE name = 'TestResults'").Single());
        Assert.Equal(5, check.StudentTopicStates.Count(state => state.StudentId == SeedData.DemoStudentId));
    }

    private async Task<Guid> StartAndAnswer(int correctCount, int answerCount = 3, TemporaryDatabase? database = null)
    {
        database ??= _database;
        Guid sessionId;
        using (var db = database.CreateContext())
        {
            sessionId = (await new ReviewSessionService(db, ClockAt(Now), TestOptions.Default).StartOrResumeAsync(SeedData.DemoStudentId, Trigonometry)).Value!.SessionId;
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
        return await new ReviewSessionService(db, ClockAt(at ?? Now), TestOptions.Default).CompleteAsync(SeedData.DemoStudentId, sessionId);
    }

    private async Task<ServiceResult<AnswerFeedbackDto>> Answer(
        Guid sessionId, string questionId, int selectedIndex, IInterceptor? interceptor = null, TemporaryDatabase? database = null)
    {
        using var db = interceptor is null ? (database ?? _database).CreateContext() : _database.CreateContext(interceptor);
        return await new ReviewSessionService(db, ClockAt(Now), TestOptions.Default).SubmitAnswerAsync(SeedData.DemoStudentId, sessionId, new SubmitAnswerRequest(questionId, selectedIndex));
    }

    /// <summary>Every persisted value completion may write: review dates, test results and session completion fields.</summary>
    private string Snapshot()
    {
        using var db = _database.CreateContext();
        var states = db.StudentTopicStates.AsNoTracking().AsEnumerable().OrderBy(s => s.TopicId)
            .Select(s => $"{s.TopicId}|{s.LastReviewedAt:O}|{s.Version}");
        var results = db.TestResults.AsNoTracking().OrderBy(r => r.Id).AsEnumerable()
            .Select(r => $"{r.Id}|{r.TopicId}|{r.TestType}|{r.CompletedAt:O}|{r.CorrectCount}/{r.TotalQuestions}|{r.ReviewSessionId}");
        var sessions = db.ReviewSessions.AsNoTracking().AsEnumerable().OrderBy(s => s.Id)
            .Select(s => $"{s.Id}|{s.Status}|{s.CompletedAt:O}|{s.NextReviewAt:O}|{s.OutcomeJson}");
        return string.Join("\n", states.Concat(results).Concat(sessions));
    }

    private (int MiniQuizResults, int Version) ReadMiniQuizCountAndVersion()
    {
        using var db = _database.CreateContext();
        var state = db.StudentTopicStates.Single(s => s.TopicId == Trigonometry);
        return (db.TestResults.Count(r => r.TopicId == Trigonometry && r.TestType == TestType.ReminderMiniQuiz), state.Version);
    }

    private static DateTimeOffset SeedCompletedAt(string topicId) =>
        SeedData.CreateSeedTestResults(Now).Single(result => result.TopicId == topicId).CompletedAt;

    private List<string> ReadTestResults()
    {
        using var db = _database.CreateContext();
        return db.TestResults.AsNoTracking().Where(r => r.TopicId == Trigonometry).OrderBy(r => r.Id).AsEnumerable()
            .Select(r => $"{r.TestType}|{r.CompletedAt:O}|{r.CorrectCount}/{r.TotalQuestions}|{r.ReviewSessionId}")
            .ToList();
    }

    private int CountAnswers()
    {
        using var db = _database.CreateContext();
        return db.SessionAnswers.Count();
    }

    private static TimeProvider ClockAt(DateTimeOffset instant) => DemoClock.Create(instant.ToString("O"), isDevelopment: true);

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
