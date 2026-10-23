using DopingRetain.Api.Memory;

namespace DopingRetain.Api.Tests;

/// <summary>The default model settings (h = 10 days, threshold 0.50) that the app uses unless configured.</summary>
internal static class TestOptions
{
    public static readonly RetentionModelOptions Default = new();
}
