namespace DopingRetain.Api.Data;

/// <summary>
/// The sixth-grade demo group: five mock students sharing the eight topics of <see cref="SixthGradeContent"/>,
/// and their initial scenario. The scenario holds only SYNTHETIC normal-test results with their answers
/// (made-up demo data), no mini quizzes or review sessions, so every weekly count starts at zero. Each result
/// is placed a fixed number of days before one reference instant; recommendations are never stored, the
/// retention model evaluates them from these results like any other.
/// </summary>
public static class SixthGradeSeed
{
    public sealed record SeedStudent(string Id, string DisplayName);

    /// <summary>A normal test of <paramref name="CorrectCount"/>/10 completed <paramref name="AgeDays"/> days before the reference instant.</summary>
    public sealed record SeedResult(string StudentId, string TopicId, int CorrectCount, double AgeDays);

    public static readonly SeedStudent[] Students =
    [
        new("grade6-elif", "Elif Kaya"),
        new("grade6-kerem", "Kerem Aydın"),
        new("grade6-zeynep", "Zeynep Demir"),
        new("grade6-mert", "Mert Şahin"),
        new("grade6-ada", "Ada Çelik"),
    ];

    public static IReadOnlyList<string> StudentIds { get; } = Students.Select(student => student.Id).ToList();

    /// <summary>The group's topics in display order.</summary>
    public static IReadOnlyList<string> TopicIds { get; } = SixthGradeContent.Topics.Select(topic => topic.Id).ToList();

    /*
     * Topic columns: T1 274513 Türkçe/Dinleme-İzleme, T2 274523 Türkçe/Yazma, T3 274839 Asal Sayılar,
     * T4 274894 Üçgenin Açıları, T5 275020 Güneş Sistemi, T6 275040 Yoğunluk, T7 274665 Konum Özellikleri,
     * T8 274669 İlk Türk Devletleri. A missing pair has no test history. With h = 10 and threshold 0.50 a
     * result reaches the threshold after 10 · log2(A / 0.5) days (10/10: 10.0, 9/10: 8.5, 8/10: 6.8,
     * 7/10: 4.9, 6/10: 2.6); 5/10 or less needs support at once. Every age keeps more than a day from
     * those crossings, so the initial states do not flip within the first day.
     */
    public static readonly SeedResult[] Results =
    [
        R("grade6-elif", "274513", 9, 2), R("grade6-elif", "274523", 7, 7), R("grade6-elif", "274839", 8, 3),
        R("grade6-elif", "274894", 4, 1), R("grade6-elif", "275020", 9, 5), R("grade6-elif", "274665", 10, 14),
        R("grade6-elif", "274669", 6, 6),

        R("grade6-kerem", "274513", 6, 1), R("grade6-kerem", "274523", 8, 10), R("grade6-kerem", "274839", 8, 9),
        R("grade6-kerem", "274894", 10, 4), R("grade6-kerem", "275040", 5, 3), R("grade6-kerem", "274665", 7, 2),
        R("grade6-kerem", "274669", 9, 12),

        R("grade6-zeynep", "274523", 10, 3), R("grade6-zeynep", "274839", 7, 8), R("grade6-zeynep", "274894", 6, 5),
        R("grade6-zeynep", "275020", 6, 5), R("grade6-zeynep", "275040", 9, 2), R("grade6-zeynep", "274665", 4, 6),
        R("grade6-zeynep", "274669", 8, 2),

        R("grade6-mert", "274513", 8, 9), R("grade6-mert", "274523", 5, 2), R("grade6-mert", "274894", 9, 12),
        R("grade6-mert", "275020", 4, 5), R("grade6-mert", "275040", 7, 1), R("grade6-mert", "274665", 0, 3),
        R("grade6-mert", "274669", 10, 5),

        R("grade6-ada", "274513", 10, 8), R("grade6-ada", "274523", 8, 8), R("grade6-ada", "274839", 6, 5),
        R("grade6-ada", "275020", 8, 10), R("grade6-ada", "275040", 3, 4), R("grade6-ada", "274665", 6, 1),
        R("grade6-ada", "274669", 4, 2),
    ];

    private static SeedResult R(string studentId, string topicId, int correct, double ageDays) => new(studentId, topicId, correct, ageDays);

