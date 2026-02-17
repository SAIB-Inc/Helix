using System.ComponentModel;
using Helix.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using ModelContextProtocol.Server;

namespace Helix.Tools.Auth;

[McpServerToolType]
public class AuthTools(IPublicClientApplication msalApp, IOptions<HelixOptions> options)
{
    private Task<AuthenticationResult>? _pendingAuth;

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

        string? verificationUrl = null;
        string? userCode = null;

        _pendingAuth = msalApp.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
        {
            verificationUrl = deviceCodeResult.VerificationUrl?.ToString();
            userCode = deviceCodeResult.UserCode;
            return Task.CompletedTask;
        }).ExecuteAsync();

        // Wait briefly for the device code callback to fire
        await Task.Delay(2000).ConfigureAwait(false);

        return $"Tell the user to open {verificationUrl} and enter code: {userCode}\n\n"
            + "Once they complete sign-in, call the 'login-status' tool to confirm authentication.";
    }

    [McpServerTool(Name = "login-status"),
     Description("Check if the user has completed the Microsoft 365 sign-in started by 'login'.")]
    public async Task<string> LoginStatus()
    {
        if (_pendingAuth is null)
            return "No login in progress. Call 'login' first.";

        if (!_pendingAuth.IsCompleted)
        {
            // Give it a moment in case they just finished
            try
            {
                await _pendingAuth.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return "Still waiting for the user to complete sign-in. Ask them to finish the browser authentication, then call 'login-status' again.";
            }
        }

        if (_pendingAuth.IsFaulted)
        {
            var message = _pendingAuth.Exception?.InnerException?.Message ?? "Unknown error";
            _pendingAuth = null;
            return $"Authentication failed: {message}";
        }

        var result = await _pendingAuth.ConfigureAwait(false);
        _pendingAuth = null;
        return $"Authenticated as {result.Account.Username}. Token cached — Microsoft 365 tools are now available.";
    }

    [McpServerTool(Name = "logout"),
     Description("Sign out of Microsoft 365 and clear cached tokens.")]
    public async Task<string> Logout()
    {
        _pendingAuth = null;

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
