namespace Helix.Core.Configuration;

public class HelixOptions
{
    public const string SectionName = "Helix";

    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = "common";
    public string? ClientSecret { get; set; }
    public string? AccessToken { get; set; }
    public CloudType CloudType { get; set; } = CloudType.Global;
    public bool ReadOnly { get; set; }
    public bool OrgMode { get; set; }
    public string? EnabledToolsPattern { get; set; }
}

public enum CloudType
{
    Global,
    China
}
