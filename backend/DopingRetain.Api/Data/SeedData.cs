using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DopingRetain.Api.Data;

/// <summary>
/// Demo content and initial memory state, ported from src/data/mockTopics.ts.
/// Stability values are calibrated so the first response shows 46 / 55 / 86 / 51 %.
/// </summary>
public static class SeedData
{
    public const string DemoStudentId = "demo-student";

    private sealed record SeedTopic(
        string Id,
        string Title,
        string Category,
        int LastStudiedDaysAgo,
        double Strength,
        int ReviewCount,
        double SuccessRate,
        QuizQuestion[] Questions,
        SummaryPoint[]? SummaryPoints = null);

    private static readonly SeedTopic[] Topics =
    [
        new("ayt-mat-trigonometri", "Trigonometri", "AYT Matematik", LastStudiedDaysAgo: 11, Strength: 14, ReviewCount: 1, SuccessRate: 0.72,
        [
            Question("trig-q1", "ayt-mat-trigonometri", 1,
                "sin 30° · cos 60° + cos 30° · sin 60° işleminin sonucu kaçtır?",
                ["1/2", "√3/2", "0", "1"], correctIndex: 3,
                "sin 30° · cos 60° + cos 30° · sin 60° = sin(30° + 60°) = sin 90° = 1"),
            Question("trig-q2", "ayt-mat-trigonometri", 2,
                "x dar açı ve tan x = 3/4 olduğuna göre, sin x kaçtır?",
                ["3/5", "4/5", "3/4", "5/3"], correctIndex: 0,
                "tan x = 3/4 → kenarlar 3 ve 4, hipotenüs 5 (3-4-5 üçgeni) → sin x = karşı / hipotenüs = 3/5"),
            Question("trig-q3", "ayt-mat-trigonometri", 3,
                "cos 2x ifadesi aşağıdakilerden hangisine eşittir?",
                ["2 · sin x · cos x", "1 − 2sin²x", "sin²x + cos²x", "2cos²x + 1"], correctIndex: 1,
                "cos 2x = cos²x − sin²x; cos²x yerine 1 − sin²x yazılırsa cos 2x = 1 − 2sin²x"),
        ],
        [
            new("📌", "Temel Özdeşlik", "sin²x + cos²x = 1", "Kareler toplamı daima 1’dir."),
            new("💡", "Oran Kuralı", "tan x = sin x / cos x"),
            new("⚡", "Sık Karıştırılan", "sin 30° = cos 60° = 1/2"),
        ]),
        new("tyt-bio-hucre-bolunmeleri", "Hücre Bölünmeleri", "TYT Biyoloji", LastStudiedDaysAgo: 6, Strength: 10, ReviewCount: 2, SuccessRate: 0.65, []),
        new("tyt-tur-sozcukte-anlam", "Sözcükte Anlam", "TYT Türkçe", LastStudiedDaysAgo: 3, Strength: 20, ReviewCount: 4, SuccessRate: 0.88, []),
        new("ayt-mat-fonksiyonlar", "Fonksiyonlar", "AYT Matematik", LastStudiedDaysAgo: 8, Strength: 12, ReviewCount: 2, SuccessRate: 0.6, []),
    ];

    /// <summary>
    /// Creates the schema and seeds the demo data once. An existing database is left untouched,
    /// so restarts never duplicate rows or move LastStudiedAt dates.
    /// </summary>
    public static void Initialize(AppDbContext db, DateTimeOffset seededAtUtc)
    {
        db.Database.EnsureCreated();
        EnsureSchemaIsCurrent(db);
        if (db.Students.Any()) return;

        db.Students.Add(new Student { Id = DemoStudentId, DisplayName = "Demo Öğrenci" });

        foreach (var (seed, index) in Topics.Select((seed, index) => (seed, index)))
        {
            db.Topics.Add(new Topic
            {
                Id = seed.Id,
                Title = seed.Title,
                Category = seed.Category,
                SortOrder = index + 1,
                Questions = [.. seed.Questions],
            });

            var state = new StudentTopicState { StudentId = DemoStudentId, TopicId = seed.Id };
            ApplyInitialState(state, seededAtUtc);
            db.StudentTopicStates.Add(state);
        }

        // One SaveChanges runs in a single transaction, so a seed is never left half-written.
        db.SaveChanges();
    }

    /// <summary>
    /// Micro-review recall points of a topic. Static demo content, served from these definitions
    /// rather than stored in the database.
    /// </summary>
    public static IReadOnlyList<SummaryPoint> GetSummaryPoints(string topicId) =>
        Topics.SingleOrDefault(topic => topic.Id == topicId)?.SummaryPoints ?? [];

    /// <summary>
    /// Sets a demo topic state to its seed values, with LastStudiedAt counted back from
    /// <paramref name="seededAtUtc"/>. Version is left to the caller.
    /// </summary>
    public static void ApplyInitialState(StudentTopicState state, DateTimeOffset seededAtUtc)
    {
        var seed = Topics.Single(topic => topic.Id == state.TopicId);
        state.Strength = seed.Strength;
        state.InitialRecall = 1;
        state.LastStudiedAt = seededAtUtc.ToUniversalTime().AddDays(-seed.LastStudiedDaysAgo);
        state.ReviewCount = seed.ReviewCount;
        state.SuccessRate = seed.SuccessRate;
        state.NeedsFollowUp = false;
    }

    /// <summary>
    /// EnsureCreated leaves an existing file as is, so a database from an earlier schema lacks newer
    /// tables or columns. Fail at startup with a clear message instead of on a later request;
    /// never alter the file.
    /// </summary>
    private static void EnsureSchemaIsCurrent(AppDbContext db)
    {
        var existing = db.Database
            .SqlQuery<string>($"""
                SELECT m.name || '.' || c.name AS Value
                FROM sqlite_master AS m JOIN pragma_table_info(m.name) AS c
                WHERE m.type = 'table'
                """)
            .ToHashSet();
        var missing = db.Model.GetEntityTypes()
            .SelectMany(entity =>
            {
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!);
                return entity.GetProperties().Select(property => $"{table.Name}.{property.GetColumnName(table)}");
            })
            .Where(column => !existing.Contains(column))
            .Order()
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Database '{db.Database.GetDbConnection().DataSource}' was created with an earlier schema " +
                $"and lacks columns: {string.Join(", ", missing)}. Point Database:Path at a new file.");
        }
    }

    private static QuizQuestion Question(
        string id, string topicId, int sortOrder, string text, List<string> options, int correctIndex, string explanation) =>
        new()
        {
            Id = id,
            TopicId = topicId,
            SortOrder = sortOrder,
            Text = text,
            Options = options,
            CorrectIndex = correctIndex,
            Explanation = explanation,
        };
}
