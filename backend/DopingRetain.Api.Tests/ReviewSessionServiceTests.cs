using System.Text.Json;
using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DopingRetain.Api.Tests;

public sealed class ReviewSessionServiceTests : IDisposable
{
    private const string Trigonometry = "ayt-mat-trigonometri";
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = DemoClock.Create("2026-10-21T09:00:00Z", isDevelopment: true);

    private readonly TemporaryDatabase _database = new();

    public ReviewSessionServiceTests()
    {
        using var db = _database.CreateContext();
        SeedData.Initialize(db, Now);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Start_CreatesSessionWithOrderedQuestionsAndNoAnswerData()
    {
        var result = await Start();

        Assert.Equal(201, result.StatusCode);
        var session = result.Value!;
        Assert.Equal(Trigonometry, session.TopicId);
        Assert.Equal(Now, session.StartedAt);
        Assert.Equal(new[] { "trig-q1", "trig-q2", "trig-q3" }, session.Questions.Select(q => q.Id));
        Assert.Equal(new[] { "3/5", "4/5", "3/4", "5/3" }, session.Questions[1].Options);
        Assert.Empty(session.Answers);

        var json = Serialize(session);
        Assert.DoesNotContain("correctIndex", json);
        Assert.DoesNotContain("explanation", json);
        Assert.DoesNotContain("isCorrect", json);
    }

    [Fact]
    public async Task Start_WithOpenSession_ResumesItWithFeedbackOnlyForAnsweredQuestions()
    {
        var sessionId = (await Start()).Value!.SessionId;
        await Answer(sessionId, "trig-q2", 1);

        var result = await Start();

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(sessionId, result.Value!.SessionId);
        var feedback = Assert.Single(result.Value.Answers);
        Assert.Equal(new AnswerFeedbackDto("trig-q2", 1, false, 0, feedback.Explanation), feedback);
        Assert.NotNull(feedback.Explanation);
        Assert.Equal(1, CountRows(db => db.ReviewSessions));
    }

    [Fact]
    public async Task Start_UnknownTopic_Returns404()
    {
        var result = await Start("unknown-topic");

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("topic_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Start_TopicWithoutQuestions_Returns409()
    {
        using (var db = _database.CreateContext())
        {
            db.Topics.Add(new Topic { Id = "no-quiz", Title = "No quiz", Category = "Test", Group = DemoGroup.Yks, SortOrder = 100 });
            db.StudentTopicStates.Add(new StudentTopicState { StudentId = SeedData.DemoStudentId, TopicId = "no-quiz" });
            db.SaveChanges();
        }

        var result = await Start("no-quiz");

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("topic_has_no_questions", result.Error!.Code);
        Assert.Equal(0, CountRows(db => db.ReviewSessions));
    }

    [Fact]
    public async Task Start_Concurrently_OpensExactlyOneSession()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Start())));

