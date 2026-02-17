using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.SharePoint;

[McpServerToolType]
public class SharePointSiteTools(GraphServiceClient graphClient)
{
    [McpServerTool(Name = "search-sites", ReadOnly = true),
     Description("Search for SharePoint sites by keyword. "
        + "Returns site ID, name, URL, and description. "
        + "The site ID from results can be used with other SharePoint tools.")]
    public async Task<string> SearchSites(
        [Description("Search keyword to find sites, e.g. 'marketing', 'project'.")] string query,
        [Description("Maximum number of sites to return (default 10).")] int? top = null)
    {
        try
        {
            var sites = await graphClient.Sites.GetAsync(config =>
            {
                config.QueryParameters.Search = query;
                config.QueryParameters.Top = top ?? 10;
                config.QueryParameters.Select = ["id", "displayName", "name", "webUrl", "description"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(sites);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "get-site", ReadOnly = true),
     Description("Get a SharePoint site by its ID. Returns full site details including URL and description.")]
    public async Task<string> GetSite(
        [Description("The site ID (e.g. 'contoso.sharepoint.com,site-guid,web-guid').")] string siteId)
    {
        try
        {
            var site = await graphClient.Sites[siteId].GetAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(site);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "list-site-lists", ReadOnly = true),
     Description("List all lists in a SharePoint site. Returns list ID, name, description, and template.")]
    public async Task<string> ListSiteLists(
        [Description("The site ID.")] string siteId,
        [Description("Maximum number of lists to return (default 20).")] int? top = null)
    {
        try
        {
            var lists = await graphClient.Sites[siteId].Lists.GetAsync(config =>
            {
                config.QueryParameters.Top = top ?? 20;
                config.QueryParameters.Select = ["id", "displayName", "description", "webUrl", "list"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(lists);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "get-site-list", ReadOnly = true),
     Description("Get a specific SharePoint list by ID. Returns list details including column definitions when expanded.")]
    public async Task<string> GetSiteList(
        [Description("The site ID.")] string siteId,
        [Description("The list ID.")] string listId)
    {
        try
        {
            var list = await graphClient.Sites[siteId].Lists[listId].GetAsync(config =>
            {
                config.QueryParameters.Expand = ["columns"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(list);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}
