using System.Globalization;

namespace DopingRetain.Api.Memory;

/// <summary>
/// Review scheduling policy, bound from the "ReviewSchedule" configuration section. This is a product rule
/// applied on top of the retention curve, not a learned half-life or a scientific conclusion: it does not
/// change h, the threshold or the estimated recall, only when the next review is recommended.
/// </summary>
public sealed class ReviewScheduleOptions
{
    public const string SectionName = "ReviewSchedule";

    /// <summary>
    /// Days from completion to the next review after a perfect reminder mini quiz that follows another perfect
    /// one and was completed at or after its recommended time. The interval stays fixed; it never grows further.
    /// </summary>
    public double ExtendedIntervalDays { get; init; } = 30;

    /// <summary>
    /// Fails clearly on a setting that would make the extended schedule meaningless: the extension may never be
    /// shorter than the formula's interval for a perfect score, h · log2(1 / threshold) with the given settings.
    /// </summary>
    public ReviewScheduleOptions Validated(RetentionModelOptions retention)
    {
        var minimumDays = RetentionModel.DaysUntilThreshold(1, retention)!.Value;
        if (!double.IsFinite(ExtendedIntervalDays) || ExtendedIntervalDays < minimumDays)
        {
            // Invariant formatting, so the message reads "0.5" whatever the machine's culture is.
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"{SectionName}:{nameof(ExtendedIntervalDays)} must be at least {minimumDays:0.###} days, the formula's " +
                $"interval for a perfect score with {RetentionModelOptions.SectionName}:{nameof(RetentionModelOptions.HalfLifeDays)} " +
                $"{retention.HalfLifeDays} and {nameof(RetentionModelOptions.ReviewThreshold)} {retention.ReviewThreshold}; " +
                $"was {ExtendedIntervalDays}."));
        }
        return this;
    }
}
