using Azure.Core;
using Helix.Core.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Creates a GraphServiceClient using the appropriate credential based on configuration.
/// </summary>
public class HelixGraphClientFactory(HelixOptions options, IPublicClientApplication? msalApp = null)
{
    public GraphServiceClient Create()
    {
        var credential = CreateCredential();
        var graphEndpoint = CloudConfiguration.GetGraphEndpoint(options.CloudType);
        var scopes = CloudConfiguration.GetGraphScopes(options.CloudType);

        return new GraphServiceClient(credential, scopes, graphEndpoint);
    }

    private TokenCredential CreateCredential()
    {
        // Priority 1: Static access token from env var
        if (!string.IsNullOrEmpty(options.AccessToken))
            return new StaticTokenCredential(options.AccessToken);

        // Priority 2: Client credentials (app-only, no user context)
        if (!string.IsNullOrEmpty(options.ClientSecret))
            return new Azure.Identity.ClientSecretCredential(
                options.TenantId,
                options.ClientId,
                options.ClientSecret);

        // Priority 3: MSAL cached token (from 'helix login')
        if (msalApp is not null)
        {
            var scopes = CloudConfiguration.GetGraphScopes(options.CloudType);
            return new HelixTokenCredential(msalApp, scopes);
        }

        throw new InvalidOperationException(
            "No authentication method configured. Either:\n" +
            "  - Set HELIX__AccessToken environment variable\n" +
            "  - Set HELIX__ClientId and HELIX__ClientSecret for app-only auth\n" +
            "  - Run 'helix login' to authenticate interactively");
    }
}
