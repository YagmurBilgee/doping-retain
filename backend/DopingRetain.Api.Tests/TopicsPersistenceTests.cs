using System.Text.Json;
using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Topics;

namespace DopingRetain.Api.Tests;

/// <summary>Runs seeding and GET /api/topics logic against a throwaway SQLite file per test.</summary>
public sealed class TopicsPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private readonly TemporaryDatabase _database = new();

    [Fact]
    public async Task SeededTopics_ShowInitialMockPercentagesInDisplayOrder()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);

        using var context = CreateContext();
        var response = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt);

        Assert.Equal(
            new[] { "ayt-mat-trigonometri", "tyt-bio-hucre-bolunmeleri", "tyt-tur-sozcukte-anlam", "ayt-mat-fonksiyonlar" },
            response.Topics.Select(topic => topic.Id));
        Assert.Equal(new[] { 46, 55, 86, 51 }, response.Topics.Select(topic => topic.Retention));
        Assert.Equal(
            new[] { RiskLevel.HighRisk, RiskLevel.ReviewSoon, RiskLevel.Safe, RiskLevel.ReviewSoon },
            response.Topics.Select(topic => topic.RiskLevel));
        Assert.Equal("ayt-mat-trigonometri", response.HighestRiskTopicId);
        Assert.Equal(1, response.HighRiskCount);
        Assert.Equal(SeededAt, response.EvaluatedAt);
        Assert.Equal(SeededAt.AddDays(-11), response.Topics[0].LastStudiedAt);
        Assert.Equal(TimeSpan.Zero, response.Topics[0].LastStudiedAt.Offset);
    }

    [Fact]
    public async Task Topics_UseFractionalElapsedDays()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);

        using var context = CreateContext();
        var response = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt.AddHours(12));

        // Trigonometri was studied 11 days before seeding, so 11.5 days have passed.
        Assert.Equal(MemoryModel.CalculateRetention(11.5, 14), response.Topics[0].Retention);
    }

    [Fact]
    public void Initialize_OnExistingDatabase_KeepsRowsAndDates()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);
        var originalDates = ReadLastStudiedDates();

        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt.AddDays(3));

        using var context = CreateContext();
        Assert.Equal(1, context.Students.Count());
        Assert.Equal(4, context.Topics.Count());
        Assert.Equal(3, context.QuizQuestions.Count());
        Assert.Equal(4, context.StudentTopicStates.Count());
        Assert.Equal(originalDates, ReadLastStudiedDates());
    }

    [Fact]
    public async Task TopicDetail_MatchesListEntryAndCarriesMicroReviewContentOnly()
    {
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);

        using var context = CreateContext();
        var list = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt);
        var detail = (await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "ayt-mat-trigonometri", SeededAt))!;

        var summary = list.Topics[0];
        Assert.Equal(
            (summary.Id, summary.Retention, summary.RiskLevel, summary.IsFollowUp, summary.LastStudiedAt, summary.ReviewCount, summary.SuccessRate),
            (detail.Id, detail.Retention, detail.RiskLevel, detail.IsFollowUp, detail.LastStudiedAt, detail.ReviewCount, detail.SuccessRate));
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
        using (var db = CreateContext()) SeedData.Initialize(db, SeededAt);

        using var context = CreateContext();
        var withoutQuiz = (await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "tyt-bio-hucre-bolunmeleri", SeededAt))!;

        Assert.Equal((55, 0), (withoutQuiz.Retention, withoutQuiz.QuestionCount));
        Assert.Empty(withoutQuiz.SummaryPoints);
        Assert.Null(await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "unknown-topic", SeededAt));
    }

    [Fact]
    public async Task InitialReinforcementTopics_DoNotDisplaceForgettingOrInflateHighRiskCount()
    {
        using (var db = CreateContext())
        {
            SeedData.Initialize(db, SeededAt);
            AddFirstTestTopic(db, "first-test-0", correctCount: 0);
            AddFirstTestTopic(db, "first-test-30", correctCount: 3);
            AddFirstTestTopic(db, "first-test-80", correctCount: 8);
            db.SaveChanges();
        }

        using var context = CreateContext();
        var response = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, SeededAt);
        var byId = response.Topics.ToDictionary(topic => topic.Id);

        // Demo topics keep their values and the forgetting card keeps its topic and count.
        Assert.Equal(
            new[] { 46, 55, 86, 51 },
            new[] { "ayt-mat-trigonometri", "tyt-bio-hucre-bolunmeleri", "tyt-tur-sozcukte-anlam", "ayt-mat-fonksiyonlar" }
                .Select(id => byId[id].Retention));
        Assert.All(response.Topics.Where(topic => !topic.Id.StartsWith("first-test")), topic => Assert.False(topic.NeedsInitialReinforcement));
        Assert.Equal("ayt-mat-trigonometri", response.HighestRiskTopicId);
        Assert.Equal(1, response.HighRiskCount);

        // Retention and risk level stay numerical; only the flag differs.
        Assert.Equal((0, RiskLevel.HighRisk, true), (byId["first-test-0"].Retention, byId["first-test-0"].RiskLevel, byId["first-test-0"].NeedsInitialReinforcement));
        Assert.Equal((30, RiskLevel.HighRisk, true), (byId["first-test-30"].Retention, byId["first-test-30"].RiskLevel, byId["first-test-30"].NeedsInitialReinforcement));
        Assert.Equal((80, RiskLevel.Safe, false), (byId["first-test-80"].Retention, byId["first-test-80"].RiskLevel, byId["first-test-80"].NeedsInitialReinforcement));

        var reinforcement = (await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "first-test-30", SeededAt))!;
        var notReinforcement = (await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "first-test-80", SeededAt))!;
        var demo = (await TopicsQuery.GetTopicAsync(context, SeedData.DemoStudentId, "ayt-mat-trigonometri", SeededAt))!;
        Assert.Equal((true, 30), (reinforcement.NeedsInitialReinforcement, reinforcement.InitialTestAccuracy));
        Assert.Equal((false, (int?)null), (notReinforcement.NeedsInitialReinforcement, notReinforcement.InitialTestAccuracy));
        Assert.Equal((false, (int?)null), (demo.NeedsInitialReinforcement, demo.InitialTestAccuracy));
    }

    public void Dispose() => _database.Dispose();

    private static void AddFirstTestTopic(AppDbContext db, string topicId, int correctCount)
    {
        db.Topics.Add(new Topic { Id = topicId, Title = topicId, Category = "Test", SortOrder = 100 });
        db.StudentTopicStates.Add(InitialMemoryState.FromFirstAttemptTest(
            new FirstAttemptTestResult(SeedData.DemoStudentId, $"test-{topicId}", topicId, SeededAt, correctCount, 10)));
    }

    private AppDbContext CreateContext() => _database.CreateContext();

    private Dictionary<string, DateTimeOffset> ReadLastStudiedDates()
    {
        using var db = CreateContext();
        return db.StudentTopicStates.ToDictionary(state => state.TopicId, state => state.LastStudiedAt);
    }
}
