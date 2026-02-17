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
            cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.VerifyPersistence();
        }
        catch
        {
            // If keyring/keychain isn't available, fall back to plain file
            var fallbackProperties = new StorageCreationPropertiesBuilder(CacheFileName, CacheDirectory)
                .WithLinuxUnprotectedFile()
                .Build();

            cacheHelper = await MsalCacheHelper.CreateAsync(fallbackProperties);
        }

        cacheHelper.RegisterCache(app.UserTokenCache);
    }
}
