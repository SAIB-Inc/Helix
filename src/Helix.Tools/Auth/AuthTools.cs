using System.ComponentModel;
using Helix.Core.Auth;
using Helix.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using ModelContextProtocol.Server;

namespace Helix.Tools.Auth;

[McpServerToolType]
public class AuthTools(IPublicClientApplication msalApp, IOptions<HelixOptions> options)
{
    [McpServerTool(Name = "login"),
     Description("Start Microsoft 365 authentication. Returns a URL and code for the user to open in their browser. "
        + "After the user completes sign-in, call 'login-status' to confirm.")]
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

        var deviceCodeReady = new TaskCompletionSource<DeviceCodeResult>();

        LoginSession.PendingAuth = msalApp.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
        {
            deviceCodeReady.TrySetResult(deviceCodeResult);
            return Task.CompletedTask;
        }).ExecuteAsync();

        // Wait for MSAL to return the device code (no arbitrary delay)
        var code = await deviceCodeReady.Task.ConfigureAwait(false);

        return $"Tell the user to open {code.VerificationUrl} and enter code: {code.UserCode}\n\n"
            + "Once they complete sign-in, call the 'login-status' tool to confirm authentication.";
    }

    [McpServerTool(Name = "login-status"),
     Description("Check if the user has completed the Microsoft 365 sign-in started by 'login'.")]
    public static async Task<string> LoginStatus()
    {
        var pending = LoginSession.PendingAuth;

        if (pending is null)
            return "No login in progress. Call 'login' first.";

        if (!pending.IsCompleted)
            return "Still waiting for the user to complete sign-in. Ask them to finish the browser authentication, then call 'login-status' again.";

        if (pending.IsFaulted)
        {
            var message = pending.Exception?.InnerException?.Message ?? "Unknown error";
            LoginSession.PendingAuth = null;
            return $"Authentication failed: {message}";
        }

        var result = await pending.ConfigureAwait(false);
        LoginSession.PendingAuth = null;
        return $"Authenticated as {result.Account.Username}. Token cached — Microsoft 365 tools are now available.";
    }

    [McpServerTool(Name = "logout"),
     Description("Sign out of Microsoft 365 and clear cached tokens.")]
    public async Task<string> Logout()
    {
        LoginSession.PendingAuth = null;

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
