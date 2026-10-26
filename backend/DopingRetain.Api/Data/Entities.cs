using System.Text.Json.Serialization;
using DopingRetain.Api.Memory;

namespace DopingRetain.Api.Data;

public sealed class Student
{
    public required string Id { get; init; }
    public required string DisplayName { get; set; }
}

/// <summary>Shared topic content; per-student memory lives in <see cref="StudentTopicState"/>.</summary>
public sealed class Topic
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public required string Category { get; set; }
    /// <summary>Display order of topics; ties fall back to Id.</summary>
    public int SortOrder { get; set; }
    public List<QuizQuestion> Questions { get; init; } = [];
}

public sealed class QuizQuestion
{
    public required string Id { get; init; }
    public required string TopicId { get; init; }
    public int SortOrder { get; set; }
    public required string Text { get; set; }
    public required List<string> Options { get; set; }
    public int CorrectIndex { get; set; }
    /// <summary>One-line solution hint shown after a wrong answer.</summary>
    public string? Explanation { get; set; }
}

/// <summary>
/// Per-student, per-topic review bookkeeping. The memory estimate itself comes from <see cref="TestResult"/>.
/// </summary>
public sealed class StudentTopicState
{
    public required string StudentId { get; init; }
    public required string TopicId { get; init; }
    public Topic Topic { get; init; } = null!;

    /// <summary>
    /// Completion of the latest reminder + mini quiz review, always UTC; null before the first one.
    /// A normal test never changes it.
    /// </summary>
    public DateTimeOffset? LastReviewedAt { get; set; }
    /// <summary>Concurrency token: incremented once per completed review and per demo reset.</summary>
    public int Version { get; set; }
}

public enum TestType
{
    /// <summary>A regular unit/topic test taken outside the review feature.</summary>
    NormalTest,
    /// <summary>The mini quiz that completes a short reminder review.</summary>
    ReminderMiniQuiz,
}

/// <summary>
/// One completed test with its actual score and sample size: the observations the retention model uses.
/// Rows are only added, never replaced, so a later mini quiz keeps the earlier normal-test record.
/// </summary>
public sealed class TestResult
{
    /// <summary>Insertion order; breaks ties between results completed at the same instant.</summary>
    public long Id { get; init; }
    public required string StudentId { get; init; }
    public required string TopicId { get; init; }
    public TestType TestType { get; init; }
    /// <summary>UTC time the test was completed.</summary>
    public DateTimeOffset CompletedAt { get; init; }
    public int CorrectCount { get; init; }
    public int TotalQuestions { get; init; }
    /// <summary>The review session a mini quiz result came from; unique, so a review is recorded once.</summary>
    public Guid? ReviewSessionId { get; init; }

    public TestObservation ToObservation() => new(CompletedAt, CorrectCount, TotalQuestions);

    /// <summary>
    /// The latest relevant result by completion time, whatever its type. Equal completion times go to the
    /// later recorded row; a result submitted later but completed earlier never wins.
    /// </summary>
    public static TestResult? Latest(IEnumerable<TestResult> results) =>
        results.OrderByDescending(result => result.CompletedAt).ThenByDescending(result => result.Id).FirstOrDefault();
}

public enum ReviewSessionStatus
{
    Open,
    Completed,
    /// <summary>Replaced by a fresh session because the memory state changed after it opened.</summary>
    Abandoned,
}

/// <summary>One micro-review attempt of a student on a topic.</summary>
public sealed class ReviewSession
{
    public required Guid Id { get; init; }
    public required string StudentId { get; init; }
    public required string TopicId { get; init; }
    public ReviewSessionStatus Status { get; set; }
    /// <summary>UTC time the session was opened.</summary>
    public DateTimeOffset StartedAt { get; init; }
    /// <summary>StudentTopicState.Version when the session opened; completion requires it to be unchanged.</summary>
    public int StateVersionAtStart { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    /// <summary>Effective next review set by completion (see ReviewSchedulePolicy); the topic follows it while this quiz is its latest result.</summary>
    public DateTimeOffset? NextReviewAt { get; set; }
    /// <summary>
    /// Consecutive on-time perfect mini quizzes as of this completion; 0 when the sequence is broken. Sessions
    /// completed before this column existed read 0, so legacy history never unlocks the extended interval.
    /// </summary>
    public int PerfectReviewStreak { get; set; }
    /// <summary>Completion response as JSON, written once and returned verbatim on retries.</summary>
    public string? OutcomeJson { get; set; }
    public List<SessionAnswer> Answers { get; init; } = [];
}

/// <summary>The first answer given to a question within a session; it cannot be changed.</summary>
public sealed class SessionAnswer
{
    public required Guid SessionId { get; init; }
    public required string QuestionId { get; init; }
    public int SelectedIndex { get; init; }
    public bool IsCorrect { get; init; }
    /// <summary>UTC time the answer was stored.</summary>
    public DateTimeOffset AnsweredAt { get; init; }
}

/// <summary>A recall bullet of the micro-review, tagged with a memory hint (static seed content).</summary>
/// <param name="Icon">Short emoji marker, e.g. 📌.</param>
/// <param name="Tag">Memory tag, e.g. "Temel Özdeşlik".</param>
/// <param name="Text">The formula or fact itself.</param>
/// <param name="Note">Optional plain-language reminder; omitted from JSON when absent.</param>
/// <param name="Detail">
/// Short lesson (3–5 sentences) on the same point, shown step by step in the reinforcement flow; omitted from JSON when absent.
/// </param>
/// <param name="Example">A short worked example for the lesson; omitted from JSON when absent.</param>
public sealed record SummaryPoint(
    string Icon,
    string Tag,
    string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Example = null);