    public static IEnumerable<Student> CreateStudents() =>
        Students.Select((student, index) => new Student
        {
            Id = student.Id,
            DisplayName = student.DisplayName,
            Group = DemoGroup.SixthGrade,
            SortOrder = index + 1,
        });

    /// <param name="firstSortOrder">Topics follow the existing content in display order.</param>
    public static IEnumerable<Topic> CreateTopics(int firstSortOrder) =>
        SixthGradeContent.Topics.Select((topic, index) => new Topic
        {
            Id = topic.Id,
            Title = topic.Title,
            Category = topic.Subject,
            Group = DemoGroup.SixthGrade,
            UnitId = topic.UnitId,
            UnitName = topic.UnitName,
            SortOrder = firstSortOrder + index,
            Questions = [.. CreateMiniQuizQuestions(topic)],
        });

    public static IEnumerable<QuizQuestion> CreateMiniQuizQuestions(SixthGradeContent.ContentTopic topic) =>
        topic.Reminders.Select((reminder, index) => new QuizQuestion
        {
            Id = SixthGradeContent.MiniQuizQuestionId(topic.Id, index + 1),
            TopicId = topic.Id,
            // Question i checks reminder i: the order links them.
            SortOrder = index + 1,
            Text = reminder.Question.Text,
            Options = [.. reminder.Question.Options],
            CorrectIndex = reminder.Question.CorrectIndex,
            Explanation = reminder.Question.Explanation,
        });

    public static IEnumerable<NormalTestQuestion> CreateNormalTestQuestions() =>
        SixthGradeContent.Topics.SelectMany(topic => topic.NormalTest.Select((question, index) => new NormalTestQuestion
        {
            Id = SixthGradeContent.NormalQuestionId(topic.Id, index + 1),
            TopicId = topic.Id,
            SortOrder = index + 1,
            Text = question.Text,
            Options = [.. question.Options],
            CorrectIndex = question.CorrectIndex,
        }));

    public static IEnumerable<StudentTopicState> CreateStates() =>
        StudentIds.SelectMany(studentId => TopicIds.Select(topicId => new StudentTopicState { StudentId = studentId, TopicId = topicId }));

    /// <summary>
    /// The scenario's normal-test results for one reference instant, each with ten answers whose correct
    /// count is the result's score. Which questions are wrong varies by student and topic.
    /// </summary>
    public static IEnumerable<TestResult> CreateResults(DateTimeOffset referenceUtc)
    {
        var now = referenceUtc.ToUniversalTime();
        return Results.Select(seed =>
        {
            var topic = SixthGradeContent.Topics.Single(t => t.Id == seed.TopicId);
            var total = topic.NormalTest.Length;
            var wrongCount = total - seed.CorrectCount;
            var offset = (Array.IndexOf(Students, Students.Single(s => s.Id == seed.StudentId)) * 3
                + Array.IndexOf(SixthGradeContent.Topics, topic) * 7) % total;

            return new TestResult
            {
                StudentId = seed.StudentId,
                TopicId = seed.TopicId,
                TestType = TestType.NormalTest,
                CompletedAt = now.AddDays(-seed.AgeDays),
                CorrectCount = seed.CorrectCount,
                TotalQuestions = total,
                NormalTestAnswers = topic.NormalTest
                    .Select((question, index) =>
                    {
                        var isWrong = (index - offset + total) % total < wrongCount;
                        return new NormalTestAnswer
                        {
                            QuestionId = SixthGradeContent.NormalQuestionId(topic.Id, index + 1),
                            SelectedIndex = isWrong ? (question.CorrectIndex + 1) % question.Options.Length : question.CorrectIndex,
                            IsCorrect = !isWrong,
                        };
                    })
                    .ToList(),
            };
        }).ToList();
    }

    /// <summary>Hap Bilgi reminders of a sixth-grade topic, or null for a topic outside the group.</summary>
    public static IReadOnlyList<SummaryPoint>? GetSummaryPoints(string topicId) =>
        SixthGradeContent.Topics.SingleOrDefault(topic => topic.Id == topicId)?.Reminders.Select(reminder => reminder.Point).ToList();
}
