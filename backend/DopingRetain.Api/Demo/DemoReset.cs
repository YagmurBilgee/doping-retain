using DopingRetain.Api.Data;
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

        app.MapPost(Path, async (AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            await ResetAsync(db, clock.GetUtcNow(), cancellationToken);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Deletes the demo student's sessions and answers and restores each topic state to its seed values,
    /// counted back from <paramref name="now"/>, in one transaction.
    /// </summary>
    public static async Task ResetAsync(AppDbContext db, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // BEGIN IMMEDIATE: waits for a running answer insert, start or completion to commit and holds
        // new ones back until the reset commits, so none of them can write pre-reset state afterwards.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var studentSessions = db.ReviewSessions.Where(session => session.StudentId == SeedData.DemoStudentId);
        await db.SessionAnswers
            .Where(answer => studentSessions.Any(session => session.Id == answer.SessionId))
            .ExecuteDeleteAsync(cancellationToken);
        await studentSessions.ExecuteDeleteAsync(cancellationToken);

        var states = await db.StudentTopicStates
            .Where(state => state.StudentId == SeedData.DemoStudentId)
            .ToListAsync(cancellationToken);
        foreach (var state in states)
        {
            SeedData.ApplyInitialState(state, now);
            // Versions only grow, so nothing captured before the reset can match a state after it.
            state.Version++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
