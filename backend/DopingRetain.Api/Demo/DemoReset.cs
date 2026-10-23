using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Demo;

/// <summary>
/// Development-only reset of the demo student's learning data. Topic content, questions and the student
/// record stay; the database file is never deleted or recreated.
/// </summary>
public static class DemoReset
{
    public const string Path = "/api/demo/reset";

    /// <summary>Maps POST /api/demo/reset in Development only; elsewhere the route does not exist (404).</summary>
    public static void MapDemoReset(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        app.MapPost(Path, async (AppDbContext db, TimeProvider clock, RetentionModelOptions options, CancellationToken cancellationToken) =>
        {
            await ResetAsync(db, clock.GetUtcNow(), options, cancellationToken);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Restores the demo student's initial scenario at <paramref name="now"/>, in one transaction: deletes the
    /// student's test results, sessions and answers, aligns the student's topics with the demo topics (topics
    /// outside the scenario are unassigned, their content stays), clears review dates and re-adds the seed's
    /// synthetic normal-test results for this one reference instant.
    /// </summary>
    public static async Task ResetAsync(
        AppDbContext db, DateTimeOffset now, RetentionModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        // BEGIN IMMEDIATE: waits for a running answer insert, start or completion to commit and holds
        // new ones back until the reset commits, so none of them can write pre-reset state afterwards.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.TestResults.Where(result => result.StudentId == SeedData.DemoStudentId).ExecuteDeleteAsync(cancellationToken);
        var studentSessions = db.ReviewSessions.Where(session => session.StudentId == SeedData.DemoStudentId);
        await db.SessionAnswers
            .Where(answer => studentSessions.Any(session => session.Id == answer.SessionId))
            .ExecuteDeleteAsync(cancellationToken);
        await studentSessions.ExecuteDeleteAsync(cancellationToken);

        var demoTopicIds = SeedData.DemoTopicIds;
        await db.StudentTopicStates
            .Where(state => state.StudentId == SeedData.DemoStudentId && !demoTopicIds.Contains(state.TopicId))
            .ExecuteDeleteAsync(cancellationToken);
        var states = await db.StudentTopicStates
            .Where(state => state.StudentId == SeedData.DemoStudentId)
            .ToListAsync(cancellationToken);
        // Demo topics the student lacks (their content is added on startup) start fresh.
        db.StudentTopicStates.AddRange(demoTopicIds
            .Where(id => states.All(state => state.TopicId != id))
            .Select(id => new StudentTopicState { StudentId = SeedData.DemoStudentId, TopicId = id }));
        foreach (var state in states)
        {
            state.LastReviewedAt = null;
            // Versions only grow, so nothing captured before the reset can match a state after it.
            state.Version++;
        }

        db.TestResults.AddRange(SeedData.CreateSeedTestResults(now, options));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
