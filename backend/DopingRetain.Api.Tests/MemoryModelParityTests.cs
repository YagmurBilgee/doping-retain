using DopingRetain.Api.Memory;
using DopingRetain.Api.Tests.Fixtures;

namespace DopingRetain.Api.Tests;

/// <summary>
/// Checks the C# port against outputs recorded from src/utils/memoryModel.ts.
/// Labels and integers must match exactly; stored doubles allow for floating-point noise.
/// </summary>
public class MemoryModelParityTests
{
    private const double Tolerance = 1e-9;

    private static MemoryModelFixture Fixture => MemoryModelFixture.Instance;

    // Cases are passed by name so test output shows which scenario failed.
    public static TheoryData<double> RiskLevelCases => ToTheoryData(Fixture.RiskLevels.Select(c => c.Retention));
    public static TheoryData<string> RetentionCases => ToTheoryData(Fixture.Retention.Select(c => c.Name));
    public static TheoryData<string> TopicRiskCases => ToTheoryData(Fixture.TopicRisks.Select(c => c.Name));
    public static TheoryData<string> ReviewCases => ToTheoryData(Fixture.Reviews.Select(c => c.Name));
    public static TheoryData<string> HighestRiskCases => ToTheoryData(Fixture.HighestRisk.Select(c => c.Name));

    [Theory]
    [MemberData(nameof(RiskLevelCases))]
    public void GetRiskLevel_MatchesTypeScript(double retention)
    {
        var testCase = Fixture.RiskLevels.Single(c => c.Retention == retention);

        Assert.Equal(testCase.Expected, MemoryModel.GetRiskLevel(testCase.Retention));
    }

    [Theory]
    [MemberData(nameof(RetentionCases))]
    public void CalculateRetention_MatchesTypeScriptRounding(string name)
    {
        var testCase = Fixture.Retention.Single(c => c.Name == name);

        Assert.Equal(
            testCase.Expected,
            MemoryModel.CalculateRetention(testCase.DaysAgo, testCase.Strength, testCase.InitialRecall));
    }

    [Theory]
    [MemberData(nameof(TopicRiskCases))]
    public void GetTopicRisk_MatchesTypeScript(string name)
    {
        var testCase = Fixture.TopicRisks.Single(c => c.Name == name);

        Assert.Equal(testCase.Expected, MemoryModel.GetTopicRisk(testCase.Topic.ToState()));
    }

    [Theory]
    [MemberData(nameof(ReviewCases))]
    public void ApplyReviewResult_MatchesTypeScript(string name)
    {
        var testCase = Fixture.Reviews.Single(c => c.Name == name);
        var expected = testCase.Expected;
        var expectedState = expected.UpdatedTopic.ToState();

        var outcome = MemoryModel.ApplyReviewResult(testCase.Topic.ToState(), testCase.Score, testCase.Total);
        var actualState = outcome.UpdatedState;

        Assert.Equal(expected.Review.Tier, outcome.Review.Tier);
        Assert.Equal(expected.Review.NextReviewInDays, outcome.Review.NextReviewInDays);
        Assert.Equal(expected.Review.NewStrength, outcome.Review.NewStrength, Tolerance);

        Assert.Equal(expectedState.ElapsedDays, actualState.ElapsedDays);
        Assert.Equal(expectedState.Strength, actualState.Strength, Tolerance);
        Assert.Equal(expectedState.InitialRecall, actualState.InitialRecall, Tolerance);
        Assert.Equal(expectedState.SuccessRate, actualState.SuccessRate, Tolerance);
        Assert.Equal(expectedState.ReviewCount, actualState.ReviewCount);
        Assert.Equal(expectedState.NeedsFollowUp, actualState.NeedsFollowUp);

        Assert.Equal(expected.RiskAfter, MemoryModel.GetTopicRisk(actualState));
        Assert.Equal(expected.RetentionAtNextReview, outcome.RetentionAtNextReview);
        Assert.Equal(expected.RetentionGain, outcome.RetentionGain);
    }

    [Theory]
    [MemberData(nameof(HighestRiskCases))]
    public void FindHighestRiskTopic_MatchesTypeScript(string name)
    {
        var testCase = Fixture.HighestRisk.Single(c => c.Name == name);

        var actual = MemoryModel.FindHighestRiskTopic(testCase.Topics, topic => topic.ToState());

        Assert.Equal(testCase.ExpectedId, actual?.Id);
        Assert.Equal(testCase.ExpectedHighRiskCount, MemoryModel.CountHighRiskTopics(testCase.Topics.Select(topic => topic.ToState())));
    }

    [Fact]
    public void Fixture_CoversRequiredScenarios()
    {
        Assert.Equal(
            new[] { 69.7, 49.7, 70, 50 },
            Fixture.RiskLevels.Select(c => c.Retention).Take(4));
        Assert.Contains(Fixture.Retention, c => c.Name == "midpoint 12.5");
        Assert.Equal(
            new[] { "Trigonometri 0/3", "Trigonometri 1/3", "Trigonometri 2/3", "Trigonometri 3/3" },
            Fixture.Reviews.Select(c => c.Name).Where(name => name.StartsWith("Trigonometri")));
        Assert.Equal(
            new[] { "first test 0%", "first test 30%", "first test 69%", "first test 70%", "first test 80%" },
            Fixture.TopicRisks.Select(c => c.Name).Where(name => name.StartsWith("first test")));
        Assert.Contains(Fixture.HighestRisk, c => c.Name == "reinforcement beside forgetting");
    }

    private static TheoryData<T> ToTheoryData<T>(IEnumerable<T> values)
    {
        var data = new TheoryData<T>();
        foreach (var value in values) data.Add(value);
        return data;
    }
}
