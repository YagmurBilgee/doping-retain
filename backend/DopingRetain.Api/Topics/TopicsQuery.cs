using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using DopingRetain.Api.TestResults;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Topics;

/// <param name="DueTopicIds">
/// Due topics in review priority order: lowest estimated recall first, ties in display order. Topics needing
/// support or without data are not included.
/// </param>
/// <param name="WeeklyReviewedTopicCount">
/// Distinct topics with a review completed this week (Monday 00:00 Europe/Istanbul) up to EvaluatedAt.
/// </param>
public sealed record TopicsResponse(
    IReadOnlyList<TopicSummaryDto> Topics,
    IReadOnlyList<string> DueTopicIds,
    int WeeklyReviewedTopicCount,
    DateTimeOffset EvaluatedAt);

/// <param name="LatestTest">The latest relevant completed test (normal test or mini quiz); null without data.</param>
/// <param name="NextReviewAt">When recall reaches the review threshold; the test's completion for support_needed.</param>
/// <param name="LastReviewedAt">Completion of the latest reminder + mini quiz review; normal tests never set it.</param>
public sealed record TopicSummaryDto(
    string Id,
    string Title,
    string Category,
    ReviewStatus ReviewStatus,
    TestResultDto? LatestTest,
    DateTimeOffset? NextReviewAt,
    DateTimeOffset? LastReviewedAt);

/// <summary>
/// One topic with its micro-review content. Questions are only counted here; their text comes with a
/// review session, and correct answers or explanations only as feedback after an answer.
/// </summary>
public sealed record TopicDetailResponse(
    string Id,
    string Title,
    string Category,
    ReviewStatus ReviewStatus,
    TestResultDto? LatestTest,
    DateTimeOffset? NextReviewAt,
    DateTimeOffset? LastReviewedAt,
    int QuestionCount,
    IReadOnlyList<SummaryPoint> SummaryPoints,
    DateTimeOffset EvaluatedAt);

public static class TopicsQuery
{
    private sealed record EvaluatedTopic(TopicSummaryDto Summary, RetentionEstimate Estimate);

    /// <summary>The student's week starts on Monday 00:00 in this time zone, not in UTC.</summary>
    private static readonly TimeZoneInfo WeekTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>Evaluates every topic of a student at one instant, in topic display order.</summary>
    public static async Task<TopicsResponse> GetTopicsAsync(
        AppDbContext db, string studentId, DateTimeOffset now, RetentionModelOptions options,
        CancellationToken cancellationToken = default)
    {
        var evaluated = await EvaluateAsync(db, studentId, topicId: null, now, options, cancellationToken);
        return new TopicsResponse(
            evaluated.Select(item => item.Summary).ToList(),
            // OrderBy is stable: equal recall keeps topic display order.
            evaluated
                .Where(item => item.Estimate.Status == ReviewStatus.Due)
                .OrderBy(item => item.Estimate.Recall)
                .Select(item => item.Summary.Id)
                .ToList(),
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
        AppDbContext db, string studentId, string topicId, DateTimeOffset now, RetentionModelOptions options,
        CancellationToken cancellationToken = default)
    {
        var evaluated = await EvaluateAsync(db, studentId, topicId, now, options, cancellationToken);
        if (evaluated is not [{ Summary: var summary }]) return null;

        var questionCount = await db.QuizQuestions.CountAsync(question => question.TopicId == topicId, cancellationToken);

        return new TopicDetailResponse(
            summary.Id,
            summary.Title,
            summary.Category,
            summary.ReviewStatus,
            summary.LatestTest,
            summary.NextReviewAt,
            summary.LastReviewedAt,
            questionCount,
            SeedData.GetSummaryPoints(topicId),
            now);
    }

    /// <summary>Evaluates the student's topics (or one topic) from each topic's latest relevant test result.</summary>
    private static async Task<List<EvaluatedTopic>> EvaluateAsync(
        AppDbContext db, string studentId, string? topicId, DateTimeOffset now, RetentionModelOptions options,
        CancellationToken cancellationToken)
    {
        var rows = await db.StudentTopicStates
            .AsNoTracking()
            .Where(state => state.StudentId == studentId && (topicId == null || state.TopicId == topicId))
            .OrderBy(state => state.Topic.SortOrder)
            .ThenBy(state => state.TopicId)
            .Select(state => new { state.TopicId, state.Topic.Title, state.Topic.Category, state.LastReviewedAt })
            .ToListAsync(cancellationToken);

        // SQLite cannot order by DateTimeOffset, so the latest result per topic is picked in memory.
        var latestByTopic = (await db.TestResults
                .AsNoTracking()
                .Where(result => result.StudentId == studentId && (topicId == null || result.TopicId == topicId))
                .ToListAsync(cancellationToken))
            .GroupBy(result => result.TopicId)
            .ToDictionary(group => group.Key, group => TestResult.Latest(group)!);

        return rows
            .Select(row =>
            {
                var latest = latestByTopic.GetValueOrDefault(row.TopicId);
                var estimate = RetentionModel.Evaluate(latest?.ToObservation(), now, options);
                var summary = new TopicSummaryDto(
                    row.TopicId,
                    row.Title,
                    row.Category,
                    estimate.Status,
                    latest is null ? null : TestResultDto.From(latest),
                    estimate.ReviewAt,
                    row.LastReviewedAt);
                return new EvaluatedTopic(summary, estimate);
            })
            .ToList();
    }
}
