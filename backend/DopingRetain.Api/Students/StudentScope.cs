using DopingRetain.Api.Data;
using DopingRetain.Api.Reviews;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Students;

public sealed record StudentDto(string Id, string DisplayName, DemoGroup Group);

/// <param name="Students">Every demo student, grouped (YKS first) and in each group's order; the first of a group is its default.</param>
/// <param name="DemoEnabled">Whether the demo reset and editor endpoints are mapped (Demo:Enabled).</param>
public sealed record StudentsResponse(IReadOnlyList<StudentDto> Students, bool DemoEnabled);

/// <summary>Why a student-scoped request was rejected; the same { code, message } body as every endpoint.</summary>
public sealed record ScopeFailure(int StatusCode, string Code, string Message)
{
    public ServiceResult<T> As<T>() => ServiceResult<T>.Failure(StatusCode, Code, Message);

    public IResult ToHttpResult() => Results.Json(new ApiError(Code, Message), statusCode: StatusCode);
}

/// <summary>
/// Checks that a request names an existing student and, for topic requests, a topic of that student's group
/// that is assigned to the student. Every student-scoped read and write goes through these checks, so one
/// student can never read or change another group's topics. There is no authentication: the student id in
/// the route is trusted as the demo selection.
/// </summary>
public static class StudentScope
{
    public static async Task<IReadOnlyList<StudentDto>> ListAsync(AppDbContext db, CancellationToken cancellationToken) =>
        (await db.Students.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(student => student.Group)
            .ThenBy(student => student.SortOrder)
            .ThenBy(student => student.Id, StringComparer.Ordinal)
            .Select(student => new StudentDto(student.Id, student.DisplayName, student.Group))
            .ToList();

    public static async Task<(Student? Student, ScopeFailure? Failure)> FindStudentAsync(
        AppDbContext db, string studentId, CancellationToken cancellationToken)
    {
        var student = await db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == studentId, cancellationToken);
        return student is null
            ? (null, new ScopeFailure(StatusCodes.Status404NotFound, "student_not_found", $"Student '{studentId}' does not exist."))
            : (student, null);
    }

    public static async Task<ScopeFailure?> CheckStudentAsync(AppDbContext db, string studentId, CancellationToken cancellationToken) =>
        (await FindStudentAsync(db, studentId, cancellationToken)).Failure;

    /// <summary>
    /// Null when the topic belongs to the student's group and is assigned to the student. A topic of the other
    /// group is rejected as topic_not_in_group; a missing or unassigned topic as topic_not_found.
    /// </summary>
    public static async Task<ScopeFailure?> CheckTopicAsync(
        AppDbContext db, string studentId, string topicId, CancellationToken cancellationToken)
    {
        var (student, failure) = await FindStudentAsync(db, studentId, cancellationToken);
        if (student is null) return failure;

        var topicGroup = await db.Topics
            .Where(topic => topic.Id == topicId)
            .Select(topic => (DemoGroup?)topic.Group)
            .SingleOrDefaultAsync(cancellationToken);
        if (topicGroup is null) return TopicNotFound(topicId);
        if (topicGroup != student.Group)
        {
            return new ScopeFailure(StatusCodes.Status404NotFound, "topic_not_in_group",
                $"Topic '{topicId}' does not belong to the group of student '{studentId}'.");
        }

        var assigned = await db.StudentTopicStates.AnyAsync(
            state => state.StudentId == studentId && state.TopicId == topicId, cancellationToken);
        return assigned ? null : TopicNotFound(topicId);
    }

    private static ScopeFailure TopicNotFound(string topicId) =>
        new(StatusCodes.Status404NotFound, "topic_not_found", $"Topic '{topicId}' does not exist.");
}
