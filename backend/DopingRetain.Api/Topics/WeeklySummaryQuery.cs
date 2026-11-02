using DopingRetain.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Topics;

/// <param name="FirstDate">Monday of the current week (Europe/Istanbul calendar).</param>
/// <param name="LastDate">Sunday of the current week.</param>
/// <param name="Today">The evaluation instant's Istanbul date, inside the week.</param>
/// <param name="DistinctTopicCount">Distinct topics over the whole week, not the sum of the days.</param>
/// <param name="CompletedReviewCount">Completed review sessions in the week.</param>
/// <param name="ActiveDayCount">Days with at least one completed review session.</param>
/// <param name="Days">Monday to Sunday, always seven entries.</param>
public sealed record WeeklySummaryResponse(
    DateOnly FirstDate,
    DateOnly LastDate,
    DateOnly Today,
    int DistinctTopicCount,
    int CompletedReviewCount,
    int ActiveDayCount,
    IReadOnlyList<WeeklySummaryDay> Days,
    DateTimeOffset EvaluatedAt);

/// <param name="DistinctTopicCount">Distinct topics completed that day; a topic completed twice counts once.</param>
/// <param name="Topics">One row per topic, latest completion first.</param>
public sealed record WeeklySummaryDay(DateOnly Date, int DistinctTopicCount, IReadOnlyList<WeeklySummaryTopic> Topics);

/// <param name="ReviewCount">Completed sessions of this topic that day.</param>
/// <param name="LastCompletedAt">The day's latest completion of this topic (UTC).</param>
/// <param name="CorrectCount">That completion's stored mini quiz score; null only for a session without a result row.</param>
public sealed record WeeklySummaryTopic(
    string TopicId,
    string Title,
    string Category,
    string? UnitName,
    int ReviewCount,
    DateTimeOffset LastCompletedAt,
    int? CorrectCount,
    int? TotalQuestions);

/// <summary>Read-only weekly summary of one student's completed reviews, grouped by Istanbul day.</summary>
public static class WeeklySummaryQuery
{
    public static async Task<WeeklySummaryResponse> GetAsync(
        AppDbContext db, string studentId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var completions = await ReviewWeek.LoadCompletionsAsync(db, studentId, now, cancellationToken);

        var sessionIds = completions.Select(completion => completion.SessionId).ToList();
        var scores = await db.TestResults
            .AsNoTracking()
            .Where(result => result.StudentId == studentId
                && result.ReviewSessionId != null
                && sessionIds.Contains(result.ReviewSessionId.Value))
            .Select(result => new { SessionId = result.ReviewSessionId!.Value, result.CorrectCount, result.TotalQuestions })
            .ToDictionaryAsync(result => result.SessionId, cancellationToken);

        var topicIds = completions.Select(completion => completion.TopicId).Distinct().ToList();
        var topics = await db.Topics
            .AsNoTracking()
            .Where(topic => topicIds.Contains(topic.Id))
            .Select(topic => new { topic.Id, topic.Title, topic.Category, topic.UnitName })
            .ToDictionaryAsync(topic => topic.Id, cancellationToken);

        var firstDate = ReviewWeek.LocalDate(ReviewWeek.Bounds(now).Start);
        var days = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = firstDate.AddDays(offset);
                var dayTopics = completions
                    .Where(completion => ReviewWeek.LocalDate(completion.CompletedAt) == date)
                    .GroupBy(completion => completion.TopicId)
                    .Select(group =>
                    {
                        // Completions are ordered oldest first, so the last one is the day's latest.
                        var latest = group.Last();
                        var score = scores.GetValueOrDefault(latest.SessionId);
                        var topic = topics[group.Key];
                        return new WeeklySummaryTopic(
                            group.Key, topic.Title, topic.Category, topic.UnitName, group.Count(), latest.CompletedAt,
                            score?.CorrectCount, score?.TotalQuestions);
                    })
                    .OrderByDescending(topic => topic.LastCompletedAt)
                    .ToList();
                return new WeeklySummaryDay(date, dayTopics.Count, dayTopics);
            })
            .ToList();

        return new WeeklySummaryResponse(
            firstDate,
            firstDate.AddDays(6),
            ReviewWeek.LocalDate(now),
            topicIds.Count,
            completions.Count,
            days.Count(day => day.Topics.Count > 0),
            days,
            now);
    }
}
