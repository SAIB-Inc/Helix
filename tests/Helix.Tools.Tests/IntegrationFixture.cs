using System.Text.Json;
using dotenv.net;
using Helix.Core.Auth;
using Helix.Core.Configuration;
using Microsoft.Graph;

namespace Helix.Tools.Tests;

public sealed class IntegrationFixture : IAsyncLifetime
{
    public GraphServiceClient GraphClient { get; private set; } = null!;
    public string SiteId { get; private set; } = string.Empty;

    public ValueTask InitializeAsync()
    {
        // Probe from the repo root by walking up from the bin output directory
        var envDir = FindRepoRoot();
        DotEnv.Load(new DotEnvOptions(envFilePaths: [Path.Combine(envDir, ".env")]));

        var options = new HelixOptions
        {
            ClientId = GetEnv("HELIX__ClientId"),
            TenantId = GetEnv("HELIX__TenantId", "common"),
            ClientSecret = Environment.GetEnvironmentVariable("HELIX__ClientSecret"),
            AccessToken = Environment.GetEnvironmentVariable("HELIX__AccessToken"),
        };

        SiteId = GetEnv("HELIX__TestSiteId", "");

        var msalApp = MsalClientFactory.CreateAsync(options).GetAwaiter().GetResult();
        var factory = new HelixGraphClientFactory(options, msalApp);
        GraphClient = factory.Create();

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Assert that the response is not an error.
    /// </summary>
    public static void AssertSuccess(string response)
    {
        using var doc = JsonDocument.Parse(response);
        if (doc.RootElement.TryGetProperty("error", out var err) && err.GetBoolean())
        {
            var message = doc.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
            throw new InvalidOperationException($"Tool returned error: {message}");
        }
    }

    /// <summary>
    /// Assert that the response is a success with no data (e.g. delete operations).
    /// </summary>
    public static void AssertSuccessNoData(string response)
    {
        using var doc = JsonDocument.Parse(response);
        Assert.True(doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean());
    }

    /// <summary>
    /// Assert the response contains a "value" array and return it.
    /// </summary>
    public static JsonElement AssertHasValues(string response)
    {
        AssertSuccess(response);
        using var doc = JsonDocument.Parse(response);
        Assert.True(doc.RootElement.TryGetProperty("value", out var values));
        Assert.Equal(JsonValueKind.Array, values.ValueKind);
        return values.Clone();
    }

    private static string GetEnv(string name, string? fallback = null)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? fallback
            ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, ".env")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Could not find .env file in any parent directory.");
    }
}
