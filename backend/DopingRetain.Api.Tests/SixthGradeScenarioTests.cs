using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using DopingRetain.Api.TestResults;
using DopingRetain.Api.Topics;
using Microsoft.EntityFrameworkCore;
using static DopingRetain.Api.Tests.SixthGrade;

namespace DopingRetain.Api.Tests;

/// <summary>Sixth-grade content, its initial scenario and the isolation of students and groups.</summary>
public sealed class SixthGradeScenarioTests : IDisposable
{
    private readonly TemporaryDatabase _database = new();

    public SixthGradeScenarioTests()
    {
        using var db = _database.CreateContext();
        SeedData.Initialize(db, Now);
    }

    public void Dispose() => _database.Dispose();

    private const ReviewStatus Due = ReviewStatus.Due;
    private const ReviewStatus Sched = ReviewStatus.Scheduled;
    private const ReviewStatus Support = ReviewStatus.SupportNeeded;
    private const ReviewStatus None = ReviewStatus.InsufficientData;

    /*
     * Expected states, written out by hand from h = 10 and threshold 0.50: a normal test of A = k/10 reaches the
     * threshold 10 · log2(A / 0.5) days after it (10/10: 10.0, 9/10: 8.48, 8/10: 6.78, 7/10: 4.85, 6/10: 2.63)
     * and k ≤ 5 needs support at once. Columns: Listening, Writing, Primes, TriangleAngles, SolarSystem,
     * Density, Location, FirstTurkicStates.
     */
    public static TheoryData<string, ReviewStatus[]> ExpectedStates => new()
    {
        // 9@2d, 7@7d, 8@3d, 4@1d, 9@5d, none, 10@14d, 6@6d
        { Elif, [Sched, Due, Sched, Support, Sched, None, Due, Due] },
        // 6@1d, 8@10d, 8@9d, 10@4d, none, 5@3d, 7@2d, 9@12d
        { Kerem, [Sched, Due, Due, Sched, None, Support, Sched, Due] },
        // none, 10@3d, 7@8d, 6@5d, 6@5d, 9@2d, 4@6d, 8@2d
        { Zeynep, [None, Sched, Due, Due, Due, Sched, Support, Sched] },
        // 8@9d, 5@2d, none, 9@12d, 4@5d, 7@1d, 0@3d, 10@5d
        { Mert, [Due, Support, None, Due, Support, Sched, Support, Sched] },
        // 10@8d, 8@8d, 6@5d, none, 8@10d, 3@4d, 6@1d, 4@2d
        { Ada, [Sched, Due, Due, None, Due, Support, Sched, Support] },
    };

    [Theory]
    [MemberData(nameof(ExpectedStates))]
    public async Task Seed_EvaluatesToTheExpectedStates_AndKeepsThemForMostOfADay(string studentId, ReviewStatus[] expected)
    {
        using var db = _database.CreateContext();
        foreach (var at in new[] { Now, Now.AddHours(20) })
        {
            var topics = await TopicsQuery.GetTopicsAsync(db, studentId, at, TestOptions.Default);
            Assert.Equal(expected, topics.Topics.Select(topic => topic.ReviewStatus));
            Assert.Equal(0, topics.WeeklyReviewedTopicCount);
        }
        // Every student shows each of the four recommendation states.
        Assert.Equal(4, expected.Distinct().Count());
    }

    [Fact]
    public void Seed_EveryTopicGetsDifferentRecommendationsAcrossStudents()
    {
        var states = ExpectedStates.Select(row => (ReviewStatus[])row[1]).ToList();
        for (var topic = 0; topic < 8; topic++)
        {
            Assert.True(states.Select(row => row[topic]).Distinct().Count() >= 2, $"Topic column {topic} has one state for everyone.");
        }
    }

