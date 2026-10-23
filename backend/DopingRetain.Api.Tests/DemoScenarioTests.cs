using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.Topics;

namespace DopingRetain.Api.Tests;

/// <summary>
/// The five-topic demo scenario after a fresh seed or a demo reset: three topics due today, one tomorrow and one
/// three days later, on the Europe/Istanbul calendar, at any time of day.
/// </summary>
public sealed class DemoScenarioTests
{
    private const string Trigonometry = "ayt-mat-trigonometri";
    private const string CellDivision = "tyt-bio-hucre-bolunmeleri";
    private const string WordMeaning = "tyt-tur-sozcukte-anlam";
    private const string Motion = "tyt-fiz-hareket-ve-kuvvet";
    private const string Atoms = "tyt-kim-atom-ve-periyodik-sistem";

    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private static readonly Dictionary<string, int[]> CorrectAnswers = new()
    {
        [Trigonometry] = [3, 0, 1],
        [CellDivision] = [1, 2, 1],
        [Motion] = [0, 1, 2],
    };

    public static TheoryData<string> ReferenceInstants => new()
    {
        "2026-10-20T21:00:30Z", // 00:00:30 in Istanbul: just after midnight
        "2026-10-21T09:30:00Z", // 12:30
        "2026-10-21T20:59:00Z", // 23:59
        "2026-12-31T21:05:00Z", // 00:05 on New Year's Day
    };

    [Theory]
    [MemberData(nameof(ReferenceInstants))]
    public async Task FreshSeed_HasThreeDueToday_OneTomorrow_OneInThreeDays(string instant)
    {
        var now = DateTimeOffset.Parse(instant);
        using var database = new TemporaryDatabase();
        using (var db = database.CreateContext()) SeedData.Initialize(db, now);

        AssertScenario(await GetTopics(database, now), now);
    }

    [Theory]
    [MemberData(nameof(ReferenceInstants))]
    public async Task DemoReset_RestoresTheSameScenarioForItsOwnReferenceInstant(string instant)
    {
        var now = DateTimeOffset.Parse(instant);
        using var database = new TemporaryDatabase();
        // Seeded a week earlier, with a completed review since: the reset must not depend on either.
        using (var db = database.CreateContext()) SeedData.Initialize(db, now.AddDays(-7));
        await CompleteReview(database, Trigonometry, correctCount: 3, at: now.AddDays(-7));

        using (var db = database.CreateContext()) await DemoReset.ResetAsync(db, now);

        AssertScenario(await GetTopics(database, now), now);
    }

    [Fact]
    public async Task DemoReset_AlignsAnEarlierDatabaseWithTheFiveDemoTopics()
    {
        var now = new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);
        using var database = new TemporaryDatabase();
        using (var db = database.CreateContext())
        {
            SeedData.Initialize(db, now);
            // An earlier demo assigned Fonksiyonlar instead of Kimya.
            db.TestResults.RemoveRange(db.TestResults.Where(r => r.TopicId == Atoms));
            db.StudentTopicStates.RemoveRange(db.StudentTopicStates.Where(s => s.TopicId == Atoms));
            db.StudentTopicStates.Add(new StudentTopicState { StudentId = SeedData.DemoStudentId, TopicId = "ayt-mat-fonksiyonlar" });
            db.SaveChanges();
        }

        using (var db = database.CreateContext()) await DemoReset.ResetAsync(db, now);

