namespace Helix.Core.Configuration;

/// <summary>
/// Configuration options for the Helix MCP server.
/// </summary>
public class HelixOptions
{
    /// <summary>
    /// The configuration section name used for binding.
    /// </summary>
    public const string SectionName = "Helix";

    /// <summary>
    /// Azure AD application (client) ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD tenant ID. Defaults to "common" for multi-tenant apps.
    /// </summary>
    public string TenantId { get; set; } = "common";

    /// <summary>
    /// Client secret for app-only (daemon) authentication.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Static access token for testing or pre-authenticated scenarios.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Target cloud environment (Global or China).
    /// </summary>
    public CloudType CloudType { get; set; } = CloudType.Global;

    /// <summary>
    /// When true, only read-only MCP tools are registered.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// When true, enables organization-wide access mode.
    /// </summary>
    public bool OrgMode { get; set; }

    /// <summary>
    /// Glob pattern to filter which MCP tools are enabled.
    /// </summary>
    public string? EnabledToolsPattern { get; set; }
}

/// <summary>
/// Supported Microsoft cloud environments.
/// </summary>
public enum CloudType
{
    /// <summary>
    /// Microsoft Global cloud (default).
    /// </summary>
    Global,

    /// <summary>
    /// Microsoft China cloud (21Vianet).
    /// </summary>
    China
}
