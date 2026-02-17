using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.Mail;

[McpServerToolType]
public class MailFolderTools(GraphServiceClient graphClient)
{
    [McpServerTool(Name = "list-mail-folders", ReadOnly = true),
     Description("List mail folders in the signed-in user's mailbox (Inbox, Drafts, Sent Items, etc.). "
        + "Returns folder ID, display name, unread count, and total count.")]
    public async Task<string> ListMailFolders()
    {
        try
        {
            var folders = await graphClient.Me.MailFolders.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "displayName", "parentFolderId", "unreadItemCount", "totalItemCount"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(folders);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "list-mail-folder-messages", ReadOnly = true),
     Description("List messages in a specific mail folder. "
        + "Use 'list-mail-folders' to get folder IDs, or use well-known names: inbox, drafts, sentitems, deleteditems, archive, junkemail. "
        + "Supports the same OData query parameters as 'list-mail-messages'.")]
    public async Task<string> ListMailFolderMessages(
        [Description("The folder ID or well-known name (e.g. 'inbox', 'drafts', 'sentitems').")] string folderId,
        [Description("Maximum number of messages to return (default 10, max 1000).")] int? top = null,
        [Description("OData $filter expression, e.g. \"isRead eq false\".")] string? filter = null,
        [Description("KQL search query wrapped in double quotes, e.g. \"\\\"from:bob\\\"\".")] string? search = null,
        [Description("Comma-separated properties to return, e.g. \"subject,from,receivedDateTime\".")] string? select = null,
        [Description("OData $orderby expression, e.g. \"receivedDateTime desc\".")] string? orderby = null,
        [Description("Number of messages to skip for paging.")] int? skip = null)
    {
        try
        {
            var messages = await graphClient.Me.MailFolders[folderId].Messages.GetAsync(config =>
            {
                config.QueryParameters.Top = top ?? 10;
                config.QueryParameters.Select = !string.IsNullOrEmpty(select)
                    ? select.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : ["id", "subject", "from", "receivedDateTime", "isRead", "bodyPreview"];
                if (!string.IsNullOrEmpty(filter))
                    config.QueryParameters.Filter = filter;
                if (!string.IsNullOrEmpty(search))
                    config.QueryParameters.Search = search;
                if (!string.IsNullOrEmpty(orderby))
                    config.QueryParameters.Orderby = [orderby];
                if (skip.HasValue)
                    config.QueryParameters.Skip = skip.Value;
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(messages);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}
