using DopingRetain.Api.Data;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.Students;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.TestResults;

public sealed record TestResultDto(TestType TestType, DateTimeOffset CompletedAt, int CorrectCount, int TotalQuestions)
{
    public static TestResultDto From(TestResult result) =>
        new(result.TestType, result.CompletedAt, result.CorrectCount, result.TotalQuestions);
}

/// <summary>All fields are nullable so a missing value is rejected instead of defaulting to 0.</summary>
/// <param name="CompletedAt">ISO 8601 with an offset, for example 2026-10-20T14:00:00+03:00.</param>
public sealed record RecordTestResultRequest(int? CorrectCount, int? TotalQuestions, DateTimeOffset? CompletedAt);

/// <param name="IsLatest">
/// Whether the stored result is now the topic's latest relevant test. A result completed before an
/// existing one is kept but does not replace it.
/// </param>
public sealed record RecordedTestResultResponse(TestResultDto Result, bool IsLatest);

/// <summary>
/// Records a topic-level normal test result for a student. DEMO INTEGRATION CONTRACT: a minimal
/// stand-in for a test platform feed, not an existing company integration. It updates the topic's test
/// observation only; it never completes a review, so LastReviewedAt and the weekly review count stay as they are.
/// </summary>
public sealed class TestResultService(AppDbContext db, TimeProvider clock)
{
    public async Task<ServiceResult<RecordedTestResultResponse>> RecordNormalTestAsync(
        string studentId, string topicId, RecordTestResultRequest request, CancellationToken cancellationToken = default)
    {
        if (request is not { CorrectCount: { } correct, TotalQuestions: { } total, CompletedAt: { } completedAt })
        {
            return Failure(StatusCodes.Status400BadRequest, "invalid_request", "correctCount, totalQuestions and completedAt are required.");
        }
        if (total <= 0 || correct < 0 || correct > total)
        {
            return Failure(StatusCodes.Status400BadRequest, "invalid_score",
                "totalQuestions must be greater than 0 and correctCount between 0 and totalQuestions.");
        }
        var now = clock.GetUtcNow();
        if (completedAt > now)
        {
            return Failure(StatusCodes.Status400BadRequest, "future_completion", $"completedAt must not be after {now:O}.");
        }
        if (await StudentScope.CheckTopicAsync(db, studentId, topicId, cancellationToken) is { } scopeFailure)
        {
            return scopeFailure.As<RecordedTestResultResponse>();
        }

        var result = new TestResult
        {
            StudentId = studentId,
            TopicId = topicId,
            TestType = TestType.NormalTest,
            CompletedAt = completedAt.ToUniversalTime(),
            CorrectCount = correct,
            TotalQuestions = total,
        };
        db.TestResults.Add(result);
        await db.SaveChangesAsync(cancellationToken);

        var topicResults = await db.TestResults
            .AsNoTracking()
            .Where(r => r.StudentId == studentId && r.TopicId == topicId)
            .ToListAsync(cancellationToken);

        return ServiceResult<RecordedTestResultResponse>.Success(
            new RecordedTestResultResponse(TestResultDto.From(result), TestResult.Latest(topicResults)?.Id == result.Id),
            StatusCodes.Status201Created);
    }

    private static ServiceResult<RecordedTestResultResponse> Failure(int statusCode, string code, string message) =>
        ServiceResult<RecordedTestResultResponse>.Failure(statusCode, code, message);
}
