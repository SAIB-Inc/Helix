using Azure.Core;
using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Bridges MSAL's IPublicClientApplication to Azure.Core's TokenCredential
/// so it can be used with GraphServiceClient.
/// </summary>
public class HelixTokenCredential(IPublicClientApplication msalApp, string[] scopes) : TokenCredential
{
    /// <inheritdoc />
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        System.Collections.Generic.IEnumerable<IAccount> accounts = msalApp.GetAccountsAsync().GetAwaiter().GetResult();
        IAccount account = accounts.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No cached account found. Run 'helix login' first to authenticate.");

        AuthenticationResult result = msalApp.AcquireTokenSilent(scopes, account)
            .ExecuteAsync(cancellationToken).GetAwaiter().GetResult();

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }

    /// <inheritdoc />
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        System.Collections.Generic.IEnumerable<IAccount> accounts = await msalApp.GetAccountsAsync().ConfigureAwait(false);
        IAccount account = accounts.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No cached account found. Run 'helix login' first to authenticate.");

        AuthenticationResult result = await msalApp.AcquireTokenSilent(scopes, account)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
