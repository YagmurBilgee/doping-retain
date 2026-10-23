using DopingRetain.Api.Memory;

namespace DopingRetain.Api.Tests;

public class InitialMemoryStateTests
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 10, 21, 12, 30, 0, TimeSpan.FromHours(3));

    private static FirstAttemptTestResult Result(
        int correctCount,
        int questionCount = 10,
        string studentId = "student-1",
        string testId = "test-1",
        string topicId = "ayt-mat-trigonometri") =>
        new(studentId, testId, topicId, CompletedAt, correctCount, questionCount);

    [Theory]
    [InlineData(8, 0.8, 80)]
    [InlineData(0, 0.0, 0)]
    [InlineData(10, 1.0, 100)]
    public void InitialRecall_IsTheScoreRatio_AndRetentionAtElapsedZeroMatchesIt(int correct, double recall, int retention)
    {
        var state = InitialMemoryState.FromFirstAttemptTest(Result(correct));

        Assert.Equal(recall, state.InitialRecall);
        var memory = state.ToMemoryState(CompletedAt);
        Assert.Equal(0, memory.ElapsedDays);
        Assert.Equal(retention, MemoryModel.GetTopicRisk(memory).Retention);
    }

    [Theory]
    [InlineData(0, 100, true)]
    [InlineData(30, 100, true)]
    [InlineData(69, 100, true)]
    [InlineData(70, 100, false)]
    [InlineData(80, 100, false)]
    public void NewTopic_NeedsInitialReinforcementBelowSeventyPercent(int correct, int questions, bool expected)
    {
        var state = InitialMemoryState.FromFirstAttemptTest(Result(correct, questions));

        var risk = MemoryModel.GetTopicRisk(state.ToMemoryState(CompletedAt));

        Assert.Equal(expected, risk.NeedsInitialReinforcement);
        Assert.Equal(correct, risk.Retention);
    }

    [Fact]
    public void WeakFirstReview_KeepsFollowUpAndClearsReinforcement()
    {
        var state = InitialMemoryState.FromFirstAttemptTest(Result(3)).ToMemoryState(CompletedAt);

        var outcome = MemoryModel.ApplyReviewResult(state, quizScore: 0, totalQuestions: 3);
        var after = MemoryModel.GetTopicRisk(outcome.UpdatedState);

        Assert.Equal(ReviewTier.Weak, outcome.Review.Tier);
        Assert.True(outcome.UpdatedState.NeedsFollowUp);
        Assert.Equal((RiskLevel.ReviewSoon, true, false), (after.RiskLevel, after.IsFollowUp, after.NeedsInitialReinforcement));
    }

    [Fact]
    public void InitialRecall_IsNotRoundedToAPercentage()
    {
        var state = InitialMemoryState.FromFirstAttemptTest(Result(correctCount: 2, questionCount: 3));

        Assert.Equal(2.0 / 3, state.InitialRecall);
    }

    [Fact]
    public void OtherFields_StartAsAFreshTopicState()
    {
        var state = InitialMemoryState.FromFirstAttemptTest(Result(8));

        Assert.Equal("student-1", state.StudentId);
        Assert.Equal("ayt-mat-trigonometri", state.TopicId);
        Assert.Equal(InitialMemoryState.DefaultInitialStrength, state.Strength);
        Assert.Equal(14, InitialMemoryState.DefaultInitialStrength);
        Assert.Equal(0, state.ReviewCount);
        Assert.Equal(0, state.SuccessRate);
        Assert.False(state.NeedsFollowUp);
        Assert.Equal(0, state.Version);
    }

    [Fact]
    public void LastStudiedAt_IsTheCompletionInstantInUtc()
    {
        var state = InitialMemoryState.FromFirstAttemptTest(Result(8));

        Assert.Equal(TimeSpan.Zero, state.LastStudiedAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero), state.LastStudiedAt);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    [InlineData(-1, 10)]
    [InlineData(11, 10)]
    public void InvalidCounts_AreRejected(int correct, int questions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InitialMemoryState.FromFirstAttemptTest(Result(correct, questions)));
    }

    [Theory]
    [InlineData("", "test-1", "topic-1")]
    [InlineData("student-1", " ", "topic-1")]
    [InlineData("student-1", "test-1", "")]
    [InlineData(null, "test-1", "topic-1")]
    public void MissingIdentifiers_AreRejected(string? studentId, string? testId, string? topicId)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            InitialMemoryState.FromFirstAttemptTest(Result(8, 10, studentId!, testId!, topicId!)));
    }

    [Fact]
    public void MissingCompletionTime_IsRejected()
    {
        var result = Result(8) with { CompletedAt = default };

        Assert.Throws<ArgumentException>(() => InitialMemoryState.FromFirstAttemptTest(result));
    }
}
