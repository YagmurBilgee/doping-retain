using System.Text.Json;
using DopingRetain.Api.Data;
using DopingRetain.Api.Demo;
using DopingRetain.Api.Topics;

namespace DopingRetain.Api.Tests;

public sealed class DemoClockTests
{
    private const string FixedUtcNow = "2026-10-21T09:00:00Z";
    private static readonly DateTimeOffset FixedInstant = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FixedClock_SeedsAndEvaluatesFreshDatabasesIdentically()
    {
        var first = await SeedAndReadTopicsJson(DemoClock.Create(FixedUtcNow, isDevelopment: true));
        var second = await SeedAndReadTopicsJson(DemoClock.Create(FixedUtcNow, isDevelopment: true));

        Assert.Equal(first, second);
        Assert.Contains("\"evaluatedAt\":\"2026-10-21T09:00:00+00:00\"", first);
        Assert.Contains("\"dueTopicIds\":[\"ayt-mat-trigonometri\",\"tyt-bio-hucre-bolunmeleri\",\"tyt-fiz-hareket-ve-kuvvet\"]", first);
    }

    [Fact]
    public void FixedClock_ReturnsTheSameUtcInstantOnEveryCall()
    {
        var clock = DemoClock.Create("2026-10-21T12:00:00+03:00", isDevelopment: true);

        Assert.Equal(FixedInstant, clock.GetUtcNow());
        Assert.Equal(TimeSpan.Zero, clock.GetUtcNow().Offset);
        Assert.Equal(clock.GetUtcNow(), clock.GetUtcNow());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutFixedValue_UsesSystemClockInAnyEnvironment(string? value)
    {
        Assert.Same(TimeProvider.System, DemoClock.Create(value, isDevelopment: false));
        Assert.Same(TimeProvider.System, DemoClock.Create(value, isDevelopment: true));
    }

    [Fact]
    public void Create_OutsideDevelopment_RejectsFixedClock()
    {
        var error = Assert.Throws<InvalidOperationException>(() => DemoClock.Create(FixedUtcNow, isDevelopment: false));

        Assert.Contains("only supported in the Development environment", error.Message);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-10-21T09:00:00")] // no offset
    [InlineData("2026-13-01T09:00:00Z")]
    [InlineData("21.10.2026 09:00")]
    public void Create_WithInvalidTimestamp_FailsClearly(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => DemoClock.Create(value, isDevelopment: true));

        Assert.Contains($"'{value}' is not a valid timestamp", error.Message);
    }

    [Fact]
    public void EnsureNotBeforeRecordedTests_RejectsEarlierFixedTimeWithoutChangingRecords()
    {
        using var database = new TemporaryDatabase();
        using (var db = database.CreateContext()) SeedData.Initialize(db, FixedInstant);
        var before = ReadTestDates(database);

        using (var db = database.CreateContext())
        {
            var error = Assert.Throws<InvalidOperationException>(
                // The latest seeded test (Kimya, 22/25) was completed about 5.4 days before seeding.
                () => DemoClock.EnsureNotBeforeRecordedTests(db, FixedInstant.AddDays(-6)));
            Assert.Contains("tyt-kim-atom-ve-periyodik-sistem", error.Message);
        }

        Assert.Equal(before, ReadTestDates(database));
    }

    [Fact]
    public void EnsureNotBeforeRecordedTests_AcceptsFixedTimeAtOrAfterRecordedTests()
    {
        using var database = new TemporaryDatabase();
        using var db = database.CreateContext();
        SeedData.Initialize(db, FixedInstant);

        DemoClock.EnsureNotBeforeRecordedTests(db, FixedInstant);
    }

    private static async Task<string> SeedAndReadTopicsJson(TimeProvider clock)
    {
        using var database = new TemporaryDatabase();
        using (var db = database.CreateContext()) SeedData.Initialize(db, clock.GetUtcNow());

        using var context = database.CreateContext();
        var response = await TopicsQuery.GetTopicsAsync(context, SeedData.DemoStudentId, clock.GetUtcNow(), TestOptions.Default);
        return JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static Dictionary<string, DateTimeOffset> ReadTestDates(TemporaryDatabase database)
    {
        using var db = database.CreateContext();
        return db.TestResults.ToDictionary(result => $"{result.StudentId}|{result.TopicId}", result => result.CompletedAt);
    }
}
