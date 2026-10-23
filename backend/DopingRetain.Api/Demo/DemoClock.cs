using System.Globalization;
using DopingRetain.Api.Data;

namespace DopingRetain.Api.Demo;

/// <summary>
/// Chooses the app's clock: system UTC by default, or a fixed instant from Demo:FixedUtcNow
/// so a fresh Development database seeds and evaluates reproducibly.
/// </summary>
public static class DemoClock
{
    public const string FixedUtcNowKey = "Demo:FixedUtcNow";

    // ISO 8601 with "Z" or a numeric offset; a timestamp without an offset would be ambiguous.
    private static readonly string[] AcceptedFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];

    public static TimeProvider Create(string? fixedUtcNow, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(fixedUtcNow)) return TimeProvider.System;

        if (!isDevelopment)
        {
            throw new InvalidOperationException(
                $"{FixedUtcNowKey} is only supported in the Development environment. Remove it to use the system clock.");
        }

        if (!DateTimeOffset.TryParseExact(
                fixedUtcNow, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
        {
            throw new InvalidOperationException(
                $"{FixedUtcNowKey} value '{fixedUtcNow}' is not a valid timestamp. " +
                "Use ISO 8601 with an offset, for example 2026-10-21T09:00:00Z.");
        }

        return new FixedTimeProvider(instant.ToUniversalTime());
    }

    /// <summary>
    /// Fails when the fixed instant precedes any stored LastStudiedAt: the model would otherwise
    /// clamp the negative elapsed time to zero and report misleading retention.
    /// </summary>
    public static void EnsureNotBeforeLastStudied(AppDbContext db, DateTimeOffset now)
    {
        // SQLite cannot compare DateTimeOffset in SQL, so the few rows are checked in memory.
        var later = db.StudentTopicStates
            .Select(state => new { state.StudentId, state.TopicId, state.LastStudiedAt })
            .AsEnumerable()
            .Where(state => state.LastStudiedAt > now)
            .ToList();

        if (later.Count == 0) return;

        var details = string.Join(", ", later.Select(state => $"{state.StudentId}/{state.TopicId} at {state.LastStudiedAt:O}"));
        throw new InvalidOperationException(
            $"{FixedUtcNowKey} ({now:O}) is earlier than persisted LastStudiedAt values: {details}. " +
            "Use a later timestamp or a separate fresh database (Database:Path).");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
