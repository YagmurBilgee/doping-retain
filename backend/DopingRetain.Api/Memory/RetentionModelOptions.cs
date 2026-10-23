namespace DopingRetain.Api.Memory;

/// <summary>
/// Shared settings of the retention model, bound from the "RetentionModel" configuration section.
/// Both values are provisional product/demo assumptions, not scientific constants: they are the same
/// for every student and topic, and nothing learns or personalizes them.
/// </summary>
public sealed class RetentionModelOptions
{
    public const string SectionName = "RetentionModel";

    /// <summary>Days after a test until the estimated recall halves (h).</summary>
    public double HalfLifeDays { get; init; } = 10;

    /// <summary>Recall (0–1) at which a review becomes due.</summary>
    public double ReviewThreshold { get; init; } = 0.50;

    /// <summary>Fails clearly on settings that would make the curve or the schedule meaningless.</summary>
    public RetentionModelOptions Validated()
    {
        if (!double.IsFinite(HalfLifeDays) || HalfLifeDays <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(HalfLifeDays)} must be greater than 0, was {HalfLifeDays}.");
        }
        if (!double.IsFinite(ReviewThreshold) || ReviewThreshold <= 0 || ReviewThreshold >= 1)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(ReviewThreshold)} must be between 0 and 1 (exclusive), was {ReviewThreshold}.");
        }
        return this;
    }
}
