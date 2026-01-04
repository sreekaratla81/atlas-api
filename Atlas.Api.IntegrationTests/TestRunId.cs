namespace Atlas.Api.IntegrationTests;

public static class TestRunId
{
    private static readonly Lazy<string> CachedValue = new(() =>
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ATLAS_TEST_RUN_ID");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        return DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
    });

    public static string Value => CachedValue.Value;
}
