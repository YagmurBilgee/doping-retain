using System.Text.Json;
using System.Text.Json.Serialization;
using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Reviews;

/// <summary>
/// Starts or resumes the demo student's review sessions, records answers and completes reviews.
/// Starting and answering never touch StudentTopicState; only completion updates memory.
/// </summary>
public sealed class ReviewSessionService(AppDbContext db, TimeProvider clock)
{
    private const string StudentId = SeedData.DemoStudentId;
    private const int SqliteUniqueViolation = 2067; // SQLITE_CONSTRAINT_UNIQUE
    private const int SqlitePrimaryKeyViolation = 1555; // SQLITE_CONSTRAINT_PRIMARYKEY

    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public async Task<ServiceResult<ReviewSessionResponse>> StartOrResumeAsync(
        string topicId, CancellationToken cancellationToken = default)
    {
        if (!await db.Topics.AnyAsync(topic => topic.Id == topicId, cancellationToken))
        {
            return ServiceResult<ReviewSessionResponse>.Failure(
                StatusCodes.Status404NotFound, "topic_not_found", $"Topic '{topicId}' does not exist.");
        }

        var questions = await db.QuizQuestions
            .AsNoTracking()
            .Where(question => question.TopicId == topicId)
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);
        if (questions.Count == 0)
        {
            return ServiceResult<ReviewSessionResponse>.Failure(
                StatusCodes.Status409Conflict, "topic_has_no_questions", $"Topic '{topicId}' has no quiz questions.");
        }

        // BEGIN IMMEDIATE (see CompleteAsync): starts, completions and resets run one at a time, so the
        // open-session check, retiring a stale session and opening its replacement commit together.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var stateVersion = await db.StudentTopicStates
            .Where(state => state.StudentId == StudentId && state.TopicId == topicId)
            .Select(state => state.Version)
            .SingleAsync(cancellationToken);

        var openSession = await FindOpenSessionAsync(topicId, cancellationToken);
        if (openSession is not null && openSession.StateVersionAtStart == stateVersion)
        {
            return ServiceResult<ReviewSessionResponse>.Success(ToResponse(openSession, questions));
        }
        if (openSession is not null)
        {
            // Opened against memory state that has since changed: retire it so it can never be completed.
            // Saved first, so the unique open-session index is free for the replacement.
            openSession.Status = ReviewSessionStatus.Abandoned;
            await db.SaveChangesAsync(cancellationToken);
        }

        var session = new ReviewSession
        {
            Id = Guid.NewGuid(),
            StudentId = StudentId,
            TopicId = topicId,
            Status = ReviewSessionStatus.Open,
            StartedAt = clock.GetUtcNow(),
            StateVersionAtStart = stateVersion,
        };
        db.ReviewSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<ReviewSessionResponse>.Success(ToResponse(session, questions), StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AnswerFeedbackDto>> SubmitAnswerAsync(
        Guid sessionId, SubmitAnswerRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionId) || request.SelectedIndex is not { } selectedIndex)
        {
            return Failure(StatusCodes.Status400BadRequest, "invalid_request", "questionId and selectedIndex are required.");
        }

