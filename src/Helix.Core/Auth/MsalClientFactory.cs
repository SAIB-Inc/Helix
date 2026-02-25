using Helix.Core.Configuration;
using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Creates and configures a MSAL PublicClientApplication with persistent token cache.
/// </summary>
public static class MsalClientFactory
{
    /// <summary>
    /// Creates a configured <see cref="IPublicClientApplication"/> with persistent token cache.
    /// </summary>
    public static Task<IPublicClientApplication> CreateAsync(HelixOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Uri authority = new(CloudConfiguration.GetAuthority(options.CloudType, options.TenantId));

        IPublicClientApplication app = PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithAuthority(authority)
            .Build();

        TokenCacheHelper.RegisterCache(app);

        return Task.FromResult(app);
    }
}
