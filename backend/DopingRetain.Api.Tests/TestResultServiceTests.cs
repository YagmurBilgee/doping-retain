using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.TestResults;
using DopingRetain.Api.Topics;

namespace DopingRetain.Api.Tests;

/// <summary>Normal test results, latest-observation ordering, and their separation from completed reviews.</summary>
public sealed class TestResultServiceTests : IDisposable
{
    private const string Trigonometry = "ayt-mat-trigonometri";
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDatabase _database = new();

    public TestResultServiceTests()
    {
        using var db = _database.CreateContext();
        SeedData.Initialize(db, Now);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task NormalTest_BecomesLatestObservation_WithoutCompletingAReview()
    {
        var result = await Record(new RecordTestResultRequest(7, 10, Now.AddHours(-2)));

        Assert.Equal(201, result.StatusCode);
        Assert.Equal(new RecordedTestResultResponse(new TestResultDto(TestType.NormalTest, Now.AddHours(-2), 7, 10), IsLatest: true), result.Value);

        var topics = await GetTopics();
        var trigonometry = topics.Topics.Single(t => t.Id == Trigonometry);
        Assert.Equal(new TestResultDto(TestType.NormalTest, Now.AddHours(-2), 7, 10), trigonometry.LatestTest);
        Assert.Equal(ReviewStatus.Scheduled, trigonometry.ReviewStatus);
        // A normal test is not our review: no review date, no weekly review count, no version change.
        Assert.Null(trigonometry.LastReviewedAt);
        Assert.Equal(0, topics.WeeklyReviewedTopicCount);
        using var db = _database.CreateContext();
        Assert.Equal(0, db.StudentTopicStates.Single(s => s.TopicId == Trigonometry).Version);
    }

    [Fact]
    public async Task OlderResultSubmittedLater_IsKeptButDoesNotReplaceTheNewerObservation()
    {
        await Record(new RecordTestResultRequest(9, 10, Now.AddDays(-1)));

        var older = await Record(new RecordTestResultRequest(2, 10, Now.AddDays(-3)));

        Assert.Equal((201, false), (older.StatusCode, older.Value!.IsLatest));
        var trigonometry = (await GetTopics()).Topics.Single(t => t.Id == Trigonometry);
        Assert.Equal(new TestResultDto(TestType.NormalTest, Now.AddDays(-1), 9, 10), trigonometry.LatestTest);
        using var db = _database.CreateContext();
        Assert.Equal(3, db.TestResults.Count(r => r.TopicId == Trigonometry));
    }

    [Fact]
    public async Task EqualCompletionTimes_GoToTheLaterRecordedResult()
    {
        await Record(new RecordTestResultRequest(9, 10, Now.AddDays(-1)));
        var tie = await Record(new RecordTestResultRequest(4, 10, Now.AddDays(-1)));

        Assert.True(tie.Value!.IsLatest);
        var trigonometry = (await GetTopics()).Topics.Single(t => t.Id == Trigonometry);
        Assert.Equal((4, ReviewStatus.SupportNeeded), (trigonometry.LatestTest!.CorrectCount, trigonometry.ReviewStatus));
    }

    [Fact]
    public async Task MiniQuizAfterNormalTest_BecomesLatest_AndKeepsTheNormalTestRecord()
    {
        await Record(new RecordTestResultRequest(6, 10, Now.AddHours(-1)));

        await CompleteReview(correctCount: 3);

        var trigonometry = (await GetTopics()).Topics.Single(t => t.Id == Trigonometry);
        Assert.Equal(new TestResultDto(TestType.ReminderMiniQuiz, Now, 3, 3), trigonometry.LatestTest);
        Assert.Equal(Now, trigonometry.LastReviewedAt);
        using var db = _database.CreateContext();
        Assert.Equal(
            new[] { "NormalTest 18/20", "NormalTest 6/10", "ReminderMiniQuiz 3/3" },
            db.TestResults.Where(r => r.TopicId == Trigonometry).OrderBy(r => r.Id).AsEnumerable()
                .Select(r => $"{r.TestType} {r.CorrectCount}/{r.TotalQuestions}"));
    }

    [Fact]
    public async Task NormalTestAfterReview_UpdatesTheObservationButNotTheReviewDate()
    {
        await CompleteReview(correctCount: 3);

        await Record(new RecordTestResultRequest(3, 10, Now), at: Now.AddDays(1));

        var trigonometry = (await GetTopics(Now.AddDays(1))).Topics.Single(t => t.Id == Trigonometry);
        Assert.Equal((TestType.NormalTest, 3), (trigonometry.LatestTest!.TestType, trigonometry.LatestTest.CorrectCount));
        Assert.Equal(ReviewStatus.SupportNeeded, trigonometry.ReviewStatus);
        Assert.Equal(Now, trigonometry.LastReviewedAt);
    }

    [Fact]
    public async Task IncompleteReview_StoresNoTestResultAndNoReviewDate()
    {
        using (var db = _database.CreateContext())
        {
            var sessions = new ReviewSessionService(db, ClockAt(Now), TestOptions.Default);
            var session = (await sessions.StartOrResumeAsync(Trigonometry)).Value!;
            await sessions.SubmitAnswerAsync(session.SessionId, new SubmitAnswerRequest(session.Questions[0].Id, 3));
            Assert.Equal(409, (await sessions.CompleteAsync(session.SessionId)).StatusCode);
        }

        using var check = _database.CreateContext();
        Assert.Equal(1, check.TestResults.Count(r => r.TopicId == Trigonometry));
        Assert.Null(check.StudentTopicStates.Single(s => s.TopicId == Trigonometry).LastReviewedAt);
    }

    [Fact]
    public async Task CompletingTheSameReviewTwice_StoresOneTestResult()
    {
        var sessionId = await CompleteReview(correctCount: 2);

        using (var db = _database.CreateContext())
        {
            Assert.Equal(200, (await new ReviewSessionService(db, ClockAt(Now.AddDays(2)), TestOptions.Default).CompleteAsync(sessionId)).StatusCode);
        }

        using var check = _database.CreateContext();
        Assert.Equal(1, check.TestResults.Count(r => r.ReviewSessionId == sessionId));
        Assert.Equal(Now, check.StudentTopicStates.Single(s => s.TopicId == Trigonometry).LastReviewedAt);
    }

    [Theory]
    [InlineData(null, 10, "invalid_request")]
    [InlineData(5, null, "invalid_request")]
    [InlineData(1, 0, "invalid_score")]
    [InlineData(-1, 10, "invalid_score")]
    [InlineData(11, 10, "invalid_score")]
    public async Task InvalidScores_AreRejectedWithoutStoring(int? correct, int? total, string code)
    {
        var result = await Record(new RecordTestResultRequest(correct, total, Now.AddHours(-1)));

        Assert.Equal((400, code), (result.StatusCode, result.Error!.Code));
        AssertOnlySeedResults();
    }

    [Fact]
    public async Task MissingOrFutureCompletionDate_IsRejectedWithoutStoring()
    {
        var missing = await Record(new RecordTestResultRequest(5, 10, null));
        var future = await Record(new RecordTestResultRequest(5, 10, Now.AddSeconds(1)));

        Assert.Equal((400, "invalid_request"), (missing.StatusCode, missing.Error!.Code));
        Assert.Equal((400, "future_completion"), (future.StatusCode, future.Error!.Code));
        AssertOnlySeedResults();
    }

    [Fact]
    public async Task UnknownTopic_Returns404()
    {
        var result = await Record(new RecordTestResultRequest(5, 10, Now), topicId: "unknown-topic");

        Assert.Equal((404, "topic_not_found"), (result.StatusCode, result.Error!.Code));
        AssertOnlySeedResults();
    }

    [Fact]
    public async Task CompletionDateWithOffset_IsStoredInUtc()
    {
        var result = await Record(new RecordTestResultRequest(5, 10, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.FromHours(3))));

        Assert.Equal(TimeSpan.Zero, result.Value!.Result.CompletedAt.Offset);
        Assert.Equal(Now.AddHours(-1), result.Value.Result.CompletedAt);
    }

