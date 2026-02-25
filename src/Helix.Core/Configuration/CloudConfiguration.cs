namespace Helix.Core.Configuration;

/// <summary>
/// Provides cloud-specific endpoint and scope configuration for Microsoft Graph.
/// </summary>
public static class CloudConfiguration
{
    /// <summary>
    /// Returns the OAuth2 authority URL for the given cloud and tenant.
    /// </summary>
    public static string GetAuthority(CloudType cloudType, string tenantId)
    {
        return cloudType switch
        {
            CloudType.Global => $"https://login.microsoftonline.com/{tenantId}",
            CloudType.China => $"https://login.chinacloudapi.cn/{tenantId}",
            _ => throw new ArgumentOutOfRangeException(nameof(cloudType))
        };
    }

    /// <summary>
    /// Returns the Microsoft Graph base endpoint URL for the given cloud.
    /// </summary>
    public static string GetGraphEndpoint(CloudType cloudType)
    {
        return cloudType switch
        {
            CloudType.Global => "https://graph.microsoft.com/v1.0",
            CloudType.China => "https://microsoftgraph.chinacloudapi.cn/v1.0",
            _ => throw new ArgumentOutOfRangeException(nameof(cloudType))
        };
    }

    /// <summary>
    /// Returns the default Graph API scopes for the given cloud.
    /// </summary>
    public static string[] GetGraphScopes(CloudType cloudType)
    {
        return cloudType switch
        {
            CloudType.Global => ["https://graph.microsoft.com/.default"],
            CloudType.China => ["https://microsoftgraph.chinacloudapi.cn/.default"],
            _ => throw new ArgumentOutOfRangeException(nameof(cloudType))
        };
    }
}
