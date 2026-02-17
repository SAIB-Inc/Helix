using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Holds the in-flight device-code login task so it survives across
/// tool invocations. Static because the MCP SDK creates new tool
/// instances per invocation. Thread-safe for HTTP transport.
/// </summary>
public static class LoginSession
{
    private static readonly Lock SyncLock = new();
    private static Task<AuthenticationResult>? s_pendingAuth;

    public static Task<AuthenticationResult>? PendingAuth
    {
        get { lock (SyncLock) return s_pendingAuth; }
        set { lock (SyncLock) s_pendingAuth = value; }
    }
}
