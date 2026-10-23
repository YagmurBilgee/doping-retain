using DopingRetain.Api.Memory;

namespace DopingRetain.Api.Tests;

/// <summary>R(t) = A · 2^(−t / h) and its threshold schedule, with the default h = 10 days and threshold 0.50.</summary>
public sealed class RetentionModelTests
{
    private const double Tolerance = 1e-12;
    private static readonly DateTimeOffset TestCompletedAt = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly RetentionModelOptions Options = TestOptions.Default;

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.8)]
    [InlineData(0.35)]
    public void Recall_IsAAtZeroAndHalfOfAAfterOneHalfLife(double accuracy)
    {
        Assert.Equal(accuracy, RetentionModel.Recall(accuracy, 0, Options), Tolerance);
        Assert.Equal(accuracy / 2, RetentionModel.Recall(accuracy, Options.HalfLifeDays, Options), Tolerance);
        Assert.Equal(accuracy / 4, RetentionModel.Recall(accuracy, 2 * Options.HalfLifeDays, Options), Tolerance);
    }

    [Fact]
    public void Recall_CountsNegativeElapsedTimeAsZero() =>
        Assert.Equal(0.8, RetentionModel.Recall(0.8, -3, Options), Tolerance);

    [Fact]
    public void ZeroScore_NeedsSupportNowWithZeroRecall()
    {
        var estimate = Evaluate(correct: 0, total: 3, daysLater: 0);

        Assert.Equal((ReviewStatus.SupportNeeded, (double?)0, (DateTimeOffset?)TestCompletedAt), (estimate.Status, estimate.Recall, estimate.ReviewAt));
    }

    [Theory]
    [InlineData(1, 3)]   // below the threshold
    [InlineData(5, 10)]  // exactly at the threshold
    public void ScoreAtOrBelowThreshold_NeedsSupportNow_NeverANegativeInterval(int correct, int total)
    {
        var estimate = Evaluate(correct, total, daysLater: 2);

        Assert.Null(RetentionModel.DaysUntilThreshold((double)correct / total, Options));
        Assert.Equal(ReviewStatus.SupportNeeded, estimate.Status);
        Assert.Equal(TestCompletedAt, estimate.ReviewAt);
    }

    [Theory]
    [InlineData(10, 10, 10.0)]                  // log2(1 / 0.5) = 1 half-life
    [InlineData(8, 10, 6.780719051126377)]      // 10 · log2(1.6)
    [InlineData(51, 100, 0.2856915219677092)]   // just above the threshold: a short but positive interval
    public void ReviewTime_IsWhereTheSameCurveReachesTheThreshold(int correct, int total, double expectedDays)
    {
        var accuracy = (double)correct / total;
        var days = RetentionModel.DaysUntilThreshold(accuracy, Options)!.Value;

        Assert.Equal(expectedDays, days, 1e-9);
        Assert.Equal(Options.ReviewThreshold, RetentionModel.Recall(accuracy, days, Options), 1e-12);
        Assert.Equal(TestCompletedAt.AddDays(days), Evaluate(correct, total, daysLater: 0).ReviewAt);
    }

    [Fact]
    public void Status_IsScheduledBeforeTheCrossingAndDueFromIt()
    {
        // 8/10: the crossing is ≈ 6.78 days after the test.
        Assert.Equal(ReviewStatus.Scheduled, Evaluate(8, 10, daysLater: 6.7).Status);
        Assert.Equal(ReviewStatus.Due, Evaluate(8, 10, daysLater: 6.8).Status);
        Assert.Equal(ReviewStatus.Due, Evaluate(8, 10, daysLater: 30).Status);
    }

    [Fact]
    public void NoTest_IsInsufficientData()
    {
        var estimate = RetentionModel.Evaluate(null, TestCompletedAt, Options);

        Assert.Equal(new RetentionEstimate(ReviewStatus.InsufficientData, null, null, null), estimate);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(-1, 3)]
    [InlineData(4, 3)]
    [InlineData(0, -2)]
    public void InvalidScores_AreRejected(int correct, int total) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestObservation(TestCompletedAt, correct, total));

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(-1, 0.5)]
    [InlineData(double.NaN, 0.5)]
    [InlineData(10, 0)]
    [InlineData(10, 1)]
    [InlineData(10, 1.5)]
    public void InvalidOptions_AreRejected(double halfLifeDays, double reviewThreshold)
    {
        var options = new RetentionModelOptions { HalfLifeDays = halfLifeDays, ReviewThreshold = reviewThreshold };

        Assert.Throws<InvalidOperationException>(() => options.Validated());
    }

    [Fact]
    public void DefaultOptions_AreTheDocumentedDemoAssumptions() =>
        Assert.Equal((10d, 0.5), (Options.Validated().HalfLifeDays, Options.ReviewThreshold));

    private static RetentionEstimate Evaluate(int correct, int total, double daysLater) =>
        RetentionModel.Evaluate(new TestObservation(TestCompletedAt, correct, total), TestCompletedAt.AddDays(daysLater), Options);
}
