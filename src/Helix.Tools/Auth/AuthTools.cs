using System.ComponentModel;
using System.Globalization;
using System.Text;
using Helix.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using ModelContextProtocol.Server;

namespace Helix.Tools.Auth;

[McpServerToolType]
public class AuthTools(IPublicClientApplication msalApp, IOptions<HelixOptions> options)
{
    [McpServerTool(Name = "login"),
     Description("Authenticate with Microsoft 365 using device code flow. "
        + "This will return a URL and code that the user must open in their browser to complete sign-in. "
        + "The tool will wait until authentication is complete.")]
    public async Task<string> Login()
    {
        var scopes = CloudConfiguration.GetGraphScopes(options.Value.CloudType);

        // Check if already authenticated
        var accounts = await msalApp.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();
        if (account is not null)
        {
            try
            {
                await msalApp.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync().ConfigureAwait(false);
                return $"Already authenticated as {account.Username}. Use the 'logout' tool first to switch accounts.";
            }
            catch (MsalUiRequiredException)
            {
                // Token expired, proceed with device code flow
            }
        }

        var sb = new StringBuilder();

        var result = await msalApp.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
        {
            sb.AppendLine("To sign in, the user must:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"1. Open: {deviceCodeResult.VerificationUrl}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"2. Enter code: {deviceCodeResult.UserCode}");
            sb.AppendLine();
            sb.AppendLine("Tell the user to complete these steps. Waiting for authentication...");
            return Task.CompletedTask;
        }).ExecuteAsync().ConfigureAwait(false);

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Authenticated as {result.Account.Username}. Token cached — Microsoft 365 tools are now available.");

        return sb.ToString();
    }

    [McpServerTool(Name = "logout"),
     Description("Sign out of Microsoft 365 and clear cached tokens.")]
    public async Task<string> Logout()
    {
        var accounts = await msalApp.GetAccountsAsync().ConfigureAwait(false);
        var removed = 0;

        foreach (var account in accounts)
        {
            await msalApp.RemoveAsync(account).ConfigureAwait(false);
            removed++;
        }

        return removed > 0
            ? $"Logged out. Removed {removed} cached account(s)."
            : "No accounts were cached. Already logged out.";
    }
}
