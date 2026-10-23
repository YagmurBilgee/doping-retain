using Microsoft.EntityFrameworkCore;

namespace DopingRetain.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<StudentTopicState> StudentTopicStates => Set<StudentTopicState>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<SessionAnswer> SessionAnswers => Set<SessionAnswer>();
    public DbSet<TestResult> TestResults => Set<TestResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Topic>()
            .HasMany(topic => topic.Questions)
            .WithOne()
            .HasForeignKey(question => question.TopicId);

        modelBuilder.Entity<StudentTopicState>(state =>
        {
            state.HasKey(s => new { s.StudentId, s.TopicId });
            state.HasOne<Student>().WithMany().HasForeignKey(s => s.StudentId);
            state.HasOne(s => s.Topic).WithMany().HasForeignKey(s => s.TopicId);
            state.Property(s => s.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<ReviewSession>(session =>
        {
            // As a concurrency token, a status change only succeeds from the status that was read.
            session.Property(s => s.Status).HasConversion<string>().IsConcurrencyToken();
            session.HasOne<Student>().WithMany().HasForeignKey(s => s.StudentId);
            session.HasOne<Topic>().WithMany().HasForeignKey(s => s.TopicId);
            session.HasMany(s => s.Answers).WithOne().HasForeignKey(a => a.SessionId);
            // At most one open session per student and topic.
            session.HasIndex(s => new { s.StudentId, s.TopicId })
                .IsUnique()
                .HasFilter($"\"Status\" = '{nameof(ReviewSessionStatus.Open)}'");
        });

        modelBuilder.Entity<SessionAnswer>(answer =>
        {
            // The key makes each question answerable once per session.
            answer.HasKey(a => new { a.SessionId, a.QuestionId });
            answer.HasOne<QuizQuestion>().WithMany().HasForeignKey(a => a.QuestionId);
        });

        modelBuilder.Entity<TestResult>(result =>
        {
            result.Property(r => r.TestType).HasConversion<string>();
            result.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId);
            result.HasOne<Topic>().WithMany().HasForeignKey(r => r.TopicId);
            result.HasOne<ReviewSession>().WithMany().HasForeignKey(r => r.ReviewSessionId);
            // One stored result per completed review, however often completion is retried.
            result.HasIndex(r => r.ReviewSessionId).IsUnique();
            result.HasIndex(r => new { r.StudentId, r.TopicId });
            result.ToTable(table => table.HasCheckConstraint(
                "CK_TestResults_Score", "\"TotalQuestions\" > 0 AND \"CorrectCount\" >= 0 AND \"CorrectCount\" <= \"TotalQuestions\""));
        });
    }
}
