using Helix.Core.Auth;
using Helix.Core.Configuration;
using Helix.Core.Extensions;
using Helix.Tools.Users;
using Microsoft.Extensions.Configuration;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using dotenv.net;

// Load .env file before anything else
DotEnv.Load();

// Handle CLI subcommands before starting the MCP server
if (args.Length > 0)
{
    switch (args[0].ToLowerInvariant())
    {
        case "login":
            await HandleLoginAsync(args);
            return;
        case "logout":
            await HandleLogoutAsync(args);
            return;
    }
}

// Start MCP server (stdio transport)
var builder = Host.CreateApplicationBuilder(args);

// Clear default logging providers and route all logs to stderr
// so stdout stays clean for MCP JSON-RPC
builder.Logging.ClearProviders();
builder.Logging.AddConsole(opts =>
{
    opts.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHelixCore(builder.Configuration);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(UserTools).Assembly);

await builder.Build().RunAsync();

// --- CLI Subcommands (outside of MCP) ---

static async Task HandleLoginAsync(string[] args)
{
    var config = BuildConfiguration(args);
    var options = new HelixOptions();
    config.GetSection(HelixOptions.SectionName).Bind(options);

    if (string.IsNullOrEmpty(options.ClientId))
    {
        Console.Error.WriteLine("Error: ClientId is required. Set HELIX__ClientId environment variable.");
        Environment.Exit(1);
    }

    var msalApp = await MsalClientFactory.CreateAsync(options);
    var scopes = CloudConfiguration.GetGraphScopes(options.CloudType);

    Console.Error.WriteLine("Authenticating with Microsoft...");

    var result = await msalApp.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
    {
        Console.Error.WriteLine(deviceCodeResult.Message);
        return Task.CompletedTask;
    }).ExecuteAsync();

    Console.Error.WriteLine($"Authenticated as: {result.Account.Username}");
    Console.Error.WriteLine("Token cached. You can now start the MCP server.");
}

static async Task HandleLogoutAsync(string[] args)
{
    var config = BuildConfiguration(args);
    var options = new HelixOptions();
    config.GetSection(HelixOptions.SectionName).Bind(options);

    if (string.IsNullOrEmpty(options.ClientId))
    {
        Console.Error.WriteLine("Error: ClientId is required. Set HELIX__ClientId environment variable.");
        Environment.Exit(1);
    }

    var msalApp = await MsalClientFactory.CreateAsync(options);
    var accounts = await msalApp.GetAccountsAsync();

    foreach (var account in accounts)
    {
        await msalApp.RemoveAsync(account);
        Console.Error.WriteLine($"Removed account: {account.Username}");
    }

    Console.Error.WriteLine("Logged out. Token cache cleared.");
}

static IConfiguration BuildConfiguration(string[] args)
{
    return new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args.Skip(1).ToArray())
        .Build();
}
