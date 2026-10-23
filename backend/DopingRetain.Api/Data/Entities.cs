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

/// <summary>One student's memory state for one topic: the inputs of the memory model.</summary>
public sealed class StudentTopicState
{
    public required string StudentId { get; init; }
    public required string TopicId { get; init; }
    public Topic Topic { get; init; } = null!;

    /// <summary>Memory stability S in days.</summary>
    public double Strength { get; set; }
    /// <summary>Initial recall R0 (0–1) right after the last study session.</summary>
    public double InitialRecall { get; set; }
    /// <summary>Last study or review time, always UTC.</summary>
    public DateTimeOffset LastStudiedAt { get; set; }
    public int ReviewCount { get; set; }
    /// <summary>0–1 running average of review score ratios.</summary>
    public double SuccessRate { get; set; }
    public bool NeedsFollowUp { get; set; }
    /// <summary>Concurrency token: incremented once per completed review.</summary>
    public int Version { get; set; }

    /// <summary>Memory-model input at <paramref name="now"/>; elapsed time is fractional days.</summary>
    public TopicMemoryState ToMemoryState(DateTimeOffset now) =>
        new((now - LastStudiedAt).TotalDays, Strength, InitialRecall, ReviewCount, SuccessRate, NeedsFollowUp);
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
    public DateTimeOffset? NextReviewAt { get; set; }
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
public sealed record SummaryPoint(
    string Icon,
    string Tag,
    string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null);
