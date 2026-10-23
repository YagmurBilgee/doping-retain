namespace DopingRetain.Api.Memory;

/*
 * Port of src/utils/memoryModel.ts; that file remains the behavioral reference.
 * Rule-based prototype built on the Ebbinghaus forgetting curve:
 *   Retention = R0 · e^(-t / S)
 *   t  = days since last study, S = memory stability in days,
 *   R0 = initial recall right after that study session.
 *
 * Thresholds, stability multipliers, recall targets and review intervals are
 * hand-picked prototype rules, not learning parameters validated on student data.
 */
public static class MemoryModel
{
    public const double SafeThreshold = 70;
    public const double HighRiskThreshold = 50;

    /// <summary>
    /// Product rule: a topic with no completed micro-review whose R0 (its first-test accuracy) is below
    /// this value needs initial reinforcement. Its low retention reflects low initial mastery, not forgetting.
    /// </summary>
    public const double InitialReinforcementRecall = 0.7;

    private sealed record ReviewRule(double StrengthMultiplier, int NextReviewInDays, double PostReviewRecall);

    private static readonly IReadOnlyDictionary<ReviewTier, ReviewRule> ReviewRules = new Dictionary<ReviewTier, ReviewRule>
    {
        [ReviewTier.Perfect] = new(StrengthMultiplier: 2, NextReviewInDays: 14, PostReviewRecall: 1),
        [ReviewTier.Partial] = new(StrengthMultiplier: 1.4, NextReviewInDays: 7, PostReviewRecall: 0.8),
        [ReviewTier.Weak] = new(StrengthMultiplier: 1.05, NextReviewInDays: 2, PostReviewRecall: 0.58),
    };

    /// <summary>A 0–1 ratio as an integer percentage, rounded like the retention display.</summary>
    public static int ToDisplayPercentage(double ratio) => (int)RoundLikeJavaScript(Math.Min(1, Math.Max(0, ratio)) * 100);

    /// <summary>Retention as an integer percentage (0–100), for display only.</summary>
    public static int CalculateRetention(double elapsedDays, double strength, double initialRecall = 1) =>
        (int)RoundLikeJavaScript(CalculateRawRetention(elapsedDays, strength, initialRecall));

    /// <summary>Classifies an unrounded retention percentage; pass raw values, not display values.</summary>
    public static RiskLevel GetRiskLevel(double rawRetention)
    {
        if (rawRetention >= SafeThreshold) return RiskLevel.Safe;
        if (rawRetention >= HighRiskThreshold) return RiskLevel.ReviewSoon;
        return RiskLevel.HighRisk;
    }

    /// <summary>
    /// A topic flagged for follow-up is never reported as safe: it is held in review_soon
    /// until a stronger review clears the flag, and only falls to high_risk if the curve does.
    /// Retention and risk level are unchanged for topics that need initial reinforcement; the flag only
    /// keeps them out of the forgetting signals (highest-risk topic, high-risk count).
    /// </summary>
    public static TopicRisk GetTopicRisk(TopicMemoryState state)
    {
        var rawRetention = CalculateRawRetention(state);
        var curveLevel = GetRiskLevel(rawRetention);
        var isFollowUp = state.NeedsFollowUp && curveLevel != RiskLevel.HighRisk;

        return new TopicRisk(
            (int)RoundLikeJavaScript(rawRetention),
            isFollowUp ? RiskLevel.ReviewSoon : curveLevel,
            isFollowUp,
            NeedsInitialReinforcement(state));
    }

    /// <summary>Updates stability from the score ratio, so the rules apply to any quiz length.</summary>
    public static ReviewResult CalculateNextReview(int quizScore, int totalQuestions, double currentStrength)
    {
        var ratio = GetScoreRatio(quizScore, totalQuestions);
        var tier = ratio == 1 ? ReviewTier.Perfect : ratio >= 2.0 / 3 ? ReviewTier.Partial : ReviewTier.Weak;
        var rule = ReviewRules[tier];

        return new ReviewResult(
            RoundLikeJavaScript(currentStrength * rule.StrengthMultiplier * 10) / 10,
            rule.NextReviewInDays,
            tier);
    }

