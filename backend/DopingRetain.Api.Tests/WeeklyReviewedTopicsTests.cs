using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.Topics;

namespace DopingRetain.Api.Tests;

/// <summary>GET /api/topics weeklyReviewedTopicCount: distinct topics completed this Istanbul week (UTC+3).</summary>
public sealed class WeeklyReviewedTopicsTests : IDisposable
{
    private const string Trigonometry = "ayt-mat-trigonometri";
    private const string CellDivision = "tyt-bio-hucre-bolunmeleri";
    private const string WordMeaning = "tyt-tur-sozcukte-anlam";

    // Wednesday 12:00 in Istanbul; that week starts Monday 2026-10-19 00:00 Istanbul = 2026-10-18T21:00Z.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeekStartUtc = new(2026, 10, 18, 21, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDatabase _database = new();

    public WeeklyReviewedTopicsTests()
    {
        using var db = _database.CreateContext();
        SeedData.Initialize(db, Now);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task NoCompletions_CountsZero()
    {
        Assert.Equal(0, await CountAt(Now));
    }

    [Fact]
    public async Task RepeatedCompletionsOfOneTopic_CountOnce()
    {
        AddSession(Trigonometry, ReviewSessionStatus.Completed, Now.AddHours(-1));
        AddSession(Trigonometry, ReviewSessionStatus.Completed, Now.AddDays(-1));
        AddSession(Trigonometry, ReviewSessionStatus.Completed, WeekStartUtc);

        Assert.Equal(1, await CountAt(Now));
    }

    [Fact]
    public async Task DifferentTopics_CountSeparately()
    {
        AddSession(Trigonometry, ReviewSessionStatus.Completed, Now.AddHours(-1));
        AddSession(CellDivision, ReviewSessionStatus.Completed, Now.AddHours(-2));
        AddSession(WordMeaning, ReviewSessionStatus.Completed, Now.AddDays(-2));

        Assert.Equal(3, await CountAt(Now));
    }

    [Fact]
    public async Task OpenAndAbandonedSessions_AreExcluded()
    {
        AddSession(Trigonometry, ReviewSessionStatus.Open, completedAt: null);
        // Even with a timestamp inside the week, only the Completed status counts.
        AddSession(CellDivision, ReviewSessionStatus.Abandoned, Now.AddHours(-1));
        AddSession(WordMeaning, ReviewSessionStatus.Completed, Now.AddHours(-1));

        Assert.Equal(1, await CountAt(Now));
    }

    [Fact]
    public async Task WeekStartsAtMondayMidnightInIstanbul()
    {
        AddSession(Trigonometry, ReviewSessionStatus.Completed, WeekStartUtc.AddSeconds(-1)); // Sunday 23:59:59 Istanbul
        AddSession(CellDivision, ReviewSessionStatus.Completed, WeekStartUtc); // Monday 00:00 Istanbul

        Assert.Equal(1, await CountAt(Now));
    }

    [Fact]
    public async Task WeekFollowsIstanbulDate_WhenUtcIsStillSunday()
    {
        // 2026-10-25T22:00Z is Monday 2026-10-26 01:00 in Istanbul but still Sunday in UTC:
        // a UTC week would still include Sunday evening.
        var mondayInIstanbul = new DateTimeOffset(2026, 10, 25, 22, 0, 0, TimeSpan.Zero);
        AddSession(Trigonometry, ReviewSessionStatus.Completed, new DateTimeOffset(2026, 10, 25, 20, 0, 0, TimeSpan.Zero)); // Sunday 23:00 Istanbul
        AddSession(CellDivision, ReviewSessionStatus.Completed, new DateTimeOffset(2026, 10, 25, 21, 0, 0, TimeSpan.Zero)); // Monday 00:00 Istanbul

        Assert.Equal(1, await CountAt(mondayInIstanbul));
    }

    [Fact]
    public async Task CompletionsAfterEvaluatedAt_AreExcluded()
    {
        AddSession(Trigonometry, ReviewSessionStatus.Completed, Now);
        AddSession(CellDivision, ReviewSessionStatus.Completed, Now.AddMinutes(1));

        Assert.Equal(1, await CountAt(Now));
    }

    [Fact]
    public async Task DemoReset_ReturnsCountToZero()
    {
        var clock = DemoClock.Create("2026-10-21T09:00:00Z", isDevelopment: true);
        using (var db = _database.CreateContext())
        {
            var sessions = new ReviewSessionService(db, clock, TestOptions.Default);
            var session = (await sessions.StartOrResumeAsync(Trigonometry)).Value!;
            foreach (var question in session.Questions)
            {
                await sessions.SubmitAnswerAsync(session.SessionId, new SubmitAnswerRequest(question.Id, 0));
            }
            Assert.Equal(200, (await sessions.CompleteAsync(session.SessionId)).StatusCode);
        }
        Assert.Equal(1, await CountAt(Now));

        using (var db = _database.CreateContext()) await DemoReset.ResetAsync(db, Now);

        Assert.Equal(0, await CountAt(Now));
    }

    private async Task<int> CountAt(DateTimeOffset now)
    {
        using var db = _database.CreateContext();
        return (await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, now, TestOptions.Default)).WeeklyReviewedTopicCount;
    }

    private void AddSession(string topicId, ReviewSessionStatus status, DateTimeOffset? completedAt)
    {
        using var db = _database.CreateContext();
        db.ReviewSessions.Add(new ReviewSession
        {
            Id = Guid.NewGuid(),
            StudentId = SeedData.DemoStudentId,
            TopicId = topicId,
            Status = status,
            StartedAt = (completedAt ?? Now).AddMinutes(-5),
            CompletedAt = completedAt,
        });
        db.SaveChanges();
    }
}
