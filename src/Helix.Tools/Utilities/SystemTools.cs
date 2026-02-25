using System.ComponentModel;
using System.Reflection;
using Helix.Core.Helpers;
using ModelContextProtocol.Server;

namespace Helix.Tools.Utilities;

/// <summary>
/// MCP tools for retrieving Helix server system information.
/// </summary>
[McpServerToolType]
public sealed class SystemTools
{
    /// <inheritdoc />
    [McpServerTool(Name = "get-version", ReadOnly = true),
     Description("Get the current Helix MCP server version.")]
    public static string GetVersion()
    {
        string version = typeof(SystemTools).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0-dev";

        return GraphResponseHelper.FormatResponse(new VersionInfo(version));
    }

    private sealed record VersionInfo(string Version);
}
