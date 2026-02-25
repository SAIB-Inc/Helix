using Helix.Core.Auth;
using Helix.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;

namespace Helix.Core.Extensions;

/// <summary>
/// Extension methods to register Helix core services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Helix authentication, Graph client factory, and related services to the container.
    /// </summary>
    public static IServiceCollection AddHelixCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _ = services.Configure<HelixOptions>(configuration.GetSection(HelixOptions.SectionName));

        HelixOptions options = new();
        configuration.GetSection(HelixOptions.SectionName).Bind(options);

        // Build MSAL app if we have a client ID (needed for cached token auth)
        IPublicClientApplication? msalApp = null;
        if (!string.IsNullOrEmpty(options.ClientId))
        {
            msalApp = MsalClientFactory.CreateAsync(options).GetAwaiter().GetResult();
            _ = services.AddSingleton(msalApp);
        }

        HelixGraphClientFactory factory = new(options, msalApp);
        _ = services.AddSingleton(factory);
        _ = services.AddSingleton(_ => factory.Create());

        return services;
    }
}
