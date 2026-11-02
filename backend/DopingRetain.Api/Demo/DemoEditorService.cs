using System.Globalization;
using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.Students;
using DopingRetain.Api.TestResults;
using DopingRetain.Api.Topics;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Demo;

/// <param name="Topic">The topic as the student screen evaluates it: status and review time come from the model.</param>
/// <param name="LatestNormalTest">The student's latest normal test of the topic; null without one.</param>
public sealed record DemoTopicRowDto(TopicSummaryDto Topic, TestResultDto? LatestNormalTest);

public sealed record DemoStudentTopicsDto(string Id, string DisplayName, IReadOnlyList<DemoTopicRowDto> Topics);

public sealed record DemoGroupResponse(IReadOnlyList<DemoStudentTopicsDto> Students, DateTimeOffset EvaluatedAt);

/// <summary>A normal-test question for the demo editor, with its answer key.</summary>
public sealed record DemoNormalQuestionDto(string Id, string Text, IReadOnlyList<string> Options, int CorrectIndex);

public sealed record DemoNormalAnswerDto(string QuestionId, int SelectedIndex, bool IsCorrect);

/// <param name="Answers">Empty when the result was recorded without individual answers.</param>
public sealed record DemoSavedNormalTestDto(DateTimeOffset CompletedAt, int CorrectCount, int TotalQuestions, IReadOnlyList<DemoNormalAnswerDto> Answers);

/// <param name="Saved">The student's latest normal test of the topic; null without one.</param>
public sealed record DemoNormalTestResponse(
    string StudentId,
    string TopicId,
    string Title,
    string Category,
    string? UnitName,
    IReadOnlyList<DemoNormalQuestionDto> Questions,
    DemoSavedNormalTestDto? Saved);

/// <summary>Both fields are nullable so a missing value is rejected instead of defaulting.</summary>
public sealed record DemoAnswerInput(string? QuestionId, int? SelectedIndex);

/// <param name="CompletedAt">ISO 8601 with "Z" or an offset, for example 2026-10-20T14:00+03:00.</param>
public sealed record SaveNormalTestRequest(IReadOnlyList<DemoAnswerInput>? Answers, string? CompletedAt);

/// <summary>
/// Demo editor operations for the sixth-grade group. Saving or clearing REPLACES the whole test and review
/// history of exactly one student and topic: normal tests with their answers, mini quiz results, review
/// sessions and session answers. It is a demo manipulation, unlike normal quiz submission, which stays
/// append-only. The recommendation is never stored: it is evaluated from the remaining results.
/// </summary>
public sealed class DemoEditorService(AppDbContext db, TimeProvider clock, RetentionModelOptions options)
{
    // ISO 8601 with "Z" or a numeric offset; a timestamp without one would depend on the server's zone.
    private static readonly string[] CompletedAtFormats =
    [
        "yyyy-MM-dd'T'HH:mm'Z'",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mmzzz",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];

    /// <summary>The group's students with their topics, all evaluated at one instant.</summary>
    public async Task<DemoGroupResponse> GetGroupAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var students = (await StudentScope.ListAsync(db, cancellationToken))
            .Where(student => student.Group == DemoGroup.SixthGrade)
            .ToList();

        var result = new List<DemoStudentTopicsDto>();
        foreach (var student in students)
        {
            result.Add(new DemoStudentTopicsDto(student.Id, student.DisplayName, await LoadRowsAsync(student.Id, now, cancellationToken)));
        }
        return new DemoGroupResponse(result, now);
    }

