namespace DopingRetain.Api.Memory;

/// <summary>One completed test: when it ended and its actual score. Counts are validated on creation.</summary>
public sealed record TestObservation
{
    public TestObservation(DateTimeOffset completedAt, int correctCount, int totalQuestions)
    {
        RetentionModel.ValidateScore(correctCount, totalQuestions);
        CompletedAt = completedAt;
        CorrectCount = correctCount;
        TotalQuestions = totalQuestions;
    }

    public DateTimeOffset CompletedAt { get; }
    public int CorrectCount { get; }
    public int TotalQuestions { get; }

    /// <summary>A = correct / total, unrounded (0–1).</summary>
    public double Accuracy => (double)CorrectCount / TotalQuestions;
}

public enum ReviewStatus
{
    /// <summary>No completed test for the topic: nothing to estimate from.</summary>
    InsufficientData,
    /// <summary>Estimated recall is above the threshold; the review time lies ahead.</summary>
    Scheduled,
    /// <summary>The curve has crossed the threshold since the test: review now.</summary>
    Due,
    /// <summary>The test score itself is at or below the threshold: support/review now, not forgetting.</summary>
    SupportNeeded,
}

/// <param name="Recall">Estimated recall (0–1) at the evaluation instant; null without a test.</param>
/// <param name="ReviewAt">When recall reaches the threshold; the test's own completion for SupportNeeded.</param>
public sealed record RetentionEstimate(
    ReviewStatus Status,
    TestObservation? Observation,
    double? Recall,
    DateTimeOffset? ReviewAt);

/*
 * Estimated recall of a topic from its latest relevant completed test:
 *   R(t) = A · 2^(−t / h)
 *   A = correct / total of that test, t = days since it was completed, h = shared half-life (configuration).
 * The review time is where the same curve reaches the threshold:  t* = h · log2(A / threshold).
 * Pure: no clock, no database; every value is unrounded on a 0–1 scale.
 */
public static class RetentionModel
{
    /// <summary>Rejects a score that is not a valid count of correct answers out of at least one question.</summary>
    public static void ValidateScore(int correctCount, int totalQuestions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalQuestions);
        if (correctCount < 0 || correctCount > totalQuestions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correctCount), correctCount, "Must be between 0 and the total question count.");
        }
    }

    /// <summary>R(t) for accuracy A after <paramref name="elapsedDays"/>; negative elapsed time counts as 0.</summary>
    public static double Recall(double accuracy, double elapsedDays, RetentionModelOptions options) =>
        accuracy * Math.Pow(2, -Math.Max(0, elapsedDays) / options.HalfLifeDays);

    /// <summary>Days from the test until recall reaches the threshold; null when A is already at or below it.</summary>
    public static double? DaysUntilThreshold(double accuracy, RetentionModelOptions options) =>
        accuracy > options.ReviewThreshold ? options.HalfLifeDays * Math.Log2(accuracy / options.ReviewThreshold) : null;

    /// <summary>Evaluates the latest relevant test at <paramref name="now"/>.</summary>
    public static RetentionEstimate Evaluate(TestObservation? latest, DateTimeOffset now, RetentionModelOptions options)
    {
        if (latest is null) return new RetentionEstimate(ReviewStatus.InsufficientData, null, null, null);

        var recall = Recall(latest.Accuracy, (now - latest.CompletedAt).TotalDays, options);
        if (DaysUntilThreshold(latest.Accuracy, options) is not { } days)
        {
            return new RetentionEstimate(ReviewStatus.SupportNeeded, latest, recall, latest.CompletedAt);
        }

        var reviewAt = latest.CompletedAt.AddDays(days);
        return new RetentionEstimate(now >= reviewAt ? ReviewStatus.Due : ReviewStatus.Scheduled, latest, recall, reviewAt);
    }
}
