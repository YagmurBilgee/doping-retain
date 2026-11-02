using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.TestResults;
using DopingRetain.Api.Topics;

namespace DopingRetain.Api.Tests;

/// <summary>
/// GET /api/weekly-summary: completed reminder mini quiz sessions of the current Istanbul week, grouped by Istanbul
/// day. Instants come from a fixed clock on an isolated database.
/// </summary>
public sealed class WeeklySummaryTests : IDisposable
{
    private const string Trigonometry = "ayt-mat-trigonometri";
    private const string CellDivision = "tyt-bio-hucre-bolunmeleri";

    // Wednesday 2026-10-21 12:00 Istanbul. The week runs Monday 2026-10-19 00:00 to Monday 2026-10-26 00:00 Istanbul,
    // that is 2026-10-18T21:00Z to 2026-10-25T21:00Z.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeekStartUtc = new(2026, 10, 18, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static readonly Dictionary<string, (string Id, int Correct, int Wrong)[]> Questions = new()
    {
        [Trigonometry] = [("trig-q1", 3, 0), ("trig-q2", 0, 1), ("trig-q3", 1, 0)],
        [CellDivision] = [("bio-q1", 1, 0), ("bio-q2", 2, 0), ("bio-q3", 1, 0)],
    };

    private readonly TemporaryDatabase _database = new();

    public WeeklySummaryTests()
    {
        using var db = _database.CreateContext();
        // Seeded a week earlier, so every completion below is after the synthetic normal tests.
        SeedData.Initialize(db, Now.AddDays(-7));
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task EmptyWeek_HasSevenEmptyDaysFromMondayToSunday()
    {
        var summary = await SummaryAt(Now);

        Assert.Equal((Monday, new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 21)), (summary.FirstDate, summary.LastDate, summary.Today));
        Assert.Equal((0, 0, 0), (summary.DistinctTopicCount, summary.CompletedReviewCount, summary.ActiveDayCount));
        Assert.Equal(Enumerable.Range(0, 7).Select(Monday.AddDays), summary.Days.Select(day => day.Date));
        Assert.All(summary.Days, day => Assert.Equal((0, 0), (day.DistinctTopicCount, day.Topics.Count)));
    }

    [Fact]
    public async Task SameTopicTwiceOnOneDay_IsOneRowWithTwoReviewsAndTheLatestScore()
    {
        await Complete(Trigonometry, 1, Now.AddHours(-3));
        await Complete(Trigonometry, 2, Now.AddHours(-1));

        var summary = await SummaryAt(Now);

        Assert.Equal((1, 2, 1), (summary.DistinctTopicCount, summary.CompletedReviewCount, summary.ActiveDayCount));
        var wednesday = summary.Days[2];
        Assert.Equal(1, wednesday.DistinctTopicCount);
        var row = Assert.Single(wednesday.Topics);
        Assert.Equal(
            (Trigonometry, "Trigonometri", 2, Now.AddHours(-1), (int?)2, (int?)3),
            (row.TopicId, row.Title, row.ReviewCount, row.LastCompletedAt, row.CorrectCount, row.TotalQuestions));
    }

    [Fact]
    public async Task SameTopicOnDifferentDays_CountsOnEachDayButOnceForTheWeek()
    {
        await Complete(Trigonometry, 3, WeekStartUtc.AddHours(10)); // Monday 10:00 Istanbul
        await Complete(Trigonometry, 2, Now.AddHours(-1)); // Wednesday

        var summary = await SummaryAt(Now);

        Assert.Equal((1, 2, 2), (summary.DistinctTopicCount, summary.CompletedReviewCount, summary.ActiveDayCount));
        Assert.Equal(new[] { 1, 0, 1, 0, 0, 0, 0 }, summary.Days.Select(day => day.DistinctTopicCount));
    }

