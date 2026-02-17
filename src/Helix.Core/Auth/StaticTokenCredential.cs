using Azure.Core;

namespace Helix.Core.Auth;

/// <summary>
/// A TokenCredential that returns a static access token provided via environment variable.
/// </summary>
public class StaticTokenCredential(string accessToken) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new AccessToken(accessToken, DateTimeOffset.UtcNow.AddHours(1));
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new AccessToken(accessToken, DateTimeOffset.UtcNow.AddHours(1)));
    }
}
