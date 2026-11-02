using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;
using DopingRetain.Api.Topics;
using Microsoft.EntityFrameworkCore;
using static DopingRetain.Api.Tests.SixthGrade;

namespace DopingRetain.Api.Tests;

/// <summary>The sixth-grade demo editor (save, clear) and the group-scoped resets.</summary>
public sealed class DemoEditorTests : IDisposable
{
    private readonly TemporaryDatabase _database = new();

    public DemoEditorTests()
    {
        using var db = _database.CreateContext();
        SeedData.Initialize(db, Now);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Save_ScoresTheAnswersOnTheServer_AndTheDateDecidesTheRecommendation()
    {
        var answers = AnswersWithWrong(Listening, 3);

        // 7/10 six days ago: past the 4.85-day crossing, so due; two days ago it is still scheduled.
        var sixDaysAgo = await Save(Elif, Listening, answers, "2026-10-23T12:00+03:00");
        var twoDaysAgo = await Save(Elif, Listening, answers, "2026-10-27T09:00:00Z");

        Assert.Equal(200, sixDaysAgo.StatusCode);
        Assert.Equal((7, 10, ReviewStatus.Due), (sixDaysAgo.Value!.LatestNormalTest!.CorrectCount, sixDaysAgo.Value.LatestNormalTest.TotalQuestions, sixDaysAgo.Value.Topic.ReviewStatus));
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero), sixDaysAgo.Value.LatestNormalTest.CompletedAt);
        Assert.Equal(ReviewStatus.Scheduled, twoDaysAgo.Value!.Topic.ReviewStatus);

