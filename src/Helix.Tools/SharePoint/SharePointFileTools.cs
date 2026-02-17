using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.SharePoint;

[McpServerToolType]
public class SharePointFileTools(GraphServiceClient graphClient)
{
    [McpServerTool(Name = "list-site-drives", ReadOnly = true),
     Description("List all document libraries (drives) in a SharePoint site. "
        + "Returns drive ID, name, URL, and quota information.")]
    public async Task<string> ListSiteDrives(
        [Description("The site ID.")] string siteId)
    {
        try
        {
            var drives = await graphClient.Sites[siteId].Drives.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "name", "webUrl", "driveType", "quota"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(drives);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "list-drive-children", ReadOnly = true),
     Description("List files and folders in a SharePoint document library. "
        + "Omit itemId to list root folder contents, or provide an itemId to list a subfolder's contents.")]
    public async Task<string> ListDriveChildren(
        [Description("The drive ID (from list-site-drives).")] string driveId,
        [Description("Folder item ID to list contents of. Omit for root folder.")] string? itemId = null,
        [Description("Maximum number of items to return (default 20).")] int? top = null)
    {
        try
        {
            DriveItemCollectionResponse? children;

            var folderId = string.IsNullOrWhiteSpace(itemId) ? "root" : itemId;
            children = await graphClient.Drives[driveId].Items[folderId].Children.GetAsync(config =>
            {
                config.QueryParameters.Top = top ?? 20;
                config.QueryParameters.Select = ["id", "name", "size", "webUrl", "folder", "file", "lastModifiedDateTime", "createdDateTime"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(children);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "get-drive-item", ReadOnly = true),
     Description("Get metadata for a specific file or folder in a SharePoint document library.")]
    public async Task<string> GetDriveItem(
        [Description("The drive ID.")] string driveId,
        [Description("The item ID.")] string itemId)
    {
        try
        {
            var item = await graphClient.Drives[driveId].Items[itemId].GetAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(item);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "download-drive-item", ReadOnly = true),
     Description("Download a file from a SharePoint document library. "
        + "The file is saved to a temporary directory on disk and the file path is returned.")]
    public async Task<string> DownloadDriveItem(
        [Description("The drive ID.")] string driveId,
        [Description("The item ID of the file to download.")] string itemId)
    {
        try
        {
            var item = await graphClient.Drives[driveId].Items[itemId].GetAsync().ConfigureAwait(false);
            if (item?.Name is null)
                return GraphResponseHelper.FormatError("Could not retrieve item metadata.");

            var stream = await graphClient.Drives[driveId].Items[itemId].Content.GetAsync().ConfigureAwait(false);
            if (stream is null)
                return GraphResponseHelper.FormatError("No content available for this item.");

            var tempDir = Path.Combine(Path.GetTempPath(), "helix-sharepoint");
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, item.Name);

            var fileStream = File.Create(filePath);
            await using var _ = fileStream.ConfigureAwait(false);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);

            var sizeBytes = new FileInfo(filePath).Length;
            var sizeDisplay = sizeBytes < 1024 ? $"{sizeBytes} bytes" : $"{sizeBytes / 1024} KB";

            return $"File saved to: {filePath}\n"
                + $"Size: {sizeDisplay}\n"
                + $"Name: {item.Name}";
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}
