using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.CreateLink;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.SharePoint;

/// <summary>
/// MCP tools for managing sharing links on SharePoint/OneDrive drive items via Microsoft Graph.
/// </summary>
[McpServerToolType]
public sealed class SharePointSharingTools(GraphServiceClient graphClient)
{
    /// <inheritdoc />
    [McpServerTool(Name = "create-sharing-link"),
     Description("Create a sharing link for a file or folder in a SharePoint/OneDrive document library. "
        + "Returns the sharing URL, link ID, scope, and expiration. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> CreateSharingLink(
        [Description("The drive ID (from list-site-drives).")] string driveId,
        [Description("The item ID of the file or folder to share.")] string itemId,
        [Description("Link type: 'view' for read-only or 'edit' for read-write.")] string type,
        [Description("Link scope: 'anonymous' (anyone with the link), 'organization' (anyone in the org), "
            + "or 'users' (specific people only — provide recipients).")] string scope,
        [Description("Optional password to protect the link.")] string? password = null,
        [Description("Optional expiration date-time in ISO 8601 format, e.g. '2025-12-31T23:59:59Z'.")] string? expirationDateTime = null,
        [Description("Optional semicolon-separated email addresses for 'users' scope, e.g. 'alice@example.com;bob@example.com'.")] string? recipients = null)
    {
        try
        {
            CreateLinkPostRequestBody body = new()
            {
                Type = type,
                Scope = scope
            };

            if (!string.IsNullOrWhiteSpace(password))
            {
                body.Password = password;
            }

            if (!string.IsNullOrWhiteSpace(expirationDateTime))
            {
                if (DateTimeOffset.TryParse(expirationDateTime, out DateTimeOffset parsed))
                {
                    body.ExpirationDateTime = parsed;
                }
                else
                {
                    return GraphResponseHelper.FormatError("Invalid ISO 8601 date-time format for 'expirationDateTime'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(recipients))
            {
                body.Recipients = [.. recipients
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(email => new DriveRecipient { Email = email })];
            }

            Permission? permission = await graphClient.Drives[driveId].Items[itemId]
                .CreateLink.PostAsync(body).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(permission);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    /// <inheritdoc />
    [McpServerTool(Name = "list-sharing-links", ReadOnly = true),
     Description("List all active sharing links for a file or folder in a SharePoint/OneDrive document library. "
        + "Returns each link's ID, URL, scope, type, password protection status, and expiration.")]
    public async Task<string> ListSharingLinks(
        [Description("The drive ID (from list-site-drives).")] string driveId,
        [Description("The item ID of the file or folder.")] string itemId)
    {
        try
        {
            PermissionCollectionResponse? permissions = await graphClient.Drives[driveId].Items[itemId]
                .Permissions.GetAsync().ConfigureAwait(false);

            // Filter to only link-type permissions
            List<Permission> linkPermissions = permissions?.Value?
                .Where(p => p.Link is not null)
                .ToList() ?? [];

            return GraphResponseHelper.FormatResponse(new { value = linkPermissions });
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    /// <inheritdoc />
    [McpServerTool(Name = "delete-sharing-link"),
     Description("Delete (revoke) a sharing link from a file or folder in a SharePoint/OneDrive document library. "
        + "Use list-sharing-links to find the permission ID. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> DeleteSharingLink(
        [Description("The drive ID.")] string driveId,
        [Description("The item ID of the file or folder.")] string itemId,
        [Description("The permission ID of the sharing link to delete (from list-sharing-links).")] string permissionId)
    {
        try
        {
            await graphClient.Drives[driveId].Items[itemId]
                .Permissions[permissionId].DeleteAsync().ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}
