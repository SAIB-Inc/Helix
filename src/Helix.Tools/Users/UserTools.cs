using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using ModelContextProtocol.Server;

namespace Helix.Tools.Users;

[McpServerToolType]
public class UserTools(GraphServiceClient graphClient)
{
    private readonly GraphServiceClient _graphClient = graphClient;

    [McpServerTool(Name = "get-current-user", ReadOnly = true),
     Description("Get the currently authenticated user's profile from Microsoft 365.")]
    public async Task<string> GetCurrentUser()
    {
        var user = await _graphClient.Me.GetAsync();
        return GraphResponseHelper.FormatResponse(user);
    }
}
