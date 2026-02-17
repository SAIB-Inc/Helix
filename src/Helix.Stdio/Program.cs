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
    switch (args[0].ToUpperInvariant())
    {
        case "--VERSION":
        case "-V":
            var version = System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(Program).Assembly)?
                .InformationalVersion ?? "0.0.0-dev";
            Console.WriteLine($"helix {version}");
            return;
        case "LOGIN":
            await HandleLoginAsync(args).ConfigureAwait(false);
            return;
        case "LOGOUT":
            await HandleLogoutAsync(args).ConfigureAwait(false);
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

await builder.Build().RunAsync().ConfigureAwait(false);

// --- CLI Subcommands (outside of MCP) ---

static async Task HandleLoginAsync(string[] args)
{
    var config = BuildConfiguration(args);
    var options = new HelixOptions();
    config.GetSection(HelixOptions.SectionName).Bind(options);

    if (string.IsNullOrEmpty(options.ClientId))
    {
        await Console.Error.WriteLineAsync("Error: ClientId is required. Set HELIX__ClientId environment variable.").ConfigureAwait(false);
        Environment.Exit(1);
    }

    var msalApp = await MsalClientFactory.CreateAsync(options).ConfigureAwait(false);
    var scopes = CloudConfiguration.GetGraphScopes(options.CloudType);

    await Console.Error.WriteLineAsync("Authenticating with Microsoft...").ConfigureAwait(false);

    var result = await msalApp.AcquireTokenWithDeviceCode(scopes, async deviceCodeResult =>
    {
        await Console.Error.WriteLineAsync(deviceCodeResult.Message).ConfigureAwait(false);
    }).ExecuteAsync().ConfigureAwait(false);

    await Console.Error.WriteLineAsync($"Authenticated as: {result.Account.Username}").ConfigureAwait(false);
    await Console.Error.WriteLineAsync("Token cached. You can now start the MCP server.").ConfigureAwait(false);
}

static async Task HandleLogoutAsync(string[] args)
{
    var config = BuildConfiguration(args);
    var options = new HelixOptions();
    config.GetSection(HelixOptions.SectionName).Bind(options);

    if (string.IsNullOrEmpty(options.ClientId))
    {
        await Console.Error.WriteLineAsync("Error: ClientId is required. Set HELIX__ClientId environment variable.").ConfigureAwait(false);
        Environment.Exit(1);
    }

    var msalApp = await MsalClientFactory.CreateAsync(options).ConfigureAwait(false);
    var accounts = await msalApp.GetAccountsAsync().ConfigureAwait(false);

    foreach (var account in accounts)
    {
        await msalApp.RemoveAsync(account).ConfigureAwait(false);
        await Console.Error.WriteLineAsync($"Removed account: {account.Username}").ConfigureAwait(false);
    }

    await Console.Error.WriteLineAsync("Logged out. Token cache cleared.").ConfigureAwait(false);
}

static IConfiguration BuildConfiguration(string[] args)
{
    return new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args[1..])
        .Build();
}