    public async Task<ServiceResult<DemoNormalTestResponse>> GetNormalTestAsync(
        string studentId, string topicId, CancellationToken cancellationToken = default)
    {
        if (await CheckEditableAsync(studentId, topicId, cancellationToken) is { } failure) return failure.As<DemoNormalTestResponse>();

        var topic = await db.Topics.AsNoTracking().SingleAsync(t => t.Id == topicId, cancellationToken);
        var questions = await LoadQuestionsAsync(topicId, cancellationToken);
        var latest = TestResult.Latest(await db.TestResults
            .AsNoTracking()
            .Include(result => result.NormalTestAnswers)
            .Where(result => result.StudentId == studentId && result.TopicId == topicId && result.TestType == TestType.NormalTest)
            .ToListAsync(cancellationToken));

        var saved = latest is null
            ? null
            : new DemoSavedNormalTestDto(
                latest.CompletedAt,
                latest.CorrectCount,
                latest.TotalQuestions,
                latest.NormalTestAnswers
                    .OrderBy(answer => questions.FindIndex(question => question.Id == answer.QuestionId))
                    .Select(answer => new DemoNormalAnswerDto(answer.QuestionId, answer.SelectedIndex, answer.IsCorrect))
                    .ToList());

        return ServiceResult<DemoNormalTestResponse>.Success(new DemoNormalTestResponse(
            studentId,
            topicId,
            topic.Title,
            topic.Category,
            topic.UnitName,
            questions.Select(q => new DemoNormalQuestionDto(q.Id, q.Text, q.Options, q.CorrectIndex)).ToList(),
            saved));
    }

    /// <summary>
    /// Replaces the student's whole history of the topic with one normal test built from the submitted answers.
    /// The score is the count of correct answers; it cannot be submitted separately.
    /// </summary>
    public async Task<ServiceResult<DemoTopicRowDto>> SaveNormalTestAsync(
        string studentId, string topicId, SaveNormalTestRequest request, CancellationToken cancellationToken = default)
    {
        if (await CheckEditableAsync(studentId, topicId, cancellationToken) is { } failure) return failure.As<DemoTopicRowDto>();

        var questions = await LoadQuestionsAsync(topicId, cancellationToken);
        var now = clock.GetUtcNow();
        if (Validate(request, questions, now) is { } invalid) return invalid;
        var completedAt = ParseCompletedAt(request.CompletedAt!)!.Value;

        var answers = request.Answers!
            .Select(input =>
            {
                var question = questions.Single(q => q.Id == input.QuestionId);
                return new NormalTestAnswer
                {
                    QuestionId = question.Id,
                    SelectedIndex = input.SelectedIndex!.Value,
                    IsCorrect = input.SelectedIndex == question.CorrectIndex,
                };
            })
            .ToList();

        await ReplaceHistoryAsync(studentId, topicId, new TestResult
        {
            StudentId = studentId,
            TopicId = topicId,
            TestType = TestType.NormalTest,
            CompletedAt = completedAt,
            CorrectCount = answers.Count(answer => answer.IsCorrect),
            TotalQuestions = questions.Count,
            NormalTestAnswers = answers,
        }, cancellationToken);

        return ServiceResult<DemoTopicRowDto>.Success(await LoadRowAsync(studentId, topicId, cancellationToken));
    }

    /// <summary>"No test history": the same scoped cleanup as saving, without a new result, so the topic has no data.</summary>
    public async Task<ServiceResult<DemoTopicRowDto>> ClearHistoryAsync(
        string studentId, string topicId, CancellationToken cancellationToken = default)
    {
        if (await CheckEditableAsync(studentId, topicId, cancellationToken) is { } failure) return failure.As<DemoTopicRowDto>();

        await ReplaceHistoryAsync(studentId, topicId, replacement: null, cancellationToken);
        return ServiceResult<DemoTopicRowDto>.Success(await LoadRowAsync(studentId, topicId, cancellationToken));
    }

    /// <summary>
    /// In one transaction: deletes the student's results (with answers), sessions (with answers) of the topic,
    /// clears its review date, increments its state version so nothing opened before can complete, and adds
    /// the replacement result if any. Other topics and students are not touched.
    /// </summary>
    private async Task ReplaceHistoryAsync(string studentId, string topicId, TestResult? replacement, CancellationToken cancellationToken)
    {
        // BEGIN IMMEDIATE, like completion and reset: they run one at a time.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await DemoHistory.DeleteAsync(db, [studentId], topicId, cancellationToken);

        var state = await db.StudentTopicStates.SingleAsync(s => s.StudentId == studentId && s.TopicId == topicId, cancellationToken);
        state.LastReviewedAt = null;
        state.Version++;

        if (replacement is not null) db.TestResults.Add(replacement);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>The student must exist, be in the sixth-grade group and the topic must be one of the student's topics.</summary>
    private async Task<ScopeFailure?> CheckEditableAsync(string studentId, string topicId, CancellationToken cancellationToken)
    {
        var (student, failure) = await StudentScope.FindStudentAsync(db, studentId, cancellationToken);
        if (student is null) return failure;
        if (student.Group != DemoGroup.SixthGrade)
        {
            return new ScopeFailure(StatusCodes.Status409Conflict, "group_not_editable",
                $"Student '{studentId}' is not in the sixth-grade demo group; only that group can be edited.");
        }
        return await StudentScope.CheckTopicAsync(db, studentId, topicId, cancellationToken);
    }

    private Task<List<NormalTestQuestion>> LoadQuestionsAsync(string topicId, CancellationToken cancellationToken) =>
        db.NormalTestQuestions
            .AsNoTracking()
            .Where(question => question.TopicId == topicId)
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);

