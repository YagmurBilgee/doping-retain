using DopingRetain.Api.Data;

namespace DopingRetain.Api.Memory;

/// <summary>
/// Result of a student's first attempt at a single-topic test taken after learning the topic.
/// </summary>
/// <param name="TopicId">The test's topic, resolved upstream through the test-to-topic relation.</param>
/// <param name="CompletedAt">When the test was finished; any offset, normalized to UTC.</param>
public sealed record FirstAttemptTestResult(
    string StudentId,
    string TestId,
    string TopicId,
    DateTimeOffset CompletedAt,
    int CorrectCount,
    int QuestionCount);

/*
 * Initial memory state of a topic the student has no state for yet, from one test result.
 *
 * Only single-topic, first-attempt, post-learning tests qualify. A mixed-topic test needs a
 * question-to-topic mapping first; repeated attempts are not handled here. Test accuracy is a
 * proxy for initial recall, not a direct measurement of memory.
 */
public static class InitialMemoryState
{
    /// <summary>
    /// Starting stability S in days for a first test. Prototype default: an unvalidated
    /// starting assumption, not a parameter fitted on student data.
    /// </summary>
    public const double DefaultInitialStrength = 14;

    /// <summary>
    /// Builds a new state with R0 = correct / questions (unrounded) and LastStudiedAt = completion.
    /// The test is not a micro-review, so review count, success rate and follow-up start empty.
    /// Pure: it creates a new object and never reads or changes stored state; the caller must use it
    /// only when the student has no state for the topic yet.
    /// </summary>
    public static StudentTopicState FromFirstAttemptTest(FirstAttemptTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.StudentId, nameof(result.StudentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(result.TestId, nameof(result.TestId));
        ArgumentException.ThrowIfNullOrWhiteSpace(result.TopicId, nameof(result.TopicId));
        if (result.CompletedAt == default)
            throw new ArgumentException("Completion time is required.", nameof(result.CompletedAt));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(result.QuestionCount, nameof(result.QuestionCount));
        if (result.CorrectCount < 0 || result.CorrectCount > result.QuestionCount)
            throw new ArgumentOutOfRangeException(
                nameof(result.CorrectCount), result.CorrectCount, "Must be between 0 and the question count.");

        return new StudentTopicState
        {
            StudentId = result.StudentId,
            TopicId = result.TopicId,
            Strength = DefaultInitialStrength,
            InitialRecall = (double)result.CorrectCount / result.QuestionCount,
            LastStudiedAt = result.CompletedAt.ToUniversalTime(),
            ReviewCount = 0,
            SuccessRate = 0,
            NeedsFollowUp = false,
            Version = 0,
        };
    }
}