    [Fact]
    public async Task Seed_SameScoreDifferentDate_AndSameDateDifferentScore_DiffersOnlyThroughHistory()
    {
        using var db = _database.CreateContext();

        // Primes 8/10: 3 days ago is scheduled, 9 days ago is due.
        var elifPrimes = await Topic(db, Elif, Primes);
        var keremPrimes = await Topic(db, Kerem, Primes);
        Assert.Equal(((8, 10), Sched), (Score(elifPrimes), elifPrimes.ReviewStatus));
        Assert.Equal(((8, 10), Due), (Score(keremPrimes), keremPrimes.ReviewStatus));
        Assert.Equal(Now.AddDays(-3), elifPrimes.LatestTest!.CompletedAt);
        Assert.Equal(Now.AddDays(-9), keremPrimes.LatestTest!.CompletedAt);
        // Next review = completion + 10 · log2(0.8 / 0.5) days.
        Assert.Equal(Now.AddDays(-3).AddDays(10 * Math.Log2(1.6)), elifPrimes.NextReviewAt!.Value, TimeSpan.FromSeconds(1));

        // Solar system 5 days ago: 9/10 scheduled, 6/10 due, 4/10 support needed.
        var bySolar = new[] { Elif, Zeynep, Mert }.Select(student => Topic(db, student, SolarSystem).Result).ToList();
        Assert.All(bySolar, topic => Assert.Equal(Now.AddDays(-5), topic.LatestTest!.CompletedAt));
        Assert.Equal(new[] { 9, 6, 4 }, bySolar.Select(topic => topic.LatestTest!.CorrectCount));
        Assert.Equal(new[] { Sched, Due, Support }, bySolar.Select(topic => topic.ReviewStatus));
    }

    [Fact]
    public async Task Seed_NoHistoryAndZeroScore_AreDifferentStates()
    {
        using var db = _database.CreateContext();
        var noHistory = await Topic(db, Mert, Primes);
        var zero = await Topic(db, Mert, Location);

        Assert.Equal((None, null), (noHistory.ReviewStatus, noHistory.LatestTest));
        Assert.Equal((Support, (0, 10)), (zero.ReviewStatus, Score(zero)));
    }

    [Fact]
    public void Content_HasTheEightCatalogTopicsWithSubjectAndUnit()
    {
        using var db = _database.CreateContext();
        var topics = db.Topics.Where(t => t.Group == DemoGroup.SixthGrade).OrderBy(t => t.SortOrder).ToList();

        Assert.Equal(new (string, string, string, int?, string?)[]
        {
            ("274513", "Dinleme-İzleme", "Türkçe", 274558, "Dilimizin Zenginliği"),
            ("274523", "Yazma", "Türkçe", 274559, "Farklı Dünyalar"),
            ("274839", "Asal Sayılar", "Matematik", 274880, "Sayılar ve Nicelikler 1"),
            ("274894", "Üçgenin Açıları", "Matematik", 274891, "Geometrik Şekiller"),
            ("275020", "Güneş Sistemi", "Fen Bilimleri", 274903, "Güneş Sistemi ve Tutulmalar"),
            ("275040", "Yoğunluk", "Fen Bilimleri", 275015, "Maddenin Ayırt Edici Özellikleri"),
            ("274665", "Ülkemizin, Kıtaların ve Okyanusların Konum Özellikleri", "Sosyal Bilgiler", 274602, "Evimiz Dünya"),
            ("274669", "Türkistan'da Kurulan İlk Türk Devletlerinin Medeniyetimize Katkıları", "Sosyal Bilgiler", 274603, "Ortak Mirasımız"),
        }, topics.Select(t => (t.Id, t.Title, t.Category, t.UnitId, t.UnitName)));
        Assert.All(topics.GroupBy(t => t.Category), subject => Assert.Equal(2, subject.Count()));
    }

    [Fact]
    public void Content_Has80NormalAnd24MiniQuizQuestionsAnd24Reminders_WithValidKeys()
    {
        using var db = _database.CreateContext();
        var topicIds = db.Topics.Where(t => t.Group == DemoGroup.SixthGrade).Select(t => t.Id).ToList();
        var normal = db.NormalTestQuestions.Where(q => topicIds.Contains(q.TopicId)).ToList();
        var quiz = db.QuizQuestions.Where(q => topicIds.Contains(q.TopicId)).ToList();

        Assert.Equal((80, 24), (normal.Count, quiz.Count));
        Assert.All(topicIds, id =>
        {
            Assert.Equal(10, normal.Count(q => q.TopicId == id));
            Assert.Equal(3, quiz.Count(q => q.TopicId == id));
            var points = SeedData.GetSummaryPoints(id);
            Assert.Equal(3, points.Count);
            // The support flow shows each reminder's lesson and example.
            Assert.All(points, p => Assert.False(string.IsNullOrWhiteSpace(p.Detail) || string.IsNullOrWhiteSpace(p.Example)));
        });
        Assert.All(normal.Select(q => (q.Options, q.CorrectIndex)).Concat(quiz.Select(q => (q.Options, q.CorrectIndex))), question =>
        {
            Assert.Equal(4, question.Options.Count);
            Assert.Equal(4, question.Options.Distinct().Count());
            Assert.InRange(question.CorrectIndex, 0, 3);
        });
        Assert.All(quiz, q => Assert.False(string.IsNullOrWhiteSpace(q.Explanation)));
        // Mini quiz question i checks reminder i.
        Assert.All(SixthGradeContent.Topics, topic => Assert.Equal(
            topic.Reminders.Select(r => r.Question.Text),
            quiz.Where(q => q.TopicId == topic.Id).OrderBy(q => q.SortOrder).Select(q => q.Text)));
    }

