using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.Users;

[McpServerToolType]
public class UserTools(GraphServiceClient graphClient)
{
    [McpServerTool(Name = "get-current-user", ReadOnly = true),
     Description("Get the currently authenticated user's profile from Microsoft 365.")]
    public async Task<string> GetCurrentUser()
    {
        try
        {
            var user = await graphClient.Me.GetAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(user);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}