        Assert.Single(results, result => result.StatusCode == 201);
        Assert.All(results, result => Assert.Equal(results[0].Value!.SessionId, result.Value!.SessionId));
        Assert.Equal(1, CountRows(db => db.ReviewSessions));
    }

    [Fact]
    public async Task Answer_First_PersistsAnswerAndReturnsFeedback()
    {
        var sessionId = (await Start()).Value!.SessionId;

        var result = await Answer(sessionId, "trig-q1", 3);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(new AnswerFeedbackDto("trig-q1", 3, true, 3, result.Value!.Explanation), result.Value);
        using var db = _database.CreateContext();
        var stored = Assert.Single(db.SessionAnswers);
        Assert.Equal((sessionId, "trig-q1", 3, true, Now), (stored.SessionId, stored.QuestionId, stored.SelectedIndex, stored.IsCorrect, stored.AnsweredAt));
    }

    [Fact]
    public async Task Answer_IdenticalRetry_ReturnsStoredFeedback()
    {
        var sessionId = (await Start()).Value!.SessionId;
        var first = await Answer(sessionId, "trig-q3", 0);

        var retry = await Answer(sessionId, "trig-q3", 0);

        Assert.Equal(200, retry.StatusCode);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(1, CountRows(db => db.SessionAnswers));
    }

    [Fact]
    public async Task Answer_DifferentRetry_Returns409AndKeepsFirstAnswer()
    {
        var sessionId = (await Start()).Value!.SessionId;
        await Answer(sessionId, "trig-q3", 0);

        var conflict = await Answer(sessionId, "trig-q3", 1);

        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal("answer_conflict", conflict.Error!.Code);
        using var db = _database.CreateContext();
        Assert.Equal(0, Assert.Single(db.SessionAnswers).SelectedIndex);
    }

    [Theory]
    [InlineData(2, 200)] // same answer as the concurrent request
    [InlineData(1, 409)] // different answer
    public async Task Answer_WhenConcurrentRequestStoresFirst_DatabaseKeyDecides(int selectedIndex, int expectedStatus)
    {
        var sessionId = (await Start()).Value!.SessionId;
        var interceptor = new BeforeFirstInsert(() =>
        {
            using var other = _database.CreateContext();
            other.SessionAnswers.Add(new SessionAnswer
            {
                SessionId = sessionId,
                QuestionId = "trig-q1",
                SelectedIndex = 2,
                IsCorrect = false,
                AnsweredAt = Now,
            });
            other.SaveChanges();
        });

        var result = await Answer(sessionId, "trig-q1", selectedIndex, interceptor);

        Assert.True(interceptor.Ran);
        Assert.Equal(expectedStatus, result.StatusCode);
        using var db = _database.CreateContext();
        Assert.Equal(2, Assert.Single(db.SessionAnswers).SelectedIndex);
    }

    [Theory]
    [InlineData(null, 0, 400, "invalid_request")]
    [InlineData("trig-q1", null, 400, "invalid_request")]
    [InlineData("unknown-question", 0, 400, "question_not_in_session")]
    [InlineData("trig-q1", -1, 400, "option_out_of_range")]
    [InlineData("trig-q1", 4, 400, "option_out_of_range")]
    public async Task Answer_InvalidInput_IsRejectedWithoutStoring(string? questionId, int? selectedIndex, int status, string code)
    {
        var sessionId = (await Start()).Value!.SessionId;

        var result = await Submit(sessionId, new SubmitAnswerRequest(questionId, selectedIndex));

        Assert.Equal(status, result.StatusCode);
        Assert.Equal(code, result.Error!.Code);
        Assert.Equal(0, CountRows(db => db.SessionAnswers));
    }

    [Fact]
    public async Task Answer_UnknownSession_Returns404()
    {
        var result = await Answer(Guid.NewGuid(), "trig-q1", 0);

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("session_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Answer_ClosedSession_Returns409()
    {
        var sessionId = (await Start()).Value!.SessionId;
        using (var db = _database.CreateContext())
        {
            db.ReviewSessions.Single().Status = ReviewSessionStatus.Completed;
            db.SaveChanges();
        }

        var result = await Answer(sessionId, "trig-q1", 3);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("session_closed", result.Error!.Code);
        Assert.Equal(0, CountRows(db => db.SessionAnswers));
    }

    [Fact]
    public async Task StartingAndAnswering_LeaveStudentMemoryStateUnchanged()
    {
        var before = ReadMemoryStates();

        var sessionId = (await Start()).Value!.SessionId;
        await Answer(sessionId, "trig-q1", 3);
        await Answer(sessionId, "trig-q2", 1);
        await Answer(sessionId, "trig-q2", 1);
        await Start();

        Assert.Equal(before, ReadMemoryStates());
    }

    [Fact]
    public void Initialize_OnDatabaseWithoutSessionTables_FailsWithoutCreatingThem()
    {
        using (var db = _database.CreateContext())
        {
            db.Database.ExecuteSqlRaw("DROP TABLE SessionAnswers; DROP TABLE ReviewSessions;");
        }

        using (var db = _database.CreateContext())
        {
            var error = Assert.Throws<InvalidOperationException>(() => SeedData.Initialize(db, Now));
            Assert.Contains("lacks columns: ReviewSessions.CompletedAt", error.Message);
            Assert.Contains("SessionAnswers.SessionId", error.Message);
        }

        using var check = _database.CreateContext();
        Assert.Equal(0, check.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM sqlite_master WHERE name IN ('ReviewSessions', 'SessionAnswers')").Single());
    }

    private async Task<ServiceResult<ReviewSessionResponse>> Start(string topicId = Trigonometry, params IInterceptor[] interceptors)
    {
        using var db = _database.CreateContext(interceptors);
        return await new ReviewSessionService(db, Clock, TestOptions.Default).StartOrResumeAsync(SeedData.DemoStudentId, topicId);
    }

    private Task<ServiceResult<AnswerFeedbackDto>> Answer(
        Guid sessionId, string questionId, int selectedIndex, params IInterceptor[] interceptors) =>
        Submit(sessionId, new SubmitAnswerRequest(questionId, selectedIndex), interceptors);

    private async Task<ServiceResult<AnswerFeedbackDto>> Submit(
        Guid sessionId, SubmitAnswerRequest request, params IInterceptor[] interceptors)
    {
        using var db = _database.CreateContext(interceptors);
        return await new ReviewSessionService(db, Clock, TestOptions.Default).SubmitAnswerAsync(SeedData.DemoStudentId, sessionId, request);
    }

    private int CountRows<T>(Func<AppDbContext, IQueryable<T>> table)
    {
        using var db = _database.CreateContext();
        return table(db).Count();
    }

    private List<string> ReadMemoryStates()
    {
        using var db = _database.CreateContext();
        return db.StudentTopicStates
            .AsNoTracking()
            .AsEnumerable()
            .OrderBy(state => state.TopicId)
            .Select(state => $"{state.TopicId}|{state.LastReviewedAt:O}|{state.Version}")
            .Append($"test results: {db.TestResults.Count()}")
            .ToList();
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
