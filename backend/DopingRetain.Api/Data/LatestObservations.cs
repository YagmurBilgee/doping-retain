using DopingRetain.Api.Memory;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Data;

/// <summary>A topic's latest relevant test result and the schedule its review completion stored, if any.</summary>
/// <param name="ScheduledReviewAt">The mini quiz session's NextReviewAt; null for a normal test (formula applies).</param>
/// <param name="PerfectReviewStreak">The mini quiz session's sequence; 0 for a normal test, which resets it.</param>
public sealed record LatestObservation(TestResult Result, DateTimeOffset? ScheduledReviewAt, int PerfectReviewStreak)
{
    /// <summary>The topic's effective status and review time at <paramref name="now"/>.</summary>
    public RetentionEstimate Evaluate(DateTimeOffset now, RetentionModelOptions options) =>
        ReviewSchedulePolicy.Evaluate(Result.ToObservation(), ScheduledReviewAt, now, options);
}

/// <summary>The single source of the schedule state that topic queries and review completion evaluate.</summary>
public static class LatestObservations
{
    /// <summary>Latest result per topic of a student (or of one topic), with its stored schedule.</summary>
    public static async Task<Dictionary<string, LatestObservation>> LoadAsync(
        AppDbContext db, string studentId, string? topicId, CancellationToken cancellationToken)
    {
        // SQLite cannot order by DateTimeOffset, so the latest result per topic is picked in memory.
        var latestByTopic = (await db.TestResults
                .AsNoTracking()
                .Where(result => result.StudentId == studentId && (topicId == null || result.TopicId == topicId))
                .ToListAsync(cancellationToken))
            .GroupBy(result => result.TopicId)
            .ToDictionary(group => group.Key, group => TestResult.Latest(group)!);

        var sessionIds = latestByTopic.Values
            .Where(result => result.ReviewSessionId is not null)
            .Select(result => result.ReviewSessionId!.Value)
            .ToList();
        var schedules = await db.ReviewSessions
            .AsNoTracking()
            .Where(session => sessionIds.Contains(session.Id))
            .Select(session => new { session.Id, session.NextReviewAt, session.PerfectReviewStreak })
            .ToDictionaryAsync(session => session.Id, cancellationToken);

        return latestByTopic.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ReviewSessionId is { } sessionId && schedules.TryGetValue(sessionId, out var session)
                ? new LatestObservation(pair.Value, session.NextReviewAt, session.PerfectReviewStreak)
                : new LatestObservation(pair.Value, null, 0));
    }
}