    private async Task<ServiceResult<RecordedTestResultResponse>> Record(
        RecordTestResultRequest request, string topicId = Trigonometry, DateTimeOffset? at = null)
    {
        using var db = _database.CreateContext();
        return await new TestResultService(db, ClockAt(at ?? Now)).RecordNormalTestAsync(topicId, request);
    }

    private async Task<Guid> CompleteReview(int correctCount)
    {
        using var db = _database.CreateContext();
        var sessions = new ReviewSessionService(db, ClockAt(Now), TestOptions.Default);
        var session = (await sessions.StartOrResumeAsync(Trigonometry)).Value!;
        int[] correct = [3, 0, 1];
        foreach (var (question, index) in session.Questions.Select((question, index) => (question, index)))
        {
            var selected = index < correctCount ? correct[index] : (correct[index] + 1) % 4;
            await sessions.SubmitAnswerAsync(session.SessionId, new SubmitAnswerRequest(question.Id, selected));
        }
        Assert.Equal(200, (await sessions.CompleteAsync(session.SessionId)).StatusCode);
        return session.SessionId;
    }

    private async Task<TopicsResponse> GetTopics(DateTimeOffset? at = null)
    {
        using var db = _database.CreateContext();
        return await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, at ?? Now, TestOptions.Default);
    }

    private void AssertOnlySeedResults()
    {
        using var db = _database.CreateContext();
        Assert.Equal(5, db.TestResults.Count());
    }

    private static TimeProvider ClockAt(DateTimeOffset instant) => DemoClock.Create(instant.ToString("O"), isDevelopment: true);
}