    [Fact]
    public async Task SeveralTopics_AreCountedPerDayAndForTheWeek()
    {
        await Complete(Trigonometry, 3, Now.AddHours(-2));
        await Complete(CellDivision, 0, Now.AddHours(-1));
        await Complete(CellDivision, 3, WeekStartUtc.AddDays(1).AddHours(9)); // Tuesday

        var summary = await SummaryAt(Now);

        Assert.Equal((2, 3, 2), (summary.DistinctTopicCount, summary.CompletedReviewCount, summary.ActiveDayCount));
        Assert.Equal(new[] { 0, 1, 2, 0, 0, 0, 0 }, summary.Days.Select(day => day.DistinctTopicCount));
        // Latest completion first.
        Assert.Equal(new[] { CellDivision, Trigonometry }, summary.Days[2].Topics.Select(topic => topic.TopicId));
    }

    [Fact]
    public async Task OpenAndAbandonedSessionsAndNormalTests_AreExcluded()
    {
        AddSession(SeedData.DemoStudentId, Trigonometry, ReviewSessionStatus.Open, completedAt: null);
        AddSession(SeedData.DemoStudentId, CellDivision, ReviewSessionStatus.Abandoned, Now.AddHours(-1));
        using (var db = _database.CreateContext())
        {
            var recorded = await new TestResultService(db, ClockAt(Now))
                .RecordNormalTestAsync(SeedData.DemoStudentId, Trigonometry, new RecordTestResultRequest(20, 20, Now.AddHours(-2)));
            Assert.Equal(201, recorded.StatusCode);
        }

        var summary = await SummaryAt(Now);

        Assert.Equal((0, 0, 0), (summary.DistinctTopicCount, summary.CompletedReviewCount, summary.ActiveDayCount));
    }

    [Fact]
    public async Task RepeatedCompletionRequest_DoesNotCountTwice()
    {
        var sessionId = await Complete(Trigonometry, 3, Now.AddHours(-1));
        using (var db = _database.CreateContext())
        {
            Assert.Equal(200, (await Service(db, Now).CompleteAsync(SeedData.DemoStudentId, sessionId)).StatusCode);
        }

        var summary = await SummaryAt(Now);

        Assert.Equal((1, 1), (summary.DistinctTopicCount, summary.CompletedReviewCount));
        Assert.Equal(1, Assert.Single(summary.Days[2].Topics).ReviewCount);
    }

    [Fact]
    public async Task DaysAndWeekFollowIstanbulMidnight()
    {
        var sunday = new DateTimeOffset(2026, 10, 25, 20, 30, 0, TimeSpan.Zero); // Sunday 23:30 Istanbul
        AddSession(SeedData.DemoStudentId, Trigonometry, ReviewSessionStatus.Completed, WeekStartUtc.AddSeconds(-1)); // previous Sunday 23:59:59
        AddSession(SeedData.DemoStudentId, CellDivision, ReviewSessionStatus.Completed, WeekStartUtc); // Monday 00:00
        AddSession(SeedData.DemoStudentId, Trigonometry, ReviewSessionStatus.Completed, WeekStartUtc.AddDays(1).AddSeconds(-1)); // Monday 23:59:59 (UTC: 20:59:59)
        AddSession(SeedData.DemoStudentId, Trigonometry, ReviewSessionStatus.Completed, new DateTimeOffset(2026, 10, 25, 20, 0, 0, TimeSpan.Zero)); // Sunday 23:00

        var summary = await SummaryAt(sunday);

        Assert.Equal(new[] { 2, 0, 0, 0, 0, 0, 1 }, summary.Days.Select(day => day.DistinctTopicCount));
        Assert.Equal((2, 3, 2), (summary.DistinctTopicCount, summary.CompletedReviewCount, summary.ActiveDayCount));
        Assert.Equal(new DateOnly(2026, 10, 25), summary.Today);
        // A session without a mini quiz result row has no score.
        Assert.Null(summary.Days[6].Topics[0].CorrectCount);

        // One hour later it is Monday 00:30 in Istanbul: a new, empty week.
        var nextWeek = await SummaryAt(sunday.AddHours(1));
        Assert.Equal((new DateOnly(2026, 10, 26), 0), (nextWeek.FirstDate, nextWeek.CompletedReviewCount));
    }

