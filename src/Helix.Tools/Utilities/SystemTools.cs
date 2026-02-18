using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Helix.Tools.Utilities;

[McpServerToolType]
public sealed class SystemTools
{
    [McpServerTool(Name = "get-version", ReadOnly = true),
     Description("Get the current Helix MCP server version.")]
    public static string GetVersion()
    {
        var version = typeof(SystemTools).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0-dev";

        return JsonSerializer.Serialize(new { version });
    }
}
