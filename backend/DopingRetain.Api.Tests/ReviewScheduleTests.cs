using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.TestResults;
using DopingRetain.Api.Topics;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Tests;

/// <summary>
/// The review scheduling policy on top of the curve: 10 days after the first on-time perfect mini quiz, the
/// extended 30 days after the next ones, early perfect retries changing nothing, and resets by non-perfect
/// quizzes and newer normal tests. Every instant comes from a fixed clock on an isolated database.
/// </summary>
public sealed class ReviewScheduleTests : IDisposable
{
    private const string Trigonometry = "ayt-mat-trigonometri";

    // Seeding at this instant makes Trigonometri's synthetic 18/20 normal test due.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstDue = Now.AddDays(10);

    private static readonly (string QuestionId, int Correct, int Wrong)[] Questions =
    [
        ("trig-q1", 3, 0),
        ("trig-q2", 0, 1),
        ("trig-q3", 1, 0),
    ];

    private readonly TemporaryDatabase _database = new();

    public ReviewScheduleTests()
    {
        using var db = _database.CreateContext();
        SeedData.Initialize(db, Now);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task FirstPerfectReview_SchedulesTenDaysFromTheFormula()
    {
        var outcome = await Review(3, Now);

        Assert.Equal((ReviewStatus.Scheduled, FirstDue), (outcome.ReviewStatus, outcome.NextReviewAt));
        Assert.Equal(1, ReadStreak(outcome.SessionId));
        Assert.Equal((ReviewStatus.Scheduled, (DateTimeOffset?)FirstDue), await TopicAt(Now));
    }

    [Fact]
    public async Task SecondPerfectReview_JustBeforeDue_NeitherExtendsNorPostpones()
    {
        await Review(3, Now);

        var early = await Review(3, FirstDue.AddMinutes(-1));

        Assert.Equal((ReviewStatus.Scheduled, FirstDue), (early.ReviewStatus, early.NextReviewAt));
        Assert.Equal(1, ReadStreak(early.SessionId));
        Assert.Equal((ReviewStatus.Due, (DateTimeOffset?)FirstDue), await TopicAt(FirstDue));
    }

    [Fact]
    public async Task SecondPerfectReview_ExactlyAtDue_SchedulesThirtyDays()
    {
        await Review(3, Now);

        var onTime = await Review(3, FirstDue);

        Assert.Equal((ReviewStatus.Scheduled, FirstDue.AddDays(30)), (onTime.ReviewStatus, onTime.NextReviewAt));
        Assert.Equal(2, ReadStreak(onTime.SessionId));
    }

    [Fact]
    public async Task LateQualifyingPerfectReview_SchedulesThirtyDaysFromActualCompletion()
    {
        await Review(3, Now);
        var late = FirstDue.AddDays(5).AddHours(3);

        var outcome = await Review(3, late);

        Assert.Equal(late.AddDays(30), outcome.NextReviewAt);
    }

    [Fact]
    public async Task ExtendedTopic_StaysScheduledAfterTenDays_AndBecomesDueAtThirty()
    {
        await Review(3, Now);
        await Review(3, FirstDue);
        var extendedDue = FirstDue.AddDays(30);

        // The unmodified curve would make the 3/3 quiz due after 10 days; the policy's date controls the status.
        Assert.Equal((ReviewStatus.Scheduled, (DateTimeOffset?)extendedDue), await TopicAt(FirstDue.AddDays(10)));
        Assert.Equal((ReviewStatus.Scheduled, (DateTimeOffset?)extendedDue), await TopicAt(extendedDue.AddSeconds(-1)));
        Assert.Equal((ReviewStatus.Due, (DateTimeOffset?)extendedDue), await TopicAt(extendedDue));
        Assert.Contains(Trigonometry, (await TopicsAt(extendedDue)).DueTopicIds);
        Assert.DoesNotContain(Trigonometry, (await TopicsAt(FirstDue.AddDays(10))).DueTopicIds);

        using var db = _database.CreateContext();
        var detail = await TopicsQuery.GetTopicAsync(db, SeedData.DemoStudentId, Trigonometry, FirstDue.AddDays(10), TestOptions.Default);
        Assert.Equal((ReviewStatus.Scheduled, (DateTimeOffset?)extendedDue), (detail!.ReviewStatus, detail.NextReviewAt));
    }

    [Fact]
    public async Task FurtherDuePerfectReview_KeepsThirtyDays()
    {
        await Review(3, Now);
        await Review(3, FirstDue);
        var secondDue = FirstDue.AddDays(30);

        var third = await Review(3, secondDue);

        Assert.Equal(secondDue.AddDays(30), third.NextReviewAt);
        Assert.Equal(3, ReadStreak(third.SessionId));
    }

    [Fact]
    public async Task EarlyPerfectRetry_PreservesTheExtendedScheduleAndEligibility()
    {
        await Review(3, Now);
        await Review(3, FirstDue);
        var extendedDue = FirstDue.AddDays(30);

        var retry = await Review(3, FirstDue.AddDays(5));

        Assert.Equal((ReviewStatus.Scheduled, extendedDue), (retry.ReviewStatus, retry.NextReviewAt));
        Assert.Equal(2, ReadStreak(retry.SessionId));
        Assert.Equal((ReviewStatus.Due, (DateTimeOffset?)extendedDue), await TopicAt(extendedDue));

        var next = await Review(3, extendedDue);
        Assert.Equal(extendedDue.AddDays(30), next.NextReviewAt);
    }

    [Fact]
    public async Task EarlyNonPerfectReview_ResetsEligibility_AndTheNextPerfectStartsAtTenDays()
    {
        await Review(3, Now);
        await Review(3, FirstDue);
        var earlyAt = FirstDue.AddDays(5);

        var twoOfThree = await Review(2, earlyAt);

        var formulaDue = earlyAt.AddDays(10 * Math.Log2(2.0 / 3 / 0.5));
        Assert.Equal((ReviewStatus.Scheduled, formulaDue), (twoOfThree.ReviewStatus, twoOfThree.NextReviewAt));
        Assert.Equal(0, ReadStreak(twoOfThree.SessionId));

        var restart = await Review(3, formulaDue);
        Assert.Equal(formulaDue.AddDays(10), restart.NextReviewAt);
        Assert.Equal(1, ReadStreak(restart.SessionId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task WeakReview_NeedsSupportNow_AndTheNextPerfectStartsAtTenDays(int score)
    {
        await Review(3, Now);
        var weakAt = Now.AddDays(2);

        var weak = await Review(score, weakAt);

        Assert.Equal((ReviewStatus.SupportNeeded, weakAt), (weak.ReviewStatus, weak.NextReviewAt));
        Assert.Equal(0, ReadStreak(weak.SessionId));

        var afterSupport = Now.AddDays(3);
        var restart = await Review(3, afterSupport);
        Assert.Equal(afterSupport.AddDays(10), restart.NextReviewAt);
    }

    // A reinforcement of a support-needed topic is an ordinary mini quiz session: the support state is always
    // on time, and the weak score reset the sequence, so the stored score alone decides the recommendation.
    [Theory]
    [InlineData(2, ReviewStatus.Scheduled)]
    [InlineData(1, ReviewStatus.SupportNeeded)]
    [InlineData(0, ReviewStatus.SupportNeeded)]
    public async Task ReinforcementAfterSupport_UsesTheScoreBasedRecommendation(int score, ReviewStatus status)
    {
        await Review(0, Now);
        var reinforcementAt = Now.AddHours(1);

        var outcome = await Review(score, reinforcementAt);

        var expected = score == 2 ? reinforcementAt.AddDays(10 * Math.Log2(2.0 / 3 / 0.5)) : reinforcementAt;
        Assert.Equal((status, expected), (outcome.ReviewStatus, outcome.NextReviewAt));
        Assert.Equal((status, (DateTimeOffset?)expected), await TopicAt(reinforcementAt));
    }

    [Fact]
    public async Task PerfectReinforcement_StartsAtTenDays_AndTheNextOnTimePerfectGetsThirty()
    {
        await Review(1, Now);
        var reinforcementAt = Now.AddHours(1);

        var reinforcement = await Review(3, reinforcementAt);
        var nextDue = reinforcementAt.AddDays(10);
        Assert.Equal((ReviewStatus.Scheduled, nextDue), (reinforcement.ReviewStatus, reinforcement.NextReviewAt));

        var onTime = await Review(3, nextDue);
        Assert.Equal(nextDue.AddDays(30), onTime.NextReviewAt);
    }

    [Fact]
    public async Task DueNonPerfectReview_BreaksTheSequence()
    {
        await Review(3, Now);
        var twoOfThree = await Review(2, FirstDue);
        var formulaDue = FirstDue.AddDays(10 * Math.Log2(2.0 / 3 / 0.5));
        Assert.Equal(formulaDue, twoOfThree.NextReviewAt);

        var restart = await Review(3, formulaDue);

        Assert.Equal(formulaDue.AddDays(10), restart.NextReviewAt);
    }

    [Fact]
    public async Task NewerNormalTest_ReplacesTheExtendedSchedule_AndResetsEligibility()
    {
        await Review(3, Now);
        await Review(3, FirstDue);
        var testAt = FirstDue.AddDays(1);

        var recorded = await RecordNormalTest(20, 20, completedAt: testAt, clockAt: testAt);

        Assert.True(recorded.IsLatest);
        Assert.Equal((ReviewStatus.Scheduled, (DateTimeOffset?)testAt.AddDays(10)), await TopicAt(testAt));
        Assert.Equal((ReviewStatus.Due, (DateTimeOffset?)testAt.AddDays(10)), await TopicAt(testAt.AddDays(10)));

        // A perfect mini quiz after the normal test is the first of a new sequence: the formula again.
        var fresh = await Review(3, testAt.AddDays(10));
        Assert.Equal(testAt.AddDays(20), fresh.NextReviewAt);
        Assert.Equal(1, ReadStreak(fresh.SessionId));
    }

    [Fact]
    public async Task OlderNormalTestSubmittedLate_ChangesNeitherScheduleNorEligibility()
    {
        await Review(3, Now);
        await Review(3, FirstDue);
        var extendedDue = FirstDue.AddDays(30);

        var recorded = await RecordNormalTest(5, 20, completedAt: FirstDue.AddDays(-1), clockAt: FirstDue.AddDays(2));

        Assert.False(recorded.IsLatest);
        Assert.Equal((ReviewStatus.Scheduled, (DateTimeOffset?)extendedDue), await TopicAt(FirstDue.AddDays(2)));
        var next = await Review(3, extendedDue);
        Assert.Equal(extendedDue.AddDays(30), next.NextReviewAt);
    }

    [Fact]
    public async Task NormalTest_NeverCountsAsAPerfectMiniReview()
    {
        // A perfect normal test followed by an on-time perfect mini quiz is a first perfect review, not a second.
        await RecordNormalTest(20, 20, completedAt: Now, clockAt: Now);

        var mini = await Review(3, FirstDue);

        Assert.Equal(FirstDue.AddDays(10), mini.NextReviewAt);
        Assert.Equal(1, ReadStreak(mini.SessionId));
    }

    [Fact]
    public async Task DuplicateCompletion_AfterRestart_DoesNotAdvanceTheSequence()
    {
        await Review(3, Now);
        var qualifying = await Review(3, FirstDue);

        // A new context and a later clock stand in for a restart; the stored outcome comes back unchanged.
        ServiceResult<ReviewCompletionResponse> retry;
        using (var db = _database.CreateContext())
        {
            retry = await Service(db, FirstDue.AddDays(40)).CompleteAsync(qualifying.SessionId);
        }

        Assert.Equal(200, retry.StatusCode);
        Assert.Equal(qualifying.NextReviewAt, retry.Value!.NextReviewAt);
        Assert.Equal(2, ReadStreak(qualifying.SessionId));
        using (var db = _database.CreateContext())
        {
            Assert.Equal(2, db.TestResults.Count(r => r.TopicId == Trigonometry && r.TestType == TestType.ReminderMiniQuiz));
        }
        Assert.Equal((ReviewStatus.Scheduled, (DateTimeOffset?)FirstDue.AddDays(30)), await TopicAt(FirstDue.AddDays(15)));
    }

    [Fact]
    public async Task WeeklyCount_StillCountsATopicOnce_AcrossRetries()
    {
        await Review(3, Now);
        await Review(3, Now.AddHours(2));

        using var db = _database.CreateContext();
        var topics = await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, Now.AddHours(3), TestOptions.Default);
        Assert.Equal(1, topics.WeeklyReviewedTopicCount);
    }

    [Fact]
    public async Task ExtendedInterval_IsConfigurable()
    {
        var options = new ReviewScheduleOptions { ExtendedIntervalDays = 21 };
        await Review(3, Now, options);

        var onTime = await Review(3, FirstDue, options);

        Assert.Equal(FirstDue.AddDays(21), onTime.NextReviewAt);
    }

    // The minimum is the formula's interval for a perfect score, h · log2(1 / threshold): 10 days by default,
    // 20 with h = 20, and 10 · log2(1 / 0.25) = 20 with threshold 0.25.
    [Theory]
    [InlineData(10, 0.5, 9.99)]
    [InlineData(20, 0.5, 19.5)]
    [InlineData(10, 0.25, 19.9)]
    [InlineData(10, 0.5, 0)]
    [InlineData(10, 0.5, -1)]
    [InlineData(10, 0.5, double.NaN)]
    [InlineData(10, 0.5, double.PositiveInfinity)]
    public void ExtendedInterval_ShorterThanThePerfectScoreInterval_IsRejected(double halfLife, double threshold, double days)
    {
        var retention = new RetentionModelOptions { HalfLifeDays = halfLife, ReviewThreshold = threshold }.Validated();

        var error = Assert.Throws<InvalidOperationException>(
            () => new ReviewScheduleOptions { ExtendedIntervalDays = days }.Validated(retention));

        Assert.Contains("ReviewSchedule:ExtendedIntervalDays must be at least", error.Message);
        // Culture-independent: "0.5", not "0,5", also on a Turkish machine.
        Assert.Contains($"ReviewThreshold {threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)};", error.Message);
    }

    [Theory]
    [InlineData(10, 0.5, 10)]
    [InlineData(20, 0.5, 20)]
    [InlineData(10, 0.25, 20)]
    [InlineData(10, 0.5, 30)]
    [InlineData(20, 0.5, 45)]
    public void ExtendedInterval_EqualToOrLongerThanThePerfectScoreInterval_IsAccepted(double halfLife, double threshold, double days)
    {
        var retention = new RetentionModelOptions { HalfLifeDays = halfLife, ReviewThreshold = threshold }.Validated();
        var options = new ReviewScheduleOptions { ExtendedIntervalDays = days };

        Assert.Same(options, options.Validated(retention));
    }

    [Fact]
    public void DefaultSettings_AreValid() =>
        new ReviewScheduleOptions().Validated(TestOptions.Default);

    [Fact]
    public async Task Initialize_OnDatabaseWithoutStreakColumn_AddsItWithoutChangingRows_AndGrantsNoLegacyStreak()
    {
        await Review(3, Now);
        using (var db = _database.CreateContext())
        {
            // A database from before the policy: perfect history exists, but no sequence was ever recorded.
            db.Database.ExecuteSqlRaw("ALTER TABLE ReviewSessions DROP COLUMN PerfectReviewStreak;");
        }
        var before = Snapshot();

        using (var db = _database.CreateContext())
        {
            SeedData.Initialize(db, FirstDue);
        }

        Assert.Equal(before, Snapshot());
        using (var db = _database.CreateContext())
        {
            Assert.Equal(0, db.ReviewSessions.Single().PerfectReviewStreak);
        }
        Assert.Equal((ReviewStatus.Due, (DateTimeOffset?)FirstDue), await TopicAt(FirstDue));
        var next = await Review(3, FirstDue);
        Assert.Equal(FirstDue.AddDays(10), next.NextReviewAt);
    }

    [Fact]
    public void Initialize_WithOtherMissingColumns_FailsWithoutAddingTheStreakColumn()
    {
        using (var db = _database.CreateContext())
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE ReviewSessions DROP COLUMN PerfectReviewStreak; ALTER TABLE StudentTopicStates DROP COLUMN Version;");
        }

        using (var db = _database.CreateContext())
        {
            var error = Assert.Throws<InvalidOperationException>(() => SeedData.Initialize(db, Now));
            Assert.Contains("ReviewSessions.PerfectReviewStreak", error.Message);
            Assert.Contains("StudentTopicStates.Version", error.Message);
        }

        using var check = _database.CreateContext();
        Assert.Equal(0, check.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM pragma_table_info('ReviewSessions') WHERE name = 'PerfectReviewStreak'").Single());
    }

    private async Task<ReviewCompletionResponse> Review(int correctCount, DateTimeOffset at, ReviewScheduleOptions? options = null)
    {
        Guid sessionId;
        using (var db = _database.CreateContext())
        {
            sessionId = (await Service(db, at, options).StartOrResumeAsync(Trigonometry)).Value!.SessionId;
        }
        foreach (var (question, index) in Questions.Select((question, index) => (question, index)))
        {
            using var db = _database.CreateContext();
            var selected = index < correctCount ? question.Correct : question.Wrong;
            var answer = await Service(db, at, options).SubmitAnswerAsync(sessionId, new SubmitAnswerRequest(question.QuestionId, selected));
            Assert.Equal(200, answer.StatusCode);
        }
        using (var db = _database.CreateContext())
        {
            var result = await Service(db, at, options).CompleteAsync(sessionId);
            Assert.Equal(200, result.StatusCode);
            return result.Value!;
        }
    }

    private async Task<RecordedTestResultResponse> RecordNormalTest(
        int correctCount, int totalQuestions, DateTimeOffset completedAt, DateTimeOffset clockAt)
    {
        using var db = _database.CreateContext();
        var result = await new TestResultService(db, ClockAt(clockAt))
            .RecordNormalTestAsync(Trigonometry, new RecordTestResultRequest(correctCount, totalQuestions, completedAt));
        Assert.Equal(201, result.StatusCode);
        return result.Value!;
    }

    private async Task<TopicsResponse> TopicsAt(DateTimeOffset at)
    {
        using var db = _database.CreateContext();
        return await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, at, TestOptions.Default);
    }

    private async Task<(ReviewStatus Status, DateTimeOffset? NextReviewAt)> TopicAt(DateTimeOffset at)
    {
        var topic = (await TopicsAt(at)).Topics.Single(t => t.Id == Trigonometry);
        return (topic.ReviewStatus, topic.NextReviewAt);
    }

    private int ReadStreak(Guid sessionId)
    {
        using var db = _database.CreateContext();
        return db.ReviewSessions.Single(s => s.Id == sessionId).PerfectReviewStreak;
    }

    /// <summary>Stored rows a schema update must leave untouched.</summary>
    private string Snapshot()
    {
        using var db = _database.CreateContext();
        var states = db.Database.SqlQuery<string>($"""
            SELECT "TopicId" || '|' || IFNULL("LastReviewedAt", '') || '|' || "Version" AS Value FROM "StudentTopicStates" ORDER BY "TopicId"
            """).ToList();
        var results = db.Database.SqlQuery<string>($"""
            SELECT "Id" || '|' || "TestType" || '|' || "CompletedAt" || '|' || "CorrectCount" || '/' || "TotalQuestions" AS Value FROM "TestResults" ORDER BY "Id"
            """).ToList();
        var sessions = db.Database.SqlQuery<string>($"""
            SELECT "Id" || '|' || "Status" || '|' || IFNULL("NextReviewAt", '') || '|' || IFNULL("OutcomeJson", '') AS Value FROM "ReviewSessions" ORDER BY "Id"
            """).ToList();
        return string.Join("\n", states.Concat(results).Concat(sessions));
    }

    private static ReviewSessionService Service(AppDbContext db, DateTimeOffset at, ReviewScheduleOptions? options = null) =>
        new(db, ClockAt(at), TestOptions.Default, options);

    private static TimeProvider ClockAt(DateTimeOffset instant) => DemoClock.Create(instant.ToString("O"), isDevelopment: true);
}
