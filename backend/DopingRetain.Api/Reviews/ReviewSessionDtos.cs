using DopingRetain.Api.Memory;

namespace DopingRetain.Api.Reviews;

/// <summary>A started or resumed session; feedback is included only for already answered questions.</summary>
public sealed record ReviewSessionResponse(
    Guid SessionId,
    string TopicId,
    DateTimeOffset StartedAt,
    IReadOnlyList<SessionQuestionDto> Questions,
    IReadOnlyList<AnswerFeedbackDto> Answers);

/// <summary>A question as the student sees it before answering: no correct index, no explanation.</summary>
public sealed record SessionQuestionDto(string Id, string Text, IReadOnlyList<string> Options);

/// <summary>Both fields are nullable so a missing value is rejected instead of defaulting to 0.</summary>
public sealed record SubmitAnswerRequest(string? QuestionId, int? SelectedIndex);

public sealed record AnswerFeedbackDto(
    string QuestionId,
    int SelectedIndex,
    bool IsCorrect,
    int CorrectIndex,
    string? Explanation);

/// <summary>
/// Completion result. The first fields match the frontend ReviewOutcome (updatedTopic, review,
/// retentionAtNextReview, retentionGain); the rest are API additions. RiskBefore and RiskAfter are evaluated
/// at the completion instant, so RiskAfter.Retention - RiskBefore.Retention equals RetentionGain.
/// </summary>
public sealed record ReviewCompletionResponse(
    Guid SessionId,
    int Score,
    int Total,
    CompletedTopicDto UpdatedTopic,
    ReviewResult Review,
    int RetentionAtNextReview,
    int RetentionGain,
    TopicRisk RiskBefore,
    TopicRisk RiskAfter,
    DateTimeOffset CompletedAt,
    DateTimeOffset NextReviewAt);

/// <summary>The reviewed topic in the frontend Topic shape (memory fields only, no quiz content).</summary>
public sealed record CompletedTopicDto(
    string Id,
    string Title,
    string Category,
    double LastStudiedDaysAgo,
    DateTimeOffset LastStudiedAt,
    double PreviousSuccessRate,
    int ReviewCount,
    double CurrentStrength,
    double InitialRecall,
    bool NeedsFollowUp);

public sealed record ApiError(string Code, string Message);

/// <summary>Either a value with its success status code or an error with its failure status code.</summary>
public sealed record ServiceResult<T>(int StatusCode, T? Value, ApiError? Error)
{
    public static ServiceResult<T> Success(T value, int statusCode = StatusCodes.Status200OK) => new(statusCode, value, null);

    public static ServiceResult<T> Failure(int statusCode, string code, string message) =>
        new(statusCode, default, new ApiError(code, message));

    public IResult ToHttpResult() => Error is null
        ? Results.Json(Value, statusCode: StatusCode)
        : Results.Json(Error, statusCode: StatusCode);
}