    [Fact]
    public void Seed_StoresOnlyNormalTestsWhoseAnswersMatchTheirScores()
    {
        using var db = _database.CreateContext();
        var students = SixthGradeSeed.StudentIds.ToList();
        var results = db.TestResults.Include(r => r.NormalTestAnswers).Where(r => students.Contains(r.StudentId)).ToList();
        var keys = db.NormalTestQuestions.ToDictionary(q => q.Id, q => q.CorrectIndex);

        Assert.Equal(35, results.Count);
        Assert.All(results, result =>
        {
            Assert.Equal((TestType.NormalTest, 10), (result.TestType, result.TotalQuestions));
            Assert.Equal(10, result.NormalTestAnswers.Select(a => a.QuestionId).Distinct().Count());
            Assert.All(result.NormalTestAnswers, a => Assert.Equal(keys[a.QuestionId] == a.SelectedIndex, a.IsCorrect));
            Assert.Equal(result.CorrectCount, result.NormalTestAnswers.Count(a => a.IsCorrect));
        });
        Assert.Equal(0, db.ReviewSessions.Count(s => students.Contains(s.StudentId)));
        Assert.All(db.StudentTopicStates.Where(s => students.Contains(s.StudentId)), s => Assert.Null(s.LastReviewedAt));
    }

    [Fact]
    public async Task MiniQuiz_AppendsAResultForThatStudentOnly_AndUpdatesItsRecommendationAndWeek()
    {
        var keremBefore = Snapshot(_database, Kerem, SeedData.DemoStudentId);

        // Elif's 4/10 triangle angles test needs support; an on-time 3/3 quiz schedules the formula's 10 days.
        var completion = await CompleteReviewAsync(_database, Elif, TriangleAngles, 3);

        Assert.Equal((3, 3, Sched), (completion.Score, completion.Total, completion.ReviewStatus));
        Assert.Equal(Now.AddDays(10), completion.NextReviewAt, TimeSpan.FromSeconds(1));
        using var db = _database.CreateContext();
        var elif = await TopicsQuery.GetTopicsAsync(db, Elif, Now, TestOptions.Default);
        var topic = elif.Topics.Single(t => t.Id == TriangleAngles);
        Assert.Equal((TestType.ReminderMiniQuiz, Sched, Now), (topic.LatestTest!.TestType, topic.ReviewStatus, topic.LastReviewedAt!.Value));
        Assert.Equal(2, db.TestResults.Count(r => r.StudentId == Elif && r.TopicId == TriangleAngles));
        Assert.Equal(1, elif.WeeklyReviewedTopicCount);
        var week = await WeeklySummaryQuery.GetAsync(db, Elif, Now);
        Assert.Equal((1, 1), (week.DistinctTopicCount, week.CompletedReviewCount));
        Assert.Equal(("Üçgenin Açıları", "Geometrik Şekiller", 3), (week.Days[3].Topics.Single().Title, week.Days[3].Topics.Single().UnitName, week.Days[3].Topics.Single().CorrectCount));

        // Nobody else changed.
        Assert.Equal(keremBefore, Snapshot(_database, Kerem, SeedData.DemoStudentId));
        Assert.Equal(0, (await WeeklySummaryQuery.GetAsync(db, Kerem, Now)).CompletedReviewCount);
    }