    [Fact]
    public async Task OtherStudentsSessions_AreExcluded()
    {
        using (var db = _database.CreateContext())
        {
            db.Students.Add(new Student { Id = "other-student", DisplayName = "Diğer Öğrenci" });
            db.StudentTopicStates.Add(new StudentTopicState { StudentId = "other-student", TopicId = Trigonometry });
            db.SaveChanges();
        }
        AddSession("other-student", Trigonometry, ReviewSessionStatus.Completed, Now.AddHours(-1));
        await Complete(CellDivision, 2, Now.AddHours(-2));

        var summary = await SummaryAt(Now);

        Assert.Equal((1, 1), (summary.DistinctTopicCount, summary.CompletedReviewCount));
        Assert.Equal(CellDivision, Assert.Single(summary.Days[2].Topics).TopicId);
    }

    [Fact]
    public async Task DistinctTopicCount_MatchesTheHomepageCounter()
    {
        await Complete(Trigonometry, 3, WeekStartUtc.AddHours(8));
        await Complete(Trigonometry, 1, Now.AddHours(-3));
        await Complete(CellDivision, 2, Now.AddHours(-2));
        AddSession(SeedData.DemoStudentId, CellDivision, ReviewSessionStatus.Abandoned, Now.AddHours(-1));

        var summary = await SummaryAt(Now);
        int homepageCount;
        using (var db = _database.CreateContext())
        {
            homepageCount = (await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, Now, TestOptions.Default)).WeeklyReviewedTopicCount;
        }

        Assert.Equal(2, summary.DistinctTopicCount);
        Assert.Equal(homepageCount, summary.DistinctTopicCount);
    }

    /// <summary>Starts, answers and completes a real review session at <paramref name="at"/>; returns its id.</summary>
    private async Task<Guid> Complete(string topicId, int correctCount, DateTimeOffset at)
    {
        Guid sessionId;
        using (var db = _database.CreateContext())
        {
            sessionId = (await Service(db, at).StartOrResumeAsync(SeedData.DemoStudentId, topicId)).Value!.SessionId;
        }
        foreach (var (question, index) in Questions[topicId].Select((question, index) => (question, index)))
        {
            using var db = _database.CreateContext();
            var selected = index < correctCount ? question.Correct : question.Wrong;
            Assert.Equal(200, (await Service(db, at).SubmitAnswerAsync(SeedData.DemoStudentId, sessionId, new SubmitAnswerRequest(question.Id, selected))).StatusCode);
        }
        using (var db = _database.CreateContext())
        {
            Assert.Equal(200, (await Service(db, at).CompleteAsync(SeedData.DemoStudentId, sessionId)).StatusCode);
        }
        return sessionId;
    }

    private void AddSession(string studentId, string topicId, ReviewSessionStatus status, DateTimeOffset? completedAt)
    {
        using var db = _database.CreateContext();
        db.ReviewSessions.Add(new ReviewSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            TopicId = topicId,
            Status = status,
            StartedAt = (completedAt ?? Now).AddMinutes(-5),
            CompletedAt = completedAt,
        });
        db.SaveChanges();
    }

    private async Task<WeeklySummaryResponse> SummaryAt(DateTimeOffset now)
    {
        using var db = _database.CreateContext();
        return await WeeklySummaryQuery.GetAsync(db, SeedData.DemoStudentId, now);
    }

    private static ReviewSessionService Service(AppDbContext db, DateTimeOffset at) => new(db, ClockAt(at), TestOptions.Default);

    private static TimeProvider ClockAt(DateTimeOffset instant) => DemoClock.Create(instant.ToString("O"), isDevelopment: true);
}
