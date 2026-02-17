namespace Helix.Core.Configuration;

public static class CloudConfiguration
{
    public static string GetAuthority(CloudType cloudType, string tenantId) => cloudType switch
    {
        CloudType.Global => $"https://login.microsoftonline.com/{tenantId}",
        CloudType.China => $"https://login.chinacloudapi.cn/{tenantId}",
        _ => throw new ArgumentOutOfRangeException(nameof(cloudType))
    };

    public static string GetGraphEndpoint(CloudType cloudType) => cloudType switch
    {
        CloudType.Global => "https://graph.microsoft.com/v1.0",
        CloudType.China => "https://microsoftgraph.chinacloudapi.cn/v1.0",
        _ => throw new ArgumentOutOfRangeException(nameof(cloudType))
    };

    public static string[] GetGraphScopes(CloudType cloudType) => cloudType switch
    {
        CloudType.Global => ["https://graph.microsoft.com/.default"],
        CloudType.China => ["https://microsoftgraph.chinacloudapi.cn/.default"],
        _ => throw new ArgumentOutOfRangeException(nameof(cloudType))
    };
}
