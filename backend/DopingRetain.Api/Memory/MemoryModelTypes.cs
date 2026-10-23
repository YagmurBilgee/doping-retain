namespace DopingRetain.Api.Memory;

public enum RiskLevel
{
    Safe,
    ReviewSoon,
    HighRisk,
}

/// <summary>Score band of a micro-review: full score, at least two thirds, or below.</summary>
public enum ReviewTier
{
    Perfect,
    Partial,
    Weak,
}

/// <summary>
/// Memory state of one topic for one student. Elapsed time is supplied by the caller;
/// the model never reads the system clock.
/// </summary>
/// <param name="ElapsedDays">Days since the last study session (t).</param>
/// <param name="Strength">Memory stability S in days.</param>
/// <param name="InitialRecall">Recall R0 (0–1) right after the last study session.</param>
/// <param name="ReviewCount">Number of completed reviews.</param>
/// <param name="SuccessRate">0–1 running average of review score ratios.</param>
/// <param name="NeedsFollowUp">Set after a weak review: keeps the topic out of the safe band.</param>
public sealed record TopicMemoryState(
    double ElapsedDays,
    double Strength,
    double InitialRecall,
    int ReviewCount,
    double SuccessRate,
    bool NeedsFollowUp);

/// <param name="Retention">Display retention percentage (0–100), rounded like JavaScript's Math.round.</param>
/// <param name="RiskLevel">Classified from the unrounded retention.</param>
/// <param name="IsFollowUp">True when a weak last review holds the topic in review_soon.</param>
/// <param name="NeedsInitialReinforcement">
/// No completed micro-review and a first-test R0 below 0.70: low initial mastery, not forgetting.
/// </param>
public sealed record TopicRisk(int Retention, RiskLevel RiskLevel, bool IsFollowUp, bool NeedsInitialReinforcement);

public sealed record ReviewResult(double NewStrength, int NextReviewInDays, ReviewTier Tier);

/// <param name="UpdatedState">State right after the review (t = 0).</param>
/// <param name="RetentionAtNextReview">Predicted display retention on the next scheduled review day.</param>
/// <param name="RetentionGain">Display retention points gained by the review (after minus before).</param>
public sealed record ReviewOutcome(
    TopicMemoryState UpdatedState,
    ReviewResult Review,
    int RetentionAtNextReview,
    int RetentionGain);
