using System.Text.Json;
using System.Text.Json.Serialization;
using DopingRetain.Api.Memory;

namespace DopingRetain.Api.Tests.Fixtures;

/// <summary>Expected outputs recorded from the TypeScript model by generate-memory-model-fixtures.ts.</summary>
public sealed record MemoryModelFixture(
    IReadOnlyList<RiskLevelCase> RiskLevels,
    IReadOnlyList<RetentionCase> Retention,
    IReadOnlyList<TopicRiskCase> TopicRisks,
    IReadOnlyList<ReviewCase> Reviews,
    IReadOnlyList<HighestRiskCase> HighestRisk)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static MemoryModelFixture Instance { get; } = Load();

    private static MemoryModelFixture Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "memory-model-parity.json");
        return JsonSerializer.Deserialize<MemoryModelFixture>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException($"Could not read {path}");
    }
}

/// <summary>A topic's memory fields as the TypeScript Topic type stores them.</summary>
public sealed record FixtureTopic(
    string Id,
    double LastStudiedDaysAgo,
    double CurrentStrength,
    double? InitialRecall,
    int ReviewCount,
    double PreviousSuccessRate,
    bool? NeedsFollowUp)
{
    // Mirrors the TS defaults for optional fields: initialRecall = 1, needsFollowUp = false.
    public TopicMemoryState ToState() => new(
        LastStudiedDaysAgo,
        CurrentStrength,
        InitialRecall ?? 1,
        ReviewCount,
        PreviousSuccessRate,
        NeedsFollowUp ?? false);
}

public sealed record RiskLevelCase(double Retention, RiskLevel Expected);

public sealed record RetentionCase(string Name, double DaysAgo, double Strength, double InitialRecall, int Expected);

public sealed record TopicRiskCase(string Name, FixtureTopic Topic, TopicRisk Expected);

public sealed record ReviewCase(string Name, FixtureTopic Topic, int Score, int Total, ExpectedReview Expected);

public sealed record ExpectedReview(
    ReviewResult Review,
    FixtureTopic UpdatedTopic,
    TopicRisk RiskAfter,
    int RetentionAtNextReview,
    int RetentionGain);

public sealed record HighestRiskCase(string Name, IReadOnlyList<FixtureTopic> Topics, string? ExpectedId, int ExpectedHighRiskCount);
