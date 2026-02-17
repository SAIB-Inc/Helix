using Microsoft.Identity.Client;

namespace Helix.Core.Auth;

/// <summary>
/// Holds the in-flight device-code login task so it survives across
/// tool invocations. Static because the MCP SDK creates new tool
/// instances per invocation.
/// </summary>
public static class LoginSession
{
    public static Task<AuthenticationResult>? PendingAuth { get; set; }
}
