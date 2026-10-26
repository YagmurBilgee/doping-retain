using DopingRetain.Api.Memory;
using DopingRetain.Api.TestResults;

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

/// <summary>Completion result: the quiz's stored score and the retention model's recommendation from it.</summary>
/// <param name="NextReviewAt">
/// The policy's next review (ReviewSchedulePolicy): usually where this quiz's curve reaches the threshold, the
/// extended interval after consecutive on-time perfect quizzes, or the unchanged schedule after an early perfect
/// retry; CompletedAt when support is needed now.
/// </param>
public sealed record ReviewCompletionResponse(
    Guid SessionId,
    int Score,
    int Total,
    CompletedTopicDto UpdatedTopic,
    DateTimeOffset CompletedAt,
    DateTimeOffset NextReviewAt,
    ReviewStatus ReviewStatus,
    TestResultDto TestResult);

/// <summary>The reviewed topic after completion.</summary>
public sealed record CompletedTopicDto(string Id, string Title, string Category, DateTimeOffset LastReviewedAt);

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