        using var db = _database.CreateContext();
        var stored = db.TestResults.Include(r => r.NormalTestAnswers).Single(r => r.StudentId == Elif && r.TopicId == Listening);
        Assert.Equal((7, 10), (stored.CorrectCount, stored.NormalTestAnswers.Count(a => a.IsCorrect) + 3));
        var editor = await Editor(db).GetNormalTestAsync(Elif, Listening);
        Assert.Equal(10, editor.Value!.Questions.Count);
        Assert.Equal(answers.Select(a => (a.QuestionId, a.SelectedIndex!.Value)), editor.Value.Saved!.Answers.Select(a => (a.QuestionId, a.SelectedIndex)));
    }

    public static TheoryData<string, string?, string> InvalidRequests => new()
    {
        { "nine-answers", "2026-10-27T09:00Z", "invalid_answer_count" },
        { "duplicate", "2026-10-27T09:00Z", "duplicate_question" },
        { "foreign-question", "2026-10-27T09:00Z", "question_not_in_test" },
        { "option-4", "2026-10-27T09:00Z", "option_out_of_range" },
        { "negative-option", "2026-10-27T09:00Z", "option_out_of_range" },
        { "valid", "2026-10-30T09:00Z", "future_completion" },
        { "valid", "2026-10-27T09:00", "invalid_completed_at" },
        { "valid", "27.10.2026", "invalid_completed_at" },
        { "valid", null, "invalid_request" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Save_RejectsInvalidAnswersAndDates_WithoutChangingAnything(string answersKind, string? completedAt, string code)
    {
        var answers = AnswersWithWrong(Listening, 0);
        answers = answersKind switch
        {
            "nine-answers" => answers.Take(9).ToList(),
            "duplicate" => [.. answers.Take(9), answers[0]],
            "foreign-question" => [.. answers.Take(9), new DemoAnswerInput(SixthGradeContent.NormalQuestionId(Writing, 10), 0)],
            "option-4" => [.. answers.Take(9), answers[9] with { SelectedIndex = 4 }],
            "negative-option" => [.. answers.Take(9), answers[9] with { SelectedIndex = -1 }],
            _ => answers,
        };
        var before = Snapshot(_database, Elif);

        var result = await Save(Elif, Listening, answers, completedAt);

        Assert.Equal((400, code), (result.StatusCode, result.Error!.Code));
        Assert.Equal(before, Snapshot(_database, Elif));
    }

    [Fact]
    public async Task Save_ReplacesAllHistoryOfOnlyThatStudentAndTopic()
    {
        await CompleteReviewAsync(_database, Elif, Listening, 3);
        await CompleteReviewAsync(_database, Elif, Writing, 2);
        await CompleteReviewAsync(_database, Kerem, Listening, 3);
        var openOnOtherTopic = await StartAndAnswerAsync(_database, Elif, Primes, 1, answerCount: 1);
        var untouched = Snapshot(_database, Kerem, Zeynep, SeedData.DemoStudentId);
        var elifOtherTopics = Snapshot(_database, Elif).Where(line => !line.Contains($"|{Listening}|")).ToList();

        var saved = await Save(Elif, Listening, AnswersWithWrong(Listening, 10), "2026-10-28T09:00Z");

        Assert.Equal(200, saved.StatusCode);
        using var db = _database.CreateContext();
        var history = db.TestResults.Include(r => r.NormalTestAnswers).Where(r => r.StudentId == Elif && r.TopicId == Listening).ToList();
        var result = Assert.Single(history);
        Assert.Equal((TestType.NormalTest, 0, 10), (result.TestType, result.CorrectCount, result.NormalTestAnswers.Count));
        Assert.Empty(db.ReviewSessions.Where(s => s.StudentId == Elif && s.TopicId == Listening));
        var state = db.StudentTopicStates.Single(s => s.StudentId == Elif && s.TopicId == Listening);
        Assert.Null(state.LastReviewedAt);
        // Seed 0, completion +1, edit +1.
        Assert.Equal(2, state.Version);
        // 0/10 is a result: support needed, not "no data".
        Assert.Equal(ReviewStatus.SupportNeeded, saved.Value!.Topic.ReviewStatus);

        // The weekly summary loses the replaced review, keeps Elif's other one.
        var week = await WeeklySummaryQuery.GetAsync(db, Elif, Now);
        Assert.Equal(new[] { Writing }, week.Days.SelectMany(d => d.Topics).Select(t => t.TopicId));
        Assert.Equal(untouched, Snapshot(_database, Kerem, Zeynep, SeedData.DemoStudentId));
        // Elif's other topics, including the session still open on another topic, are unchanged.
        Assert.Equal(elifOtherTopics, Snapshot(_database, Elif).Where(line => !line.Contains($"|{Listening}|")).ToList());
        Assert.Contains(elifOtherTopics, line => line.Contains(openOnOtherTopic.ToString()) && line.Contains("|Open|"));
    }

    [Fact]
    public async Task Clear_LeavesNoHistory_SoTheTopicHasInsufficientData()
    {
        await CompleteReviewAsync(_database, Elif, Primes, 3);
        var sixthOthers = Snapshot(_database, Kerem, Zeynep, Mert, Ada);

        using var db = _database.CreateContext();
        var cleared = await Editor(db).ClearHistoryAsync(Elif, Primes);

        Assert.Equal(200, cleared.StatusCode);
        Assert.Equal((ReviewStatus.InsufficientData, null, null, null),
            (cleared.Value!.Topic.ReviewStatus, cleared.Value.Topic.LatestTest, cleared.Value.LatestNormalTest, cleared.Value.Topic.LastReviewedAt));
        Assert.Empty(db.TestResults.Where(r => r.StudentId == Elif && r.TopicId == Primes));
        Assert.Empty(db.ReviewSessions.Where(s => s.StudentId == Elif && s.TopicId == Primes));
        Assert.Equal(0, (await WeeklySummaryQuery.GetAsync(db, Elif, Now)).CompletedReviewCount);
        Assert.Null((await Editor(db).GetNormalTestAsync(Elif, Primes)).Value!.Saved);
        Assert.Equal(sixthOthers, Snapshot(_database, Kerem, Zeynep, Mert, Ada));
    }

    [Fact]
    public async Task EditAndClear_InvalidateSessionsOpenedBefore()
    {
        var beforeSave = await StartAndAnswerAsync(_database, Elif, SolarSystem, 3);
        await Save(Elif, SolarSystem, AnswersWithWrong(SolarSystem, 1), "2026-10-28T09:00Z");
        var beforeClear = await StartAndAnswerAsync(_database, Elif, SolarSystem, 3);
        using (var db = _database.CreateContext()) await Editor(db).ClearHistoryAsync(Elif, SolarSystem);

        using var check = _database.CreateContext();
        foreach (var sessionId in new[] { beforeSave, beforeClear })
        {
            var completion = await Sessions(check).CompleteAsync(Elif, sessionId);
            Assert.Equal((404, "session_not_found"), (completion.StatusCode, completion.Error!.Code));
        }
        // A review started now works again, against the new version.
        var fresh = await CompleteReviewAsync(_database, Elif, SolarSystem, 3);
        Assert.Equal(3, fresh.Score);
        Assert.Equal(3, check.StudentTopicStates.AsNoTracking().Single(s => s.StudentId == Elif && s.TopicId == SolarSystem).Version);
    }

    [Fact]
    public async Task Save_WhenTheInsertFails_RollsBackTheCleanupToo()
    {
        await CompleteReviewAsync(_database, Elif, Location, 2);
        var before = Snapshot(_database, Elif);

        using var db = _database.CreateContext(new BeforeFirstInsert(() => throw new InvalidOperationException("disk full")));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            Editor(db).SaveNormalTestAsync(Elif, Location, new SaveNormalTestRequest(AnswersWithWrong(Location, 0), "2026-10-28T09:00Z")));

        Assert.Equal(before, Snapshot(_database, Elif));
    }

    [Fact]
    public async Task Editor_RejectsTheYksGroupAndForeignTopics()
    {
        using var db = _database.CreateContext();
        var yks = await Editor(db).ClearHistoryAsync(SeedData.DemoStudentId, "ayt-mat-trigonometri");
        var foreign = await Editor(db).GetNormalTestAsync(Elif, "ayt-mat-trigonometri");
        var unknown = await Editor(db).GetNormalTestAsync("nobody", Listening);

        Assert.Equal((409, "group_not_editable"), (yks.StatusCode, yks.Error!.Code));
        Assert.Equal((404, "topic_not_in_group"), (foreign.StatusCode, foreign.Error!.Code));
        Assert.Equal((404, "student_not_found"), (unknown.StatusCode, unknown.Error!.Code));
        Assert.Equal(5, db.TestResults.Count(r => r.StudentId == SeedData.DemoStudentId));
    }

    [Fact]
    public async Task GetGroup_ListsTheFiveStudentsWithTheirEightTopics()
    {
        using var db = _database.CreateContext();
        var group = await Editor(db).GetGroupAsync();

        Assert.Equal(SixthGradeSeed.StudentIds, group.Students.Select(s => s.Id));
        Assert.All(group.Students, student => Assert.Equal(SixthGradeSeed.TopicIds, student.Topics.Select(t => t.Topic.Id)));
        var elifPrimes = group.Students[0].Topics.Single(t => t.Topic.Id == Primes);
        Assert.Equal((8, "Sayılar ve Nicelikler 1"), (elifPrimes.LatestNormalTest!.CorrectCount, elifPrimes.Topic.UnitName));
    }

    [Fact]
    public async Task SixthGradeReset_RestoresOnlyTheSixthGrade_AndYksResetOnlyYks()
    {
        var resetAt = Now.AddDays(1);
        await CompleteReviewAsync(_database, SeedData.DemoStudentId, "ayt-mat-trigonometri", 3);
        await CompleteReviewAsync(_database, Kerem, Listening, 3);
        var openSession = await StartAndAnswerAsync(_database, Ada, Primes, 1, answerCount: 1);
        using (var db = _database.CreateContext()) await Editor(db).ClearHistoryAsync(Elif, Listening);
        var yksBefore = Snapshot(_database, SeedData.DemoStudentId);
        var sixth = SixthGradeSeed.StudentIds.ToArray();

        using (var db = _database.CreateContext()) await DemoReset.ResetAsync(db, DemoGroup.SixthGrade, resetAt);

        Assert.Equal(FreshSeedSnapshot(resetAt, sixth), WithoutVersions(Snapshot(_database, sixth)));
        Assert.Equal(yksBefore, Snapshot(_database, SeedData.DemoStudentId));
        using (var db = _database.CreateContext())
        {
            var completion = await Sessions(db, resetAt).CompleteAsync(Ada, openSession);
            Assert.Equal((404, "session_not_found"), (completion.StatusCode, completion.Error!.Code));
            Assert.Equal(0, (await WeeklySummaryQuery.GetAsync(db, Kerem, resetAt)).CompletedReviewCount);
        }

        var sixthAfterReset = Snapshot(_database, sixth);
        using (var db = _database.CreateContext()) await DemoReset.ResetAsync(db, DemoGroup.Yks, resetAt);

        Assert.Equal(FreshSeedSnapshot(resetAt, SeedData.DemoStudentId), WithoutVersions(Snapshot(_database, SeedData.DemoStudentId)));
        Assert.Equal(sixthAfterReset, Snapshot(_database, sixth));
    }

    /// <summary>One answer per question of the topic's normal test; the first <paramref name="wrong"/> are wrong.</summary>
    private List<DemoAnswerInput> AnswersWithWrong(string topicId, int wrong)
    {
        using var db = _database.CreateContext();
        return db.NormalTestQuestions.Where(q => q.TopicId == topicId).OrderBy(q => q.SortOrder).AsEnumerable()
            .Select((q, index) => new DemoAnswerInput(q.Id, index < wrong ? (q.CorrectIndex + 1) % q.Options.Count : q.CorrectIndex))
            .ToList();
    }

    private async Task<ServiceResult<DemoTopicRowDto>> Save(string studentId, string topicId, List<DemoAnswerInput> answers, string? completedAt)
    {
        using var db = _database.CreateContext();
        return await Editor(db).SaveNormalTestAsync(studentId, topicId, new SaveNormalTestRequest(answers, completedAt));
    }
}
