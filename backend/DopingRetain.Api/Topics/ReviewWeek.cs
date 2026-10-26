using DopingRetain.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Topics;

/// <summary>One completed reminder mini quiz session (normal review or reinforcement) of the current week.</summary>
public sealed record WeeklyCompletion(Guid SessionId, string TopicId, DateTimeOffset CompletedAt);

/// <summary>
/// The student's review week and the completions that count in it, shared by the homepage counter and the weekly
/// summary: completed review sessions whose CompletedAt lies from Monday 00:00 to the next Monday 00:00 in
/// Europe/Istanbul and not after the evaluation instant. Normal tests, open and abandoned sessions never count.
/// </summary>
public static class ReviewWeek
{
    /// <summary>The student's week and days follow this time zone, not UTC.</summary>
    private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>Monday 00:00 of <paramref name="now"/>'s Istanbul week and the following Monday, as UTC instants.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Bounds(DateTimeOffset now)
    {
        var localDate = TimeZoneInfo.ConvertTime(now, TimeZone).Date;
        var weekStartLocal = localDate.AddDays(-(((int)localDate.DayOfWeek + 6) % 7));

        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(weekStartLocal, TimeZone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(weekStartLocal.AddDays(7), TimeZone)));
    }

    /// <summary>The Istanbul calendar date of an instant.</summary>
    public static DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);

    /// <summary>The student's completions in <paramref name="now"/>'s week, oldest first.</summary>
    public static async Task<List<WeeklyCompletion>> LoadCompletionsAsync(
        AppDbContext db, string studentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (weekStart, weekEnd) = Bounds(now);

        var completions = await db.ReviewSessions
            .AsNoTracking()
            .Where(session => session.StudentId == studentId
                && session.Status == ReviewSessionStatus.Completed
                && session.CompletedAt != null)
            .Select(session => new WeeklyCompletion(session.Id, session.TopicId, session.CompletedAt!.Value))
            .ToListAsync(cancellationToken);

        // SQLite cannot compare DateTimeOffset in SQL, so the UTC window is applied to the selected rows.
        return completions
            .Where(completion => completion.CompletedAt >= weekStart
                && completion.CompletedAt < weekEnd
                && completion.CompletedAt <= now)
            .OrderBy(completion => completion.CompletedAt)
            .ToList();
    }
}