    /// <summary>
    /// Applies a finished micro-review: stability grows per the review rules, the topic counts as
    /// studied now (t = 0) with an initial recall set by the score, the success rate becomes a
    /// running average, and a weak score flags the topic for follow-up.
    /// </summary>
    public static ReviewOutcome ApplyReviewResult(TopicMemoryState state, int quizScore, int totalQuestions)
    {
        var review = CalculateNextReview(quizScore, totalQuestions, state.Strength);
        var ratio = GetScoreRatio(quizScore, totalQuestions);
        var reviewCount = state.ReviewCount + 1;
        var successRate = RoundLikeJavaScript((state.SuccessRate * state.ReviewCount + ratio) / reviewCount * 100) / 100;

        // A review never leaves the student worse off than before it.
        var initialRecall = Math.Max(ReviewRules[review.Tier].PostReviewRecall, CalculateRawRetention(state) / 100);

        var updatedState = state with
        {
            ElapsedDays = 0,
            Strength = review.NewStrength,
            InitialRecall = initialRecall,
            ReviewCount = reviewCount,
            SuccessRate = successRate,
            NeedsFollowUp = review.Tier == ReviewTier.Weak,
        };

        return new ReviewOutcome(
            updatedState,
            review,
            CalculateRetention(review.NextReviewInDays, review.NewStrength, initialRecall),
            GetTopicRisk(updatedState).Retention - GetTopicRisk(state).Retention);
    }

    /// <summary>The forgetting high_risk topic with the lowest unrounded retention; ties keep input order.</summary>
    public static T? FindHighestRiskTopic<T>(IEnumerable<T> topics, Func<T, TopicMemoryState> stateOf) where T : class =>
        topics
            .Select(topic => (Topic: topic, State: stateOf(topic)))
            .Where(item => IsForgettingHighRisk(GetTopicRisk(item.State)))
            .OrderBy(item => CalculateRawRetention(item.State))
            .Select(item => item.Topic)
            .FirstOrDefault();

    public static int CountHighRiskTopics(IEnumerable<TopicMemoryState> states) =>
        states.Count(state => IsForgettingHighRisk(GetTopicRisk(state)));

    private static bool NeedsInitialReinforcement(TopicMemoryState state) =>
        state.ReviewCount == 0 && state.InitialRecall < InitialReinforcementRecall;

    /// <summary>A high_risk topic that is being forgotten; topics needing initial reinforcement are not.</summary>
    private static bool IsForgettingHighRisk(TopicRisk risk) =>
        risk.RiskLevel == RiskLevel.HighRisk && !risk.NeedsInitialReinforcement;

    /// <summary>Unrounded retention percentage (0–100); every business decision uses this value.</summary>
    private static double CalculateRawRetention(double elapsedDays, double strength, double initialRecall)
    {
        if (strength <= 0) return 0;

        var t = Math.Max(0, elapsedDays);
        var retention = initialRecall * Math.Exp(-t / strength) * 100;

        return Math.Min(100, Math.Max(0, retention));
    }

    private static double CalculateRawRetention(TopicMemoryState state) =>
        CalculateRawRetention(state.ElapsedDays, state.Strength, state.InitialRecall);

    private static double GetScoreRatio(int quizScore, int totalQuestions) =>
        totalQuestions > 0 ? Math.Min(1, Math.Max(0, (double)quizScore / totalQuestions)) : 0;

    /// <summary>
    /// JavaScript Math.round: halves round toward +∞ (12.5 → 13). C#'s Math.Round defaults to
    /// banker's rounding (12.5 → 12), so it would break display parity with the frontend.
    /// Subtracting the floor is exact for the non-negative values this model rounds.
    /// </summary>
    private static double RoundLikeJavaScript(double value)
    {
        var floor = Math.Floor(value);
        return value - floor >= 0.5 ? floor + 1 : floor;
    }
}
