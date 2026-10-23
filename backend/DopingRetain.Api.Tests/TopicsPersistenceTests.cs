using System.Text.Json;
using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using DopingRetain.Api.TestResults;
using DopingRetain.Api.Topics;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Tests;

/// <summary>Runs seeding and GET /api/topics logic against a throwaway SQLite file per test.</summary>
public sealed class TopicsPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private readonly TemporaryDatabase _database = new();

    [Fact]
    public async Task SeededTopics_AreEvaluatedFromTheirNormalTestsInDisplayOrder()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);

        using var context = CreateContext();
        var response = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt, TestOptions.Default);

        // Five subjects; Fonksiyonlar stays content only and is not assigned to the demo student.
        Assert.Equal(
            new[] { "ayt-mat-trigonometri", "tyt-bio-hucre-bolunmeleri", "tyt-tur-sozcukte-anlam", "tyt-fiz-hareket-ve-kuvvet", "tyt-kim-atom-ve-periyodik-sistem" },
            response.Topics.Select(topic => topic.Id));
        Assert.Equal(
            new[] { ReviewStatus.Due, ReviewStatus.Due, ReviewStatus.Scheduled, ReviewStatus.Due, ReviewStatus.Scheduled },
            response.Topics.Select(topic => topic.ReviewStatus));
        // Observed scores are whole counts (90 / 80 / 95 / 73 / 88 %); recall estimates are never reported as scores.
        Assert.Equal(new[] { "18/20", "16/20", "19/20", "11/15", "22/25" },
            response.Topics.Select(topic => $"{topic.LatestTest!.CorrectCount}/{topic.LatestTest.TotalQuestions}"));
        Assert.Equal(new[] { "ayt-mat-trigonometri", "tyt-bio-hucre-bolunmeleri", "tyt-fiz-hareket-ve-kuvvet" }, response.DueTopicIds);
        Assert.Equal((SeededAt, 0), (response.EvaluatedAt, response.WeeklyReviewedTopicCount));
        Assert.All(response.Topics, topic => Assert.Equal(TimeSpan.Zero, topic.LatestTest!.CompletedAt.Offset));
        // Seeds contain normal tests only, so no review of our feature has been completed yet.
        Assert.All(response.Topics, topic => Assert.Null(topic.LastReviewedAt));

        // Istanbul 12:30 on 21 October; today began at 2026-10-20T21:00Z. Due topics crossed at 1/4, 1/2 and 3/4
        // of today so far; the others cross tomorrow and three days later at 10:00 Istanbul (07:00Z).
        var byId = response.Topics.ToDictionary(topic => topic.Id);
        AssertAt(new DateTimeOffset(2026, 10, 21, 0, 7, 30, TimeSpan.Zero), byId["ayt-mat-trigonometri"].NextReviewAt);
        AssertAt(new DateTimeOffset(2026, 10, 21, 3, 15, 0, TimeSpan.Zero), byId["tyt-bio-hucre-bolunmeleri"].NextReviewAt);
        AssertAt(new DateTimeOffset(2026, 10, 21, 6, 22, 30, TimeSpan.Zero), byId["tyt-fiz-hareket-ve-kuvvet"].NextReviewAt);
        AssertAt(new DateTimeOffset(2026, 10, 22, 7, 0, 0, TimeSpan.Zero), byId["tyt-tur-sozcukte-anlam"].NextReviewAt);
        AssertAt(new DateTimeOffset(2026, 10, 24, 7, 0, 0, TimeSpan.Zero), byId["tyt-kim-atom-ve-periyodik-sistem"].NextReviewAt);
        // Each completion is the crossing minus h · log2(A / 0.5), e.g. 10 · log2(0.9 / 0.5) ≈ 8.48 days for 18/20.
        AssertAt(new DateTimeOffset(2026, 10, 21, 0, 7, 30, TimeSpan.Zero).AddDays(-10 * Math.Log2(0.9 / 0.5)),
            byId["ayt-mat-trigonometri"].LatestTest!.CompletedAt);

        static void AssertAt(DateTimeOffset expected, DateTimeOffset? actual) =>
            Assert.InRange((actual!.Value - expected).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Topics_UseFractionalElapsedDays()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);

        using var context = CreateContext();
        // Sözcükte Anlam crosses the threshold tomorrow at 10:00 Istanbul (2026-10-22T07:00Z), 21.5 h after seeding.
        var at21Hours = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt.AddHours(21), TestOptions.Default);
        var at22Hours = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt.AddHours(22), TestOptions.Default);

        Assert.Equal(ReviewStatus.Scheduled, at21Hours.Topics.Single(t => t.Id == "tyt-tur-sozcukte-anlam").ReviewStatus);
        Assert.Equal(ReviewStatus.Due, at22Hours.Topics.Single(t => t.Id == "tyt-tur-sozcukte-anlam").ReviewStatus);
    }

    [Fact]
    public void Initialize_OnExistingDatabase_KeepsRowsAndDates()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);
        var originalDates = ReadTestDates();

        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt.AddDays(3));

        using var context = CreateContext();
        Assert.Equal(1, context.Students.Count());
        Assert.Equal(6, context.Topics.Count());
        Assert.Equal(18, context.QuizQuestions.Count());
        Assert.Equal(5, context.StudentTopicStates.Count());
        Assert.Equal(5, context.TestResults.Count());
        Assert.Equal(originalDates, ReadTestDates());
    }

    [Fact]
    public async Task TopicDetail_MatchesListEntryAndCarriesMicroReviewContentOnly()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);

        using var context = CreateContext();
        var list = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt, TestOptions.Default);
        var detail = (await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "ayt-mat-trigonometri", SeededAt, TestOptions.Default))!;

        var summary = list.Topics[0];
        Assert.Equal(
            (summary.Id, summary.ReviewStatus, summary.LatestTest, summary.NextReviewAt, summary.LastReviewedAt),
            (detail.Id, detail.ReviewStatus, detail.LatestTest, detail.NextReviewAt, detail.LastReviewedAt));
        Assert.Equal(3, detail.QuestionCount);
        Assert.Equal(new[] { "Temel Özdeşlik", "Oran Kuralı", "Sık Karıştırılan" }, detail.SummaryPoints.Select(point => point.Tag));
        Assert.Equal(new SummaryPoint("📌", "Temel Özdeşlik", "sin²x + cos²x = 1", "Kareler toplamı daima 1’dir."), detail.SummaryPoints[0]);

        var json = JsonSerializer.Serialize(detail, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("correctIndex", json);
        Assert.DoesNotContain("explanation", json);
        Assert.DoesNotContain("options", json);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"note\""));
    }

    [Fact]
    public async Task TopicDetail_WithoutContentOrUnknown()
    {
        using (var db = CreateContext())
        {
            SeedData.Initialize(db, SeededAt);
            AddTopic(db, "no-content", correctCount: 8);
            db.SaveChanges();
        }

        using var context = CreateContext();
        var withoutQuiz = (await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "no-content", SeededAt, TestOptions.Default))!;

        Assert.Equal((ReviewStatus.Scheduled, 0), (withoutQuiz.ReviewStatus, withoutQuiz.QuestionCount));
        Assert.Empty(withoutQuiz.SummaryPoints);
        Assert.Null(await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "unknown-topic", SeededAt, TestOptions.Default));
    }

    [Fact]
    public void EveryDemoTopic_HasAReminderAndThreeValidQuestions()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);

        using var context = CreateContext();
        foreach (var topic in context.Topics.Include(t => t.Questions).ToList())
        {
            Assert.Equal(3, SeedData.GetSummaryPoints(topic.Id).Count);
            Assert.Equal(new[] { 1, 2, 3 }, topic.Questions.OrderBy(q => q.SortOrder).Select(q => q.SortOrder));
            Assert.All(topic.Questions, question =>
            {
                Assert.Equal(4, question.Options.Count);
                Assert.Equal(4, question.Options.Distinct().Count());
                Assert.InRange(question.CorrectIndex, 0, 3);
                Assert.False(string.IsNullOrWhiteSpace(question.Explanation));
            });
        }
    }

    [Fact]
    public void Initialize_OnEarlierDatabase_AddsOnlyMissingContentOnce()
    {
        string[] newTopics = ["tyt-fiz-hareket-ve-kuvvet", "tyt-kim-atom-ve-periyodik-sistem"];
        using (var db = CreateContext())
        {
            SeedData.Initialize(db, SeededAt);
            // An earlier database: no Fizik or Kimya topic, only Trigonometri had questions, and results exist.
            db.TestResults.RemoveRange(db.TestResults.Where(r => newTopics.Contains(r.TopicId)));
            db.StudentTopicStates.RemoveRange(db.StudentTopicStates.Where(s => newTopics.Contains(s.TopicId)));
            db.QuizQuestions.RemoveRange(db.QuizQuestions.Where(q => q.TopicId != "ayt-mat-trigonometri"));
            db.Topics.RemoveRange(db.Topics.Where(t => newTopics.Contains(t.Id)));
            db.QuizQuestions.Single(q => q.Id == "trig-q1").Text = "Edited earlier";
            db.SaveChanges();
        }
        var resultsBefore = ReadTestDates();

        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt.AddDays(2));
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt.AddDays(3));

        using var context = CreateContext();
        Assert.Equal((6, 18), (context.Topics.Count(), context.QuizQuestions.Count()));
        Assert.All(context.Topics.Include(t => t.Questions).ToList(), topic => Assert.Equal(3, topic.Questions.Count));
        // Only content is added: existing content, results and the student's topics are left as they were.
        Assert.Equal("Edited earlier", context.QuizQuestions.Single(q => q.Id == "trig-q1").Text);
        Assert.Equal(resultsBefore, ReadTestDates());
        Assert.Equal((3, 3), (context.TestResults.Count(), context.StudentTopicStates.Count()));
    }

    [Fact]
    public async Task SupportAndMissingData_DoNotDisplaceDueTopicOrInflateDueCount()
    {
        using (var db = CreateContext())
        {
            SeedData.Initialize(db, SeededAt);
            AddTopic(db, "score-0", correctCount: 0);
            AddTopic(db, "score-30", correctCount: 3);
            AddTopic(db, "score-50", correctCount: 5);
            AddTopic(db, "score-80", correctCount: 8);
            AddTopic(db, "no-test", correctCount: null);
            db.SaveChanges();
        }

        using var context = CreateContext();
        var response = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt, TestOptions.Default);
        var byId = response.Topics.ToDictionary(topic => topic.Id);

        Assert.Equal(new[] { "ayt-mat-trigonometri", "tyt-bio-hucre-bolunmeleri", "tyt-fiz-hareket-ve-kuvvet" }, response.DueTopicIds);

        // A score at or below the threshold needs support now; it is never a negative or future interval.
        Assert.Equal(ReviewStatus.SupportNeeded, byId["score-0"].ReviewStatus);
        Assert.Equal(ReviewStatus.SupportNeeded, byId["score-30"].ReviewStatus);
        Assert.Equal(ReviewStatus.SupportNeeded, byId["score-50"].ReviewStatus);
        Assert.Equal(SeededAt, byId["score-50"].NextReviewAt);
        Assert.Equal(ReviewStatus.Scheduled, byId["score-80"].ReviewStatus);
        Assert.Equal(ReviewStatus.InsufficientData, byId["no-test"].ReviewStatus);
        Assert.Equal(((TestResultDto?)null, (DateTimeOffset?)null), (byId["no-test"].LatestTest, byId["no-test"].NextReviewAt));
    }

    public void Dispose() => _database.Dispose();

    /// <summary>Adds a topic with a normal test of <paramref name="correctCount"/>/10 at seeding time, or no test.</summary>
    private static void AddTopic(AppDbContext db, string topicId, int? correctCount)
    {
        db.Topics.Add(new Topic { Id = topicId, Title = topicId, Category = "Test", SortOrder = 100 });
        db.StudentTopicStates.Add(new StudentTopicState { StudentId = SeedData.DemoStudentId, TopicId = topicId });
        if (correctCount is { } correct)
        {
            db.TestResults.Add(new TestResult
            {
                StudentId = SeedData.DemoStudentId,
                TopicId = topicId,
                TestType = TestType.NormalTest,
                CompletedAt = SeededAt,
                CorrectCount = correct,
                TotalQuestions = 10,
            });
        }
    }

    private AppDbContext CreateContext() => _database.CreateContext();

    private Dictionary<string, DateTimeOffset> ReadTestDates()
    {
        using var db = CreateContext();
        return db.TestResults.ToDictionary(result => result.TopicId, result => result.CompletedAt);
    }
}