    private static ServiceResult<DemoTopicRowDto>? Validate(SaveNormalTestRequest request, List<NormalTestQuestion> questions, DateTimeOffset now)
    {
        if (questions.Count == 0)
        {
            return Invalid(StatusCodes.Status409Conflict, "topic_has_no_normal_test", "This topic has no normal-test questions.");
        }
        if (request.Answers is not { } answers || request.CompletedAt is null)
        {
            return Invalid(StatusCodes.Status400BadRequest, "invalid_request", "answers and completedAt are required.");
        }
        if (answers.Count != questions.Count)
        {
            return Invalid(StatusCodes.Status400BadRequest, "invalid_answer_count",
                $"Exactly {questions.Count} answers are required, one per question; got {answers.Count}.");
        }
        var seen = new HashSet<string>();
        foreach (var answer in answers)
        {
            if (string.IsNullOrWhiteSpace(answer?.QuestionId) || answer.SelectedIndex is not { } selected)
            {
                return Invalid(StatusCodes.Status400BadRequest, "invalid_request", "Every answer needs questionId and selectedIndex.");
            }
            var question = questions.SingleOrDefault(q => q.Id == answer.QuestionId);
            if (question is null)
            {
                return Invalid(StatusCodes.Status400BadRequest, "question_not_in_test",
                    $"Question '{answer.QuestionId}' is not part of this topic's normal test.");
            }
            if (!seen.Add(question.Id))
            {
                return Invalid(StatusCodes.Status400BadRequest, "duplicate_question", $"Question '{question.Id}' is answered more than once.");
            }
            if (selected < 0 || selected >= question.Options.Count)
            {
                return Invalid(StatusCodes.Status400BadRequest, "option_out_of_range",
                    $"selectedIndex of '{question.Id}' must be between 0 and {question.Options.Count - 1}.");
            }
        }
        if (ParseCompletedAt(request.CompletedAt) is not { } completedAt)
        {
            return Invalid(StatusCodes.Status400BadRequest, "invalid_completed_at",
                "completedAt must be ISO 8601 with \"Z\" or an offset, for example 2026-10-20T14:00+03:00.");
        }
        if (completedAt > now)
        {
            return Invalid(StatusCodes.Status400BadRequest, "future_completion", $"completedAt must not be after {now:O}.");
        }
        return null;
    }

    private static DateTimeOffset? ParseCompletedAt(string value) =>
        DateTimeOffset.TryParseExact(value, CompletedAtFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    private static ServiceResult<DemoTopicRowDto> Invalid(int statusCode, string code, string message) =>
        ServiceResult<DemoTopicRowDto>.Failure(statusCode, code, message);

    private async Task<DemoTopicRowDto> LoadRowAsync(string studentId, string topicId, CancellationToken cancellationToken) =>
        (await LoadRowsAsync(studentId, clock.GetUtcNow(), cancellationToken)).Single(row => row.Topic.Id == topicId);

    private async Task<List<DemoTopicRowDto>> LoadRowsAsync(string studentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var topics = await TopicsQuery.GetTopicsAsync(db, studentId, now, options, cancellationToken);
        var latestNormalTests = (await db.TestResults
                .AsNoTracking()
                .Where(result => result.StudentId == studentId && result.TestType == TestType.NormalTest)
                .ToListAsync(cancellationToken))
            .GroupBy(result => result.TopicId)
            .ToDictionary(group => group.Key, group => TestResult.Latest(group)!);

        return topics.Topics
            .Select(topic => new DemoTopicRowDto(
                topic,
                latestNormalTests.TryGetValue(topic.Id, out var normal) ? TestResultDto.From(normal) : null))
            .ToList();
    }
}
