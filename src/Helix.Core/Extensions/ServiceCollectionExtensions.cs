using Helix.Core.Auth;
using Helix.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace Helix.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHelixCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HelixOptions>(configuration.GetSection(HelixOptions.SectionName));

        var options = new HelixOptions();
        configuration.GetSection(HelixOptions.SectionName).Bind(options);

        // Build MSAL app if we have a client ID (needed for cached token auth)
        IPublicClientApplication? msalApp = null;
        if (!string.IsNullOrEmpty(options.ClientId))
        {
            msalApp = MsalClientFactory.CreateAsync(options).GetAwaiter().GetResult();
            services.AddSingleton(msalApp);
        }

        var factory = new HelixGraphClientFactory(options, msalApp);
        services.AddSingleton(factory);
        services.AddSingleton(_ => factory.Create());

        return services;
    }
}