        var session = await db.ReviewSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sessionId && s.StudentId == StudentId, cancellationToken);
        if (session is null) return SessionNotFound<AnswerFeedbackDto>(sessionId);
        if (session.Status == ReviewSessionStatus.Abandoned) return SessionAbandoned<AnswerFeedbackDto>();

        var question = await db.QuizQuestions
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.Id == request.QuestionId && q.TopicId == session.TopicId, cancellationToken);
        if (question is null)
        {
            return Failure(StatusCodes.Status400BadRequest, "question_not_in_session",
                $"Question '{request.QuestionId}' is not part of this review session.");
        }
        if (selectedIndex < 0 || selectedIndex >= question.Options.Count)
        {
            return Failure(StatusCodes.Status400BadRequest, "option_out_of_range",
                $"selectedIndex must be between 0 and {question.Options.Count - 1}.");
        }

        var storedAnswer = await FindAnswerAsync(sessionId, question.Id, cancellationToken)
            ?? await TryStoreAnswerAsync(sessionId, question, selectedIndex, cancellationToken);
        if (storedAnswer is null) return await RejectClosedSessionAsync(sessionId, cancellationToken);

        // An identical retry gets the stored feedback, even after completion; it adds nothing new.
        return storedAnswer.SelectedIndex == selectedIndex
            ? ServiceResult<AnswerFeedbackDto>.Success(ToFeedback(storedAnswer, question))
            : Failure(StatusCodes.Status409Conflict, "answer_conflict",
                $"Question '{question.Id}' was already answered with option {storedAnswer.SelectedIndex}.");
    }

    /// <summary>
    /// Completes a fully answered session: scores the stored answers, applies the memory model, and saves
    /// the updated memory state, the session's completion and its outcome snapshot in one transaction.
    /// A completed session returns its stored outcome without recalculating anything.
    /// </summary>
    public async Task<ServiceResult<ReviewCompletionResponse>> CompleteAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Microsoft.Data.Sqlite begins EF transactions with BEGIN IMMEDIATE, taking the write lock up front:
        // completions and answer inserts run one at a time, and the reads below see the latest commit.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var session = await db.ReviewSessions
            .Include(s => s.Answers)
            .SingleOrDefaultAsync(s => s.Id == sessionId && s.StudentId == StudentId, cancellationToken);
        if (session is null) return SessionNotFound<ReviewCompletionResponse>(sessionId);
        if (session.OutcomeJson is not null)
        {
            return ServiceResult<ReviewCompletionResponse>.Success(ReadOutcome(session.OutcomeJson));
        }
        if (session.Status == ReviewSessionStatus.Abandoned) return SessionAbandoned<ReviewCompletionResponse>();
        if (session.Status != ReviewSessionStatus.Open)
        {
            return CompletionFailure(StatusCodes.Status409Conflict, "session_closed", "This review session is closed.");
        }

        var questionIds = await db.QuizQuestions
            .Where(question => question.TopicId == session.TopicId)
            .Select(question => question.Id)
            .ToListAsync(cancellationToken);
        var unanswered = questionIds.Count(id => session.Answers.All(answer => answer.QuestionId != id));
        if (unanswered > 0)
        {
            return CompletionFailure(StatusCodes.Status409Conflict, "session_incomplete",
                $"{unanswered} of {questionIds.Count} questions are still unanswered.");
        }

        var state = await db.StudentTopicStates
            .Include(s => s.Topic)
            .SingleAsync(s => s.StudentId == StudentId && s.TopicId == session.TopicId, cancellationToken);

        var now = clock.GetUtcNow();
        var score = session.Answers.Count(answer => answer.IsCorrect);
        var before = state.ToMemoryState(now);
        var outcome = MemoryModel.ApplyReviewResult(before, score, questionIds.Count);
        var updated = outcome.UpdatedState;

        // The UPDATE matches only the version read when the session opened, so answers given against
        // outdated memory state can never overwrite a newer one.
        db.Entry(state).Property(s => s.Version).OriginalValue = session.StateVersionAtStart;
        state.Version = session.StateVersionAtStart + 1;
        state.Strength = updated.Strength;
        state.InitialRecall = updated.InitialRecall;
        state.LastStudiedAt = now;
        state.ReviewCount = updated.ReviewCount;
        state.SuccessRate = updated.SuccessRate;
        state.NeedsFollowUp = updated.NeedsFollowUp;

        var response = new ReviewCompletionResponse(
            session.Id,
            score,
            questionIds.Count,
            new CompletedTopicDto(
                state.TopicId,
                state.Topic.Title,
                state.Topic.Category,
                updated.ElapsedDays,
                now,
                updated.SuccessRate,
                updated.ReviewCount,
                updated.Strength,
                updated.InitialRecall,
                updated.NeedsFollowUp),
            outcome.Review,
            outcome.RetentionAtNextReview,
            outcome.RetentionGain,
            MemoryModel.GetTopicRisk(before),
            MemoryModel.GetTopicRisk(updated),
            now,
            now.AddDays(outcome.Review.NextReviewInDays));

        session.Status = ReviewSessionStatus.Completed;
        session.CompletedAt = response.CompletedAt;
        session.NextReviewAt = response.NextReviewAt;
        session.OutcomeJson = JsonSerializer.Serialize(response, SnapshotJson);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();

            var stored = await db.ReviewSessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId)
                .Select(s => new { s.OutcomeJson })
                .SingleOrDefaultAsync(cancellationToken);
            if (stored is null) return SessionNotFound<ReviewCompletionResponse>(sessionId);
            return stored.OutcomeJson is not null
                ? ServiceResult<ReviewCompletionResponse>.Success(ReadOutcome(stored.OutcomeJson))
                : CompletionFailure(StatusCodes.Status409Conflict, "stale_session",
                    "The topic's memory state changed after this session started. Start a new session.");
        }

        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<ReviewCompletionResponse>.Success(response);
    }

    /// <summary>
    /// Inserts the answer only while the session is open, in a single statement, so an answer can never
    /// land after completion. Returns the stored answer, or null when the session is closed. When a
    /// concurrent request stored an answer first, the primary key rejects this one and that answer wins.
    /// </summary>
    private async Task<SessionAnswer?> TryStoreAnswerAsync(
        Guid sessionId, QuizQuestion question, int selectedIndex, CancellationToken cancellationToken)
    {
        var isCorrect = selectedIndex == question.CorrectIndex;
        var answeredAt = clock.GetUtcNow();
        var open = nameof(ReviewSessionStatus.Open);

        try
        {
            var inserted = await db.Database.ExecuteSqlAsync($"""
                INSERT INTO "SessionAnswers" ("SessionId", "QuestionId", "SelectedIndex", "IsCorrect", "AnsweredAt")
                SELECT {sessionId}, {question.Id}, {selectedIndex}, {isCorrect}, {answeredAt}
                WHERE EXISTS (SELECT 1 FROM "ReviewSessions" WHERE "Id" = {sessionId} AND "Status" = {open})
                """, cancellationToken);
            if (inserted == 0) return null;
        }
        catch (SqliteException exception) when (IsUniqueViolation(exception))
        {
        }

        return await FindAnswerAsync(sessionId, question.Id, cancellationToken);
    }

    private Task<ReviewSession?> FindOpenSessionAsync(string topicId, CancellationToken cancellationToken) =>
        db.ReviewSessions
            .Include(session => session.Answers)
            .SingleOrDefaultAsync(
                session => session.StudentId == StudentId
                    && session.TopicId == topicId
                    && session.Status == ReviewSessionStatus.Open,
                cancellationToken);

    private Task<SessionAnswer?> FindAnswerAsync(Guid sessionId, string questionId, CancellationToken cancellationToken) =>
        db.SessionAnswers
            .AsNoTracking()
            .SingleOrDefaultAsync(answer => answer.SessionId == sessionId && answer.QuestionId == questionId, cancellationToken);

    private static ReviewSessionResponse ToResponse(ReviewSession session, IReadOnlyList<QuizQuestion> questions)
    {
        var answersByQuestion = session.Answers.ToDictionary(answer => answer.QuestionId);

        return new ReviewSessionResponse(
            session.Id,
            session.TopicId,
            session.StartedAt,
            questions.Select(question => new SessionQuestionDto(question.Id, question.Text, question.Options)).ToList(),
            questions
                .Where(question => answersByQuestion.ContainsKey(question.Id))
                .Select(question => ToFeedback(answersByQuestion[question.Id], question))
                .ToList());
    }

    private static AnswerFeedbackDto ToFeedback(SessionAnswer answer, QuizQuestion question) =>
        new(question.Id, answer.SelectedIndex, answer.IsCorrect, question.CorrectIndex, question.Explanation);

    private static ServiceResult<AnswerFeedbackDto> Failure(int statusCode, string code, string message) =>
        ServiceResult<AnswerFeedbackDto>.Failure(statusCode, code, message);

    private static ServiceResult<ReviewCompletionResponse> CompletionFailure(int statusCode, string code, string message) =>
        ServiceResult<ReviewCompletionResponse>.Failure(statusCode, code, message);

    private static ReviewCompletionResponse ReadOutcome(string json) =>
        JsonSerializer.Deserialize<ReviewCompletionResponse>(json, SnapshotJson)!;

    /// <summary>Explains why an answer was not inserted: the session was removed, abandoned or completed.</summary>
    private async Task<ServiceResult<AnswerFeedbackDto>> RejectClosedSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var status = await db.ReviewSessions
            .Where(s => s.Id == sessionId)
            .Select(s => (ReviewSessionStatus?)s.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return status switch
        {
            null => SessionNotFound<AnswerFeedbackDto>(sessionId),
            ReviewSessionStatus.Abandoned => SessionAbandoned<AnswerFeedbackDto>(),
            _ => Failure(StatusCodes.Status409Conflict, "session_closed", "This review session no longer accepts answers."),
        };
    }

    private static ServiceResult<T> SessionNotFound<T>(Guid sessionId) =>
        ServiceResult<T>.Failure(StatusCodes.Status404NotFound, "session_not_found",
            $"Review session '{sessionId}' does not exist. A demo reset removes all sessions.");

    private static ServiceResult<T> SessionAbandoned<T>() =>
        ServiceResult<T>.Failure(StatusCodes.Status409Conflict, "session_abandoned",
            "This review session was replaced because the topic's memory state changed. Start the review again.");

    private static bool IsUniqueViolation(SqliteException exception) =>
        exception.SqliteExtendedErrorCode is SqliteUniqueViolation or SqlitePrimaryKeyViolation;
}
