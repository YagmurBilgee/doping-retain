using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Reviews;
using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Tests;

/// <summary>Shared ids, clocks and steps of the sixth-grade tests.</summary>
internal static class SixthGrade
{
    /// <summary>Thursday 2026-10-29 12:00 in Istanbul; its review week started Monday 2026-10-26 00:00 (Istanbul).</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    public const string Elif = "grade6-elif";
    public const string Kerem = "grade6-kerem";
    public const string Zeynep = "grade6-zeynep";
    public const string Mert = "grade6-mert";
    public const string Ada = "grade6-ada";

    // Topic columns, in display order.
    public const string Listening = "274513";
    public const string Writing = "274523";
    public const string Primes = "274839";
    public const string TriangleAngles = "274894";
    public const string SolarSystem = "275020";
    public const string Density = "275040";
    public const string Location = "274665";
    public const string FirstTurkicStates = "274669";

    public static TimeProvider ClockAt(DateTimeOffset instant) => DemoClock.Create(instant.ToString("O"), isDevelopment: true);

    public static ReviewSessionService Sessions(AppDbContext db, DateTimeOffset? at = null) =>
        new(db, ClockAt(at ?? Now), TestOptions.Default);

    public static DemoEditorService Editor(AppDbContext db, DateTimeOffset? at = null) =>
        new(db, ClockAt(at ?? Now), TestOptions.Default);

    /// <summary>Starts a review for the student and answers it with <paramref name="correctCount"/> correct answers.</summary>
    public static async Task<Guid> StartAndAnswerAsync(TemporaryDatabase database, string studentId, string topicId, int correctCount, int? answerCount = null)
    {
        using var db = database.CreateContext();
        var session = (await Sessions(db).StartOrResumeAsync(studentId, topicId)).Value!;
        var keys = await db.QuizQuestions.Where(q => q.TopicId == topicId).ToDictionaryAsync(q => q.Id, q => q.CorrectIndex);
        foreach (var (question, index) in session.Questions.Take(answerCount ?? session.Questions.Count).Select((q, i) => (q, i)))
        {
            var key = keys[question.Id];
            var selected = index < correctCount ? key : (key + 1) % question.Options.Count;
            Assert.Equal(200, (await Sessions(db).SubmitAnswerAsync(studentId, session.SessionId, new SubmitAnswerRequest(question.Id, selected))).StatusCode);
        }
        return session.SessionId;
    }

    public static async Task<ReviewCompletionResponse> CompleteReviewAsync(TemporaryDatabase database, string studentId, string topicId, int correctCount)
    {
        var sessionId = await StartAndAnswerAsync(database, studentId, topicId, correctCount);
        using var db = database.CreateContext();
        var completion = await Sessions(db).CompleteAsync(studentId, sessionId);
        Assert.Equal(200, completion.StatusCode);
        return completion.Value!;
    }

    /// <summary>
    /// Every stored learning value of the students: topic states (with version), results with their answers,
    /// sessions and session answers. Result Ids are insertion order and not part of the snapshot.
    /// </summary>
    public static List<string> Snapshot(TemporaryDatabase database, params string[] studentIds)
    {
        using var db = database.CreateContext();
        var states = db.StudentTopicStates.AsNoTracking()
            .Where(s => studentIds.Contains(s.StudentId))
            .AsEnumerable()
            .OrderBy(s => s.StudentId).ThenBy(s => s.TopicId)
            .Select(s => $"state|{s.StudentId}|{s.TopicId}|{s.LastReviewedAt:O}|v{s.Version}");
        var results = db.TestResults.AsNoTracking()
            .Include(r => r.NormalTestAnswers)
            .Where(r => studentIds.Contains(r.StudentId))
            .AsEnumerable()
            .OrderBy(r => r.StudentId).ThenBy(r => r.TopicId).ThenBy(r => r.CompletedAt)
            .Select(r => $"result|{r.StudentId}|{r.TopicId}|{r.TestType}|{r.CompletedAt:O}|{r.CorrectCount}/{r.TotalQuestions}|{r.ReviewSessionId}|" +
                string.Join(",", r.NormalTestAnswers.OrderBy(a => a.QuestionId).Select(a => $"{a.QuestionId}={a.SelectedIndex}")));
        var sessions = db.ReviewSessions.AsNoTracking()
            .Include(s => s.Answers)
            .Where(s => studentIds.Contains(s.StudentId))
            .AsEnumerable()
            .OrderBy(s => s.StudentId).ThenBy(s => s.Id)
            .Select(s => $"session|{s.StudentId}|{s.TopicId}|{s.Id}|{s.Status}|{s.Answers.Count}");
        return states.Concat(results).Concat(sessions).ToList();
    }

    /// <summary>The learning values a fresh seed at <paramref name="at"/> gives the students, without versions.</summary>
    public static List<string> FreshSeedSnapshot(DateTimeOffset at, params string[] studentIds)
    {
        using var fresh = new TemporaryDatabase();
        using (var db = fresh.CreateContext()) SeedData.Initialize(db, at);
        return WithoutVersions(Snapshot(fresh, studentIds));
    }

    public static List<string> WithoutVersions(List<string> snapshot) =>
        snapshot.Select(line => line.StartsWith("state|") ? line[..line.LastIndexOf("|v", StringComparison.Ordinal)] : line).ToList();
}
