using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.Users;

/// <summary>
/// MCP tools for retrieving Microsoft 365 user profile information.
/// </summary>
[McpServerToolType]
public class UserTools(GraphServiceClient graphClient)
{
    /// <inheritdoc />
    [McpServerTool(Name = "get-current-user", ReadOnly = true),
     Description("Get the currently authenticated user's profile from Microsoft 365.")]
    public async Task<string> GetCurrentUser()
    {
        try
        {
            User? user = await graphClient.Me.GetAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(user);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}