        AssertScenario(await GetTopics(database, now), now);
        using var check = database.CreateContext();
        // Fonksiyonlar is unassigned, its content stays.
        Assert.Equal((1, 3), (check.Topics.Count(t => t.Id == "ayt-mat-fonksiyonlar"), check.QuizQuestions.Count(q => q.TopicId == "ayt-mat-fonksiyonlar")));
    }

    [Fact]
    public async Task CompletingTheDueTopics_EmptiesTheDueListAndCountsEachTopicOnce()
    {
        var now = new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);
        using var database = new TemporaryDatabase();
        using (var db = database.CreateContext()) SeedData.Initialize(db, now);

        var progress = new List<(int Due, int Weekly)> { Read(await GetTopics(database, now)) };
        foreach (var topicId in new[] { Trigonometry, CellDivision, Motion })
        {
            await CompleteReview(database, topicId, correctCount: 3, at: now);
            progress.Add(Read(await GetTopics(database, now)));
        }

        Assert.Equal(new[] { (3, 0), (2, 1), (1, 2), (0, 3) }, progress);
        var topics = await GetTopics(database, now);
        // The future topics stay scheduled; Sözcükte Anlam (tomorrow) is the nearest one.
        Assert.Equal(
            new[] { WordMeaning, Atoms },
            topics.Topics.Where(t => t.Id is WordMeaning or Atoms).OrderBy(t => t.NextReviewAt).Select(t => t.Id));
        Assert.All(topics.Topics.Where(t => t.Id is WordMeaning or Atoms), t => Assert.Equal(ReviewStatus.Scheduled, t.ReviewStatus));

        static (int, int) Read(TopicsResponse response) => (response.DueTopicIds.Count, response.WeeklyReviewedTopicCount);
    }

    [Fact]
    public async Task WeakReviewOfADueTopic_NeedsSupportInsteadOfStayingDue()
    {
        var now = new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);
        using var database = new TemporaryDatabase();
        using (var db = database.CreateContext()) SeedData.Initialize(db, now);

        await CompleteReview(database, CellDivision, correctCount: 1, at: now);

        var topics = await GetTopics(database, now);
        Assert.Equal(new[] { Trigonometry, Motion }, topics.DueTopicIds);
        Assert.Equal(ReviewStatus.SupportNeeded, topics.Topics.Single(t => t.Id == CellDivision).ReviewStatus);
        Assert.Equal(1, topics.WeeklyReviewedTopicCount);
    }

    private static void AssertScenario(TopicsResponse response, DateTimeOffset now)
    {
        var today = TimeZoneInfo.ConvertTime(now, Istanbul).Date;
        var todayStart = TimeZoneInfo.ConvertTimeToUtc(today, Istanbul);
        DateTimeOffset LocalTen(int daysFromToday) => new(TimeZoneInfo.ConvertTimeToUtc(today.AddDays(daysFromToday).AddHours(10), Istanbul));
        var byId = response.Topics.ToDictionary(topic => topic.Id);

        Assert.Equal(new[] { Trigonometry, CellDivision, WordMeaning, Motion, Atoms }, response.Topics.Select(t => t.Id));
        Assert.Equal(new[] { Trigonometry, CellDivision, Motion }, response.DueTopicIds);
        Assert.Equal(0, response.WeeklyReviewedTopicCount);

        // Due earlier today, at distinct times, in priority order.
        var dueAt = response.DueTopicIds.Select(id => byId[id].NextReviewAt!.Value).ToList();
        Assert.All(dueAt, at => Assert.InRange(at, new DateTimeOffset(todayStart), now));
        Assert.Equal(dueAt.Order(), dueAt);
        Assert.Equal(3, dueAt.Distinct().Count());

        // Tomorrow and three days later at 10:00 Istanbul.
        Assert.Equal(ReviewStatus.Scheduled, byId[WordMeaning].ReviewStatus);
        Assert.Equal(ReviewStatus.Scheduled, byId[Atoms].ReviewStatus);
        Assert.InRange((byId[WordMeaning].NextReviewAt!.Value - LocalTen(1)).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
        Assert.InRange((byId[Atoms].NextReviewAt!.Value - LocalTen(3)).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));

        // Plausible synthetic normal tests: completed before now, scores above the threshold, no reviews yet.
        Assert.All(response.Topics, topic =>
        {
            var test = topic.LatestTest!;
            Assert.Equal(Data.TestType.NormalTest, test.TestType);
            Assert.True(test.CompletedAt < now);
            Assert.True((double)test.CorrectCount / test.TotalQuestions > TestOptions.Default.ReviewThreshold);
            Assert.Null(topic.LastReviewedAt);
        });
    }

    private static async Task<TopicsResponse> GetTopics(TemporaryDatabase database, DateTimeOffset now)
    {
        using var db = database.CreateContext();
        return await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, now, TestOptions.Default);
    }

    private static async Task CompleteReview(TemporaryDatabase database, string topicId, int correctCount, DateTimeOffset at)
    {
        using var db = database.CreateContext();
        var sessions = new ReviewSessionService(db, DemoClock.Create(at.ToString("O"), isDevelopment: true), TestOptions.Default);
        var session = (await sessions.StartOrResumeAsync(topicId)).Value!;
        var answers = CorrectAnswers.GetValueOrDefault(topicId, [0, 0, 0]);
        foreach (var (question, index) in session.Questions.Select((question, index) => (question, index)))
        {
            var selected = index < correctCount ? answers[index] : (answers[index] + 1) % 4;
            await sessions.SubmitAnswerAsync(session.SessionId, new SubmitAnswerRequest(question.Id, selected));
        }
        Assert.Equal(200, (await sessions.CompleteAsync(session.SessionId)).StatusCode);
    }
}
