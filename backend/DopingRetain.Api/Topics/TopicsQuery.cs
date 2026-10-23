using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Topics;

/// <param name="WeeklyReviewedTopicCount">
/// Distinct topics with a review completed this week (Monday 00:00 Europe/Istanbul) up to EvaluatedAt.
/// </param>
public sealed record TopicsResponse(
    IReadOnlyList<TopicSummaryDto> Topics,
    string? HighestRiskTopicId,
    int HighRiskCount,
    int WeeklyReviewedTopicCount,
    DateTimeOffset EvaluatedAt);

public sealed record TopicSummaryDto(
    string Id,
    string Title,
    string Category,
    int Retention,
    RiskLevel RiskLevel,
    bool IsFollowUp,
    bool NeedsInitialReinforcement,
    DateTimeOffset LastStudiedAt,
    int ReviewCount,
    double SuccessRate);

/// <summary>
/// One topic with its micro-review content. Questions are only counted here; their text comes with a
/// review session, and correct answers or explanations only as feedback after an answer.
/// InitialTestAccuracy is the first-test accuracy (R0 as a percentage), set only while the topic needs
/// initial reinforcement; it is a test score, not a measured recall.
/// </summary>
public sealed record TopicDetailResponse(
    string Id,
    string Title,
    string Category,
    int Retention,
    RiskLevel RiskLevel,
    bool IsFollowUp,
    bool NeedsInitialReinforcement,
    DateTimeOffset LastStudiedAt,
    int ReviewCount,
    double SuccessRate,
    int QuestionCount,
    int? InitialTestAccuracy,
    IReadOnlyList<SummaryPoint> SummaryPoints,
    DateTimeOffset EvaluatedAt);

public static class TopicsQuery
{
    private sealed record EvaluatedTopic(TopicSummaryDto Summary, TopicMemoryState Memory);

    /// <summary>The student's week starts on Monday 00:00 in this time zone, not in UTC.</summary>
    private static readonly TimeZoneInfo WeekTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>Evaluates every topic of a student at one instant, in topic display order.</summary>
    public static async Task<TopicsResponse> GetTopicsAsync(
        AppDbContext db, string studentId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var evaluated = await EvaluateAsync(
            db.StudentTopicStates.Where(state => state.StudentId == studentId), now, cancellationToken);

        return new TopicsResponse(
            evaluated.Select(item => item.Summary).ToList(),
            MemoryModel.FindHighestRiskTopic(evaluated, item => item.Memory)?.Summary.Id,
            MemoryModel.CountHighRiskTopics(evaluated.Select(item => item.Memory)),
            await CountWeeklyReviewedTopicsAsync(db, studentId, now, cancellationToken),
            now);
    }

    /// <summary>
    /// Distinct topics with a completed review session whose CompletedAt lies in the current Istanbul week
    /// and not after <paramref name="now"/>. Derived from session records; a topic reviewed twice counts once.
    /// </summary>
    private static async Task<int> CountWeeklyReviewedTopicsAsync(
        AppDbContext db, string studentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (weekStart, weekEnd) = GetWeekBounds(now);

        var completions = await db.ReviewSessions
            .AsNoTracking()
            .Where(session => session.StudentId == studentId
                && session.Status == ReviewSessionStatus.Completed
                && session.CompletedAt != null)
            .Select(session => new { session.TopicId, CompletedAt = session.CompletedAt!.Value })
            .ToListAsync(cancellationToken);

        // SQLite cannot compare DateTimeOffset in SQL, so the UTC window is applied to the selected rows.
        return completions
            .Where(completion => completion.CompletedAt >= weekStart
                && completion.CompletedAt < weekEnd
                && completion.CompletedAt <= now)
            .Select(completion => completion.TopicId)
            .Distinct()
            .Count();
    }

    /// <summary>Monday 00:00 of <paramref name="now"/>'s Istanbul week and the following Monday, as UTC instants.</summary>
    private static (DateTimeOffset Start, DateTimeOffset End) GetWeekBounds(DateTimeOffset now)
    {
        var localDate = TimeZoneInfo.ConvertTime(now, WeekTimeZone).Date;
        var weekStartLocal = localDate.AddDays(-(((int)localDate.DayOfWeek + 6) % 7));

        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(weekStartLocal, WeekTimeZone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(weekStartLocal.AddDays(7), WeekTimeZone)));
    }

    /// <summary>One topic of a student evaluated at <paramref name="now"/>, or null when it does not exist.</summary>
    public static async Task<TopicDetailResponse?> GetTopicAsync(
        AppDbContext db, string studentId, string topicId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var evaluated = await EvaluateAsync(
            db.StudentTopicStates.Where(state => state.StudentId == studentId && state.TopicId == topicId), now, cancellationToken);
        if (evaluated is not [{ Summary: var summary, Memory: var memory }]) return null;

        var questionCount = await db.QuizQuestions.CountAsync(question => question.TopicId == topicId, cancellationToken);

        return new TopicDetailResponse(
            summary.Id,
            summary.Title,
            summary.Category,
            summary.Retention,
            summary.RiskLevel,
            summary.IsFollowUp,
            summary.NeedsInitialReinforcement,
            summary.LastStudiedAt,
            summary.ReviewCount,
            summary.SuccessRate,
            questionCount,
            summary.NeedsInitialReinforcement ? MemoryModel.ToDisplayPercentage(memory.InitialRecall) : null,
            SeedData.GetSummaryPoints(topicId),
            now);
    }

    private static async Task<List<EvaluatedTopic>> EvaluateAsync(
        IQueryable<StudentTopicState> states, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await states
            .AsNoTracking()
            .OrderBy(state => state.Topic.SortOrder)
            .ThenBy(state => state.TopicId)
            .Select(state => new { State = state, state.Topic.Title, state.Topic.Category })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var state = row.State;
                var memory = state.ToMemoryState(now);
                var risk = MemoryModel.GetTopicRisk(memory);
                var summary = new TopicSummaryDto(
                    state.TopicId,
                    row.Title,
                    row.Category,
                    risk.Retention,
                    risk.RiskLevel,
                    risk.IsFollowUp,
                    risk.NeedsInitialReinforcement,
                    state.LastStudiedAt,
                    state.ReviewCount,
                    state.SuccessRate);
                return new EvaluatedTopic(summary, memory);
            })
            .ToList();
    }
}
