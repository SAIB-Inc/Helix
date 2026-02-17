using Helix.Core.Configuration;
using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Creates and configures a MSAL PublicClientApplication with persistent token cache.
/// </summary>
public static class MsalClientFactory
{
    public static async Task<IPublicClientApplication> CreateAsync(HelixOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var authority = new Uri(CloudConfiguration.GetAuthority(options.CloudType, options.TenantId));

        var app = PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithAuthority(authority)
            .Build();

        await TokenCacheHelper.RegisterCacheAsync(app).ConfigureAwait(false);

        return app;
    }
}
