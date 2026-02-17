using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.Mail;

[McpServerToolType]
public class MailAttachmentTools(GraphServiceClient graphClient)
{
    [McpServerTool(Name = "list-mail-attachments", ReadOnly = true),
     Description("List all attachments on a mail message.")]
    public async Task<string> ListMailAttachments(
        [Description("The unique identifier of the message.")] string messageId)
    {
        try
        {
            var attachments = await graphClient.Me.Messages[messageId].Attachments.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "name", "contentType", "size"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(attachments);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "get-mail-attachment", ReadOnly = true),
     Description("Get a specific attachment from a mail message. "
        + "File attachments are saved to disk and the file path is returned.")]
    public async Task<string> GetMailAttachment(
        [Description("The unique identifier of the message.")] string messageId,
        [Description("The unique identifier of the attachment.")] string attachmentId)
    {
        try
        {
            var attachment = await graphClient.Me.Messages[messageId].Attachments[attachmentId].GetAsync().ConfigureAwait(false);

            // Save file attachments to disk instead of returning large blobs inline
            if (attachment is FileAttachment { ContentBytes.Length: > 0 } fileAttachment)
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "helix-attachments");
                Directory.CreateDirectory(tempDir);

                var safeName = fileAttachment.Name ?? $"attachment-{attachmentId}";
                var filePath = Path.Combine(tempDir, safeName);

                await File.WriteAllBytesAsync(filePath, fileAttachment.ContentBytes).ConfigureAwait(false);

                fileAttachment.ContentBytes = null;
                var metadata = GraphResponseHelper.FormatResponse(fileAttachment);

                var sizeBytes = new FileInfo(filePath).Length;
                var sizeDisplay = sizeBytes < 1024 ? $"{sizeBytes} bytes" : $"{sizeBytes / 1024} KB";

                return $"Attachment saved to: {filePath}\n"
                    + $"Size: {sizeDisplay}\n\n"
                    + $"Metadata:\n{metadata}";
            }

            return GraphResponseHelper.FormatResponse(attachment);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "add-mail-attachment"),
     Description("Add a file attachment to a mail message (typically a draft). "
        + "Reads the file from the given path on disk — do NOT pass file content inline.")]
    public async Task<string> AddMailAttachment(
        [Description("The unique identifier of the message to attach the file to.")] string messageId,
        [Description("Absolute path to the file on disk, e.g. '/tmp/report.pdf'.")] string filePath,
        [Description("MIME type, e.g. 'application/pdf', 'image/png', 'text/plain'.")] string contentType)
    {
        try
        {
            if (!File.Exists(filePath))
                return $"File not found: {filePath}";

            var fileBytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
            var fileName = Path.GetFileName(filePath);

            var attachment = new FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = fileName,
                ContentType = contentType,
                ContentBytes = fileBytes
            };

            await graphClient.Me.Messages[messageId].Attachments.PostAsync(attachment).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "delete-mail-attachment"),
     Description("Delete an attachment from a mail message.")]
    public async Task<string> DeleteMailAttachment(
        [Description("The unique identifier of the message.")] string messageId,
        [Description("The unique identifier of the attachment to delete.")] string attachmentId)
    {
        try
        {
            await graphClient.Me.Messages[messageId].Attachments[attachmentId].DeleteAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}
