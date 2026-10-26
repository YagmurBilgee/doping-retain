namespace DopingRetain.Api.Memory;

/// <summary>When the next review is recommended after a mini quiz, and the perfect-review sequence it leaves.</summary>
/// <param name="PerfectReviewStreak">
/// Consecutive perfect mini quizzes completed at or after their recommended time; 0 when the sequence is broken.
/// </param>
public sealed record ReviewSchedule(DateTimeOffset NextReviewAt, int PerfectReviewStreak);

/*
 * Scheduling policy on top of the retention curve (RetentionModel). The curve still gives the estimated
 * recall and the formula-based review time; this policy only decides which review time applies:
 *   - a mini quiz that is not perfect: the formula from its own score (2/3 ≈ 4.15 days, 0/3–1/3 now);
 *   - a perfect mini quiz completed at or after the current recommended time ("on time"):
 *       the first in a sequence uses the formula (10 days), the next ones ExtendedIntervalDays (30) after completion;
 *   - a perfect mini quiz completed early: the current schedule and sequence stay exactly as they were.
 * Pure: no clock, no database.
 */
public static class ReviewSchedulePolicy
{
    /// <summary>
    /// The topic's effective status and review time. Recall and support/no-data statuses come from the curve;
    /// when the latest result is a mini quiz, the review time its completion stored decides due vs. scheduled.
    /// </summary>
    public static RetentionEstimate Evaluate(
        TestObservation? latest, DateTimeOffset? scheduledReviewAt, DateTimeOffset now, RetentionModelOptions options)
    {
        var estimate = RetentionModel.Evaluate(latest, now, options);
        if (scheduledReviewAt is not { } reviewAt || estimate.Status is ReviewStatus.InsufficientData or ReviewStatus.SupportNeeded)
        {
            return estimate;
        }
        return estimate with { Status = now >= reviewAt ? ReviewStatus.Due : ReviewStatus.Scheduled, ReviewAt = reviewAt };
    }

    /// <summary>The schedule after completing <paramref name="quiz"/> (completed now).</summary>
    /// <param name="current">The topic's effective estimate just before this completion, at the quiz's completion time.</param>
    /// <param name="currentStreak">The sequence the latest result left; 0 when it is a normal test or there is none.</param>
    public static ReviewSchedule AfterMiniQuiz(
        TestObservation quiz,
        RetentionEstimate current,
        int currentStreak,
        RetentionModelOptions retention,
        ReviewScheduleOptions schedule)
    {
        var perfect = quiz.CorrectCount == quiz.TotalQuestions;
        var formulaReviewAt = RetentionModel.Evaluate(quiz, quiz.CompletedAt, retention).ReviewAt!.Value;

        if (!perfect) return new ReviewSchedule(formulaReviewAt, 0);

        // Only a scheduled topic can be reviewed early; due, support-needed and no-data topics are on time.
        if (current is { Status: ReviewStatus.Scheduled, ReviewAt: { } currentReviewAt })
        {
            return new ReviewSchedule(currentReviewAt, currentStreak);
        }

        return currentStreak > 0
            ? new ReviewSchedule(quiz.CompletedAt.AddDays(schedule.ExtendedIntervalDays), currentStreak + 1)
            : new ReviewSchedule(formulaReviewAt, 1);
    }
}
