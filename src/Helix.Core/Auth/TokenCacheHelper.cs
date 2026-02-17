using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Helix.Core.Auth;

public static class TokenCacheHelper
{
    private const string CacheFileName = "helix-token-cache.bin";

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Helix");

    public static async Task RegisterCacheAsync(IPublicClientApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var builder = new StorageCreationPropertiesBuilder(CacheFileName, CacheDirectory);

        // Linux requires keyring configuration for encrypted storage
        if (OperatingSystem.IsLinux())
        {
            builder.WithLinuxKeyring(
                schemaName: "com.helix.tokencache",
                collection: "default",
                secretLabel: "Helix MCP Server token cache",
                attribute1: new KeyValuePair<string, string>("application", "helix"),
                attribute2: new KeyValuePair<string, string>("version", "1"));
        }
        else if (OperatingSystem.IsMacOS())
        {
            builder.WithMacKeyChain(
                serviceName: "Helix",
                accountName: "HelixTokenCache");
        }

        var storageProperties = builder.Build();

        MsalCacheHelper cacheHelper;
        try
        {
            cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
            cacheHelper.VerifyPersistence();
        }
        catch (MsalCachePersistenceException)
        {
            // If keyring/keychain isn't available, fall back to unencrypted file.
            // WithUnprotectedFile() works on all platforms (Linux, macOS, Windows).
            var fallbackProperties = new StorageCreationPropertiesBuilder(CacheFileName, CacheDirectory)
                .WithUnprotectedFile()
                .Build();

            cacheHelper = await MsalCacheHelper.CreateAsync(fallbackProperties).ConfigureAwait(false);
        }

        cacheHelper.RegisterCache(app.UserTokenCache);
    }
}
