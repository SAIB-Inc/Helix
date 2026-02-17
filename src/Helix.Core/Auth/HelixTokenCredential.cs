using Azure.Core;
using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Bridges MSAL's IPublicClientApplication to Azure.Core's TokenCredential
/// so it can be used with GraphServiceClient.
/// </summary>
public class HelixTokenCredential(IPublicClientApplication msalApp, string[] scopes) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var accounts = msalApp.GetAccountsAsync().GetAwaiter().GetResult();
        var account = accounts.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No cached account found. Run 'helix login' first to authenticate.");

        var result = msalApp.AcquireTokenSilent(scopes, account)
            .ExecuteAsync(cancellationToken).GetAwaiter().GetResult();

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var accounts = await msalApp.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No cached account found. Run 'helix login' first to authenticate.");

        var result = await msalApp.AcquireTokenSilent(scopes, account)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
