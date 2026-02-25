using System.ComponentModel;
using Helix.Core.Auth;
using Helix.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using ModelContextProtocol.Server;

namespace Helix.Tools.Auth;

/// <summary>
/// MCP tools for Microsoft 365 authentication (login, login-status, logout).
/// </summary>
/// <param name="msalApp">The MSAL public client application used for authentication.</param>
/// <param name="options">Helix configuration options.</param>
[McpServerToolType]
public class AuthTools(IPublicClientApplication msalApp, IOptions<HelixOptions> options)
{
    /// <inheritdoc />
    [McpServerTool(Name = "login"),
     Description("Start Microsoft 365 authentication. Returns a URL and code for the user to open in their browser. "
        + "After the user completes sign-in, call 'login-status' to confirm.")]
    public async Task<string> Login()
    {
        string[] scopes = CloudConfiguration.GetGraphScopes(options.Value.CloudType);

        // Check if already authenticated
        System.Collections.Generic.IEnumerable<IAccount> accounts = await msalApp.GetAccountsAsync().ConfigureAwait(false);
        IAccount? account = accounts.FirstOrDefault();
        if (account is not null)
        {
            try
            {
                _ = await msalApp.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync().ConfigureAwait(false);
                return $"Already authenticated as {account.Username}. Use the 'logout' tool first to switch accounts.";
            }
            catch (MsalUiRequiredException)
            {
                // Token expired, proceed with device code flow
            }
        }

        TaskCompletionSource<DeviceCodeResult> deviceCodeReady = new();

        LoginSession.PendingAuth = msalApp.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
        {
            _ = deviceCodeReady.TrySetResult(deviceCodeResult);
            return Task.CompletedTask;
        }).ExecuteAsync();

        // Wait for MSAL to return the device code (no arbitrary delay)
        DeviceCodeResult code = await deviceCodeReady.Task.ConfigureAwait(false);

        return $"Tell the user to open {code.VerificationUrl} and enter code: {code.UserCode}\n\n"
            + "Once they complete sign-in, call the 'login-status' tool to confirm authentication.";
    }

    /// <inheritdoc />
    [McpServerTool(Name = "login-status"),
     Description("Check if the user has completed the Microsoft 365 sign-in started by 'login'.")]
    public static async Task<string> LoginStatus()
    {
        Task<AuthenticationResult>? pending = LoginSession.PendingAuth;

        if (pending is null)
        {
            return "No login in progress. Call 'login' first.";
        }

        if (!pending.IsCompleted)
        {
            return "Still waiting for the user to complete sign-in. Ask them to finish the browser authentication, then call 'login-status' again.";
        }

        if (pending.IsFaulted)
        {
            string message = pending.Exception?.InnerException?.Message ?? "Unknown error";
            LoginSession.PendingAuth = null;
            return $"Authentication failed: {message}";
        }

        AuthenticationResult result = await pending.ConfigureAwait(false);
        LoginSession.PendingAuth = null;
        return $"Authenticated as {result.Account.Username}. Token cached — Microsoft 365 tools are now available.";
    }

    /// <inheritdoc />
    [McpServerTool(Name = "logout"),
     Description("Sign out of Microsoft 365 and clear cached tokens.")]
    public async Task<string> Logout()
    {
        LoginSession.PendingAuth = null;

        System.Collections.Generic.IEnumerable<IAccount> accounts = await msalApp.GetAccountsAsync().ConfigureAwait(false);
        int removed = 0;

        foreach (IAccount account in accounts)
        {
            await msalApp.RemoveAsync(account).ConfigureAwait(false);
            removed++;
        }

        // Delete the token cache file to ensure stale scopes are fully cleared
        TokenCacheHelper.ClearCache();

        return removed > 0
            ? $"Logged out. Removed {removed} cached account(s) and cleared token cache."
            : "No accounts were cached. Token cache cleared.";
    }
}
