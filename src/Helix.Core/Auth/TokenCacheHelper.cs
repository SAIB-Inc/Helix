using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Persists the MSAL token cache to a plain file on disk.
/// Uses SetBeforeAccessAsync/SetAfterAccessAsync callbacks instead of
/// MsalCacheHelper to avoid macOS Keychain and P/Invoke issues with
/// trimmed self-contained binaries.
/// </summary>
public static class TokenCacheHelper
{
    private const string CacheFileName = "helix-token-cache.bin";

    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Helix",
        CacheFileName);

    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    public static void RegisterCache(IPublicClientApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UserTokenCache.SetBeforeAccessAsync(BeforeAccessAsync);
        app.UserTokenCache.SetAfterAccessAsync(AfterAccessAsync);
    }

    /// <summary>
    /// Deletes the token cache file from disk. Called during logout to ensure
    /// stale tokens with old scopes are fully cleared.
    /// </summary>
    public static void ClearCache()
    {
        try
        {
            if (File.Exists(CacheFilePath))
                File.Delete(CacheFilePath);
        }
        catch (IOException)
        {
            // Best effort
        }
    }

    private static async Task BeforeAccessAsync(TokenCacheNotificationArgs args)
    {
        await SyncLock.WaitAsync().ConfigureAwait(false);

        if (!File.Exists(CacheFilePath))
        {
            return;
        }

        try
        {
            var data = await File.ReadAllBytesAsync(CacheFilePath).ConfigureAwait(false);
            args.TokenCache.DeserializeMsalV3(data);
        }
        catch (IOException)
        {
            // Corrupt or locked cache file — delete and continue with empty cache
            try { File.Delete(CacheFilePath); }
            catch (IOException) { /* best effort */ }
        }
    }

    private static async Task AfterAccessAsync(TokenCacheNotificationArgs args)
    {
        try
        {
            if (!args.HasStateChanged)
            {
                return;
            }

            var data = args.TokenCache.SerializeMsalV3();

            var directory = Path.GetDirectoryName(CacheFilePath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(CacheFilePath, data).ConfigureAwait(false);

            // Owner-only permissions on Unix
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(CacheFilePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally
        {
            SyncLock.Release();
        }
    }
}
