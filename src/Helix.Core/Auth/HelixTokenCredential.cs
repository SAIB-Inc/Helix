using Azure.Core;
using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Bridges MSAL's IPublicClientApplication to Azure.Core's TokenCredential
/// so it can be used with GraphServiceClient.
/// </summary>
public class HelixTokenCredential : TokenCredential
{
    private readonly IPublicClientApplication _msalApp;
    private readonly string[] _scopes;

    public HelixTokenCredential(IPublicClientApplication msalApp, string[] scopes)
    {
        _msalApp = msalApp;
        _scopes = scopes;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var accounts = await _msalApp.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        if (account is null)
            throw new InvalidOperationException(
                "No cached account found. Run 'helix login' first to authenticate.");

        var result = await _msalApp.AcquireTokenSilent(_scopes, account)
            .ExecuteAsync(cancellationToken);

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