    [Fact]
    public async Task Session_OfAnotherStudent_IsNotFoundAndNothingIsStored()
    {
        var sessionId = await StartAndAnswerAsync(_database, Elif, Listening, 2, answerCount: 2);
        var before = Snapshot(_database, Elif, Kerem);

        using var db = _database.CreateContext();
        var question = db.QuizQuestions.First(q => q.TopicId == Listening && q.SortOrder == 3);
        var answer = await Sessions(db).SubmitAnswerAsync(Kerem, sessionId, new(question.Id, question.CorrectIndex));
        var complete = await Sessions(db).CompleteAsync(Kerem, sessionId);
        var yksComplete = await Sessions(db).CompleteAsync(SeedData.DemoStudentId, sessionId);

        Assert.Equal((404, "session_not_found"), (answer.StatusCode, answer.Error!.Code));
        Assert.Equal((404, "session_not_found"), (complete.StatusCode, complete.Error!.Code));
        Assert.Equal((404, "session_not_found"), (yksComplete.StatusCode, yksComplete.Error!.Code));
        Assert.Equal(before, Snapshot(_database, Elif, Kerem));
    }

    [Fact]
    public async Task TopicOfTheOtherGroup_IsRejectedForEveryOperation()
    {
        using var db = _database.CreateContext();
        var results = new TestResultService(db, ClockAt(Now));

        var yksStart = await Sessions(db).StartOrResumeAsync(SeedData.DemoStudentId, Listening);
        var sixthStart = await Sessions(db).StartOrResumeAsync(Elif, "ayt-mat-trigonometri");
        var sixthRecord = await results.RecordNormalTestAsync(Elif, "ayt-mat-trigonometri", new(5, 10, Now));
        var unknown = await Sessions(db).StartOrResumeAsync("nobody", Listening);
        var unassigned = await Sessions(db).StartOrResumeAsync(SeedData.DemoStudentId, "ayt-mat-fonksiyonlar");

        Assert.Equal((404, "topic_not_in_group"), (yksStart.StatusCode, yksStart.Error!.Code));
        Assert.Equal((404, "topic_not_in_group"), (sixthStart.StatusCode, sixthStart.Error!.Code));
        Assert.Equal((404, "topic_not_in_group"), (sixthRecord.StatusCode, sixthRecord.Error!.Code));
        Assert.Equal((404, "student_not_found"), (unknown.StatusCode, unknown.Error!.Code));
        Assert.Equal((404, "topic_not_found"), (unassigned.StatusCode, unassigned.Error!.Code));
        Assert.Equal(0, db.ReviewSessions.Count());
        Assert.Empty(await db.TestResults.Where(r => r.StudentId == Elif && r.TopicId == "ayt-mat-trigonometri").ToListAsync());
        // Each student only lists the topics of its own group.
        Assert.Equal(8, (await TopicsQuery.GetTopicsAsync(db, Elif, Now, TestOptions.Default)).Topics.Count);
        Assert.Equal(5, (await TopicsQuery.GetTopicsAsync(db, SeedData.DemoStudentId, Now, TestOptions.Default)).Topics.Count);
    }

    [Fact]
    public async Task NormalTest_AfterAMiniQuiz_FollowsTheLatestObservationRules()
    {
        await CompleteReviewAsync(_database, Kerem, Density, 3);
        using var db = _database.CreateContext();
        var results = new TestResultService(db, ClockAt(Now.AddHours(1)));

        // Completed before the quiz: kept, but the quiz stays the latest observation.
        var older = await results.RecordNormalTestAsync(Kerem, Density, new(2, 10, Now.AddHours(-1)));
        // Completed after the quiz: it becomes the latest observation for Kerem only.
        var newer = await results.RecordNormalTestAsync(Kerem, Density, new(2, 10, Now.AddMinutes(30)));

        Assert.Equal((201, false), (older.StatusCode, older.Value!.IsLatest));
        Assert.Equal((201, true), (newer.StatusCode, newer.Value!.IsLatest));
        var topic = await Topic(db, Kerem, Density, Now.AddHours(1));
        Assert.Equal((TestType.NormalTest, Support), (topic.LatestTest!.TestType, topic.ReviewStatus));
        Assert.Equal(Sched, (await Topic(db, Zeynep, Density, Now.AddHours(1))).ReviewStatus);
    }

    private static async Task<TopicSummaryDto> Topic(AppDbContext db, string studentId, string topicId, DateTimeOffset? at = null) =>
        (await TopicsQuery.GetTopicsAsync(db, studentId, at ?? Now, TestOptions.Default)).Topics.Single(t => t.Id == topicId);

    private static (int, int) Score(TopicSummaryDto topic) => (topic.LatestTest!.CorrectCount, topic.LatestTest.TotalQuestions);
}
