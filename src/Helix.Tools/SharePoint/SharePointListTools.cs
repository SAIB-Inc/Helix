using System.ComponentModel;
using System.Text.Json;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.SharePoint;

[McpServerToolType]
public class SharePointListTools(GraphServiceClient graphClient)
{
    [McpServerTool(Name = "list-list-items", ReadOnly = true),
     Description("List items in a SharePoint list. Returns items with their field values expanded. "
        + "Supports OData filtering and paging.")]
    public async Task<string> ListListItems(
        [Description("The site ID.")] string siteId,
        [Description("The list ID.")] string listId,
        [Description("Maximum number of items to return (default 10, max 1000).")] int? top = null,
        [Description("OData $filter expression, e.g. \"fields/Status eq 'Active'\".")] string? filter = null,
        [Description("Comma-separated properties to return.")] string? select = null,
        [Description("Number of items to skip for paging.")] int? skip = null)
    {
        try
        {
            var items = await graphClient.Sites[siteId].Lists[listId].Items.GetAsync(config =>
            {
                config.QueryParameters.Top = top ?? 10;
                config.QueryParameters.Expand = ["fields"];
                if (!string.IsNullOrEmpty(filter))
                    config.QueryParameters.Filter = filter;
                if (!string.IsNullOrEmpty(select))
                    config.QueryParameters.Select = select.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (skip.HasValue)
                    config.QueryParameters.Skip = skip.Value;
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(items);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "get-list-item", ReadOnly = true),
     Description("Get a specific item from a SharePoint list. Returns the item with all field values.")]
    public async Task<string> GetListItem(
        [Description("The site ID.")] string siteId,
        [Description("The list ID.")] string listId,
        [Description("The item ID.")] string itemId)
    {
        try
        {
            var item = await graphClient.Sites[siteId].Lists[listId].Items[itemId].GetAsync(config =>
            {
                config.QueryParameters.Expand = ["fields"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(item);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "create-list-item"),
     Description("Create a new item in a SharePoint list. "
        + "Fields are passed as a JSON object string, e.g. '{\"Title\": \"My Item\", \"Status\": \"Active\"}'. "
        + "Use 'get-site-list' to see available column names. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> CreateListItem(
        [Description("The site ID.")] string siteId,
        [Description("The list ID.")] string listId,
        [Description("JSON object string with field name/value pairs, e.g. '{\"Title\": \"New Item\", \"Status\": \"Active\"}'.")] string fields)
    {
        try
        {
            var fieldValues = ParseFields(fields);
            if (fieldValues is null)
                return GraphResponseHelper.FormatError("Invalid JSON in 'fields' parameter. Expected a JSON object, e.g. '{\"Title\": \"My Item\"}'.");

            var listItem = new ListItem
            {
                Fields = new FieldValueSet { AdditionalData = fieldValues }
            };

            var created = await graphClient.Sites[siteId].Lists[listId].Items.PostAsync(listItem).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(created);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "update-list-item"),
     Description("Update an existing item in a SharePoint list. "
        + "Fields are passed as a JSON object string with only the fields to update. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> UpdateListItem(
        [Description("The site ID.")] string siteId,
        [Description("The list ID.")] string listId,
        [Description("The item ID.")] string itemId,
        [Description("JSON object string with field name/value pairs to update, e.g. '{\"Status\": \"Completed\"}'.")] string fields)
    {
        try
        {
            var fieldValues = ParseFields(fields);
            if (fieldValues is null)
                return GraphResponseHelper.FormatError("Invalid JSON in 'fields' parameter. Expected a JSON object, e.g. '{\"Status\": \"Done\"}'.");

            var fieldValueSet = new FieldValueSet { AdditionalData = fieldValues };

            var updated = await graphClient.Sites[siteId].Lists[listId].Items[itemId].Fields.PatchAsync(fieldValueSet).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(updated);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "delete-list-item"),
     Description("Delete an item from a SharePoint list. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> DeleteListItem(
        [Description("The site ID.")] string siteId,
        [Description("The list ID.")] string listId,
        [Description("The item ID.")] string itemId)
    {
        try
        {
            await graphClient.Sites[siteId].Lists[listId].Items[itemId].DeleteAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    private static Dictionary<string, object>? ParseFields(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var dict = new Dictionary<string, object>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString()!,
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null!,
                    _ => prop.Value.GetRawText()
                };
            }
            return dict;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
