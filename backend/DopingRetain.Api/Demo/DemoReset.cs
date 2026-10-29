using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Demo;

/// <summary>
/// Reset of one demo group's learning data to its initial scenario. Each group is reset on its own: resetting
/// the sixth grade never touches the YKS student and the other way round. Topic content and student records
/// stay; the database file is never deleted or recreated.
/// </summary>
public static class DemoReset
{
    /// <summary>
    /// Restores the group's initial scenario at <paramref name="now"/>, in one transaction: deletes the group's
    /// test results (with their answers), sessions and session answers, aligns the students' topics with the
    /// group's demo topics (topics outside the scenario are unassigned, their content stays), clears review
    /// dates, increments every state version and re-adds the group's synthetic normal-test results for this one
    /// reference instant.
    /// </summary>
    public static async Task ResetAsync(
        AppDbContext db, DemoGroup group, DateTimeOffset now, RetentionModelOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (students, topicIds, results) = group switch
        {
            DemoGroup.Yks => (
                (IReadOnlyList<Student>)[new Student { Id = SeedData.DemoStudentId, DisplayName = "Demo Öğrenci", Group = DemoGroup.Yks }],
                SeedData.DemoTopicIds,
                SeedData.CreateSeedTestResults(now, options)),
            DemoGroup.SixthGrade => (
                SixthGradeSeed.CreateStudents().ToList(),
                SixthGradeSeed.TopicIds,
                SixthGradeSeed.CreateResults(now)),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown demo group."),
        };
        var studentIds = students.Select(student => student.Id).ToList();

        // BEGIN IMMEDIATE: waits for a running answer insert, start, completion or edit to commit and holds
        // new ones back until the reset commits, so none of them can write pre-reset state afterwards.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await DemoHistory.DeleteAsync(db, studentIds, topicId: null, cancellationToken);

        // Students are never deleted; a scenario student missing from the database is added back.
        var existingStudents = await db.Students.Where(s => studentIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(cancellationToken);
        db.Students.AddRange(students.Where(student => !existingStudents.Contains(student.Id)));

        await db.StudentTopicStates
            .Where(state => studentIds.Contains(state.StudentId) && !topicIds.Contains(state.TopicId))
            .ExecuteDeleteAsync(cancellationToken);
        var states = await db.StudentTopicStates
            .Where(state => studentIds.Contains(state.StudentId))
            .ToListAsync(cancellationToken);
        // Demo topics a student lacks (their content is added on startup) start fresh.
        db.StudentTopicStates.AddRange(studentIds
            .SelectMany(studentId => topicIds
                .Where(topicId => !states.Any(state => state.StudentId == studentId && state.TopicId == topicId))
                .Select(topicId => new StudentTopicState { StudentId = studentId, TopicId = topicId })));
        foreach (var state in states)
        {
            state.LastReviewedAt = null;
            // Versions only grow, so nothing captured before the reset can match a state after it.
            state.Version++;
        }

        db.TestResults.AddRange(results);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

/// <summary>Deletes learning history inside the caller's transaction, children before parents.</summary>
public static class DemoHistory
{
    /// <summary>
    /// Removes the students' normal-test answers, test results, session answers and review sessions; only for
    /// <paramref name="topicId"/> when given, otherwise for all their topics. Topic states are left to the caller.
    /// </summary>
    public static async Task DeleteAsync(
        AppDbContext db, IReadOnlyCollection<string> studentIds, string? topicId, CancellationToken cancellationToken)
    {
        var results = db.TestResults.Where(result => studentIds.Contains(result.StudentId) && (topicId == null || result.TopicId == topicId));
        await db.NormalTestAnswers
            .Where(answer => results.Any(result => result.Id == answer.TestResultId))
            .ExecuteDeleteAsync(cancellationToken);
        // Results reference their sessions, so they go before the sessions.
        await results.ExecuteDeleteAsync(cancellationToken);

        var sessions = db.ReviewSessions.Where(session => studentIds.Contains(session.StudentId) && (topicId == null || session.TopicId == topicId));
        await db.SessionAnswers
            .Where(answer => sessions.Any(session => session.Id == answer.SessionId))
            .ExecuteDeleteAsync(cancellationToken);
        await sessions.ExecuteDeleteAsync(cancellationToken);
    }
}
