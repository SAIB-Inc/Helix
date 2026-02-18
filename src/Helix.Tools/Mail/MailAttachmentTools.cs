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
        + "File attachments are saved to disk and the file path is returned. "
        + "Set returnBase64 to true to return the file content as a base64-encoded string instead "
        + "(useful when the caller cannot access the host filesystem).")]
    public async Task<string> GetMailAttachment(
        [Description("The unique identifier of the message.")] string messageId,
        [Description("The unique identifier of the attachment.")] string attachmentId,
        [Description("Return file content as base64 instead of saving to disk (default: false).")] object? returnBase64 = null)
    {
        try
        {
            var attachment = await graphClient.Me.Messages[messageId].Attachments[attachmentId].GetAsync().ConfigureAwait(false);

            if (attachment is FileAttachment { ContentBytes.Length: > 0 } fileAttachment)
            {
                var safeName = fileAttachment.Name ?? $"attachment-{attachmentId}";
                var sizeBytes = fileAttachment.ContentBytes.Length;
                var sizeDisplay = sizeBytes < 1024 ? $"{sizeBytes} bytes" : $"{sizeBytes / 1024} KB";

                if (GraphResponseHelper.IsTruthy(returnBase64))
                {
                    var base64 = Convert.ToBase64String(fileAttachment.ContentBytes);
                    fileAttachment.ContentBytes = null;
                    var metadata = GraphResponseHelper.FormatResponse(fileAttachment);

                    return $"Name: {safeName}\n"
                        + $"Size: {sizeDisplay}\n"
                        + $"ContentBase64: {base64}\n\n"
                        + $"Metadata:\n{metadata}";
                }

                var tempDir = Path.Combine(Path.GetTempPath(), "helix-attachments");
                Directory.CreateDirectory(tempDir);
                var filePath = Path.Combine(tempDir, safeName);

                await File.WriteAllBytesAsync(filePath, fileAttachment.ContentBytes).ConfigureAwait(false);

                fileAttachment.ContentBytes = null;
                var meta = GraphResponseHelper.FormatResponse(fileAttachment);

                return $"Attachment saved to: {filePath}\n"
                    + $"Size: {sizeDisplay}\n\n"
                    + $"Metadata:\n{meta}";
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
        + "Reads the file from the given path on disk — do NOT pass file content inline. "
        + "Alternatively, pass file content as base64 with contentBase64 and fileName instead of filePath "
        + "(useful when the caller cannot access the host filesystem).")]
    public async Task<string> AddMailAttachment(
        [Description("The unique identifier of the message to attach the file to.")] string messageId,
        [Description("MIME type, e.g. 'application/pdf', 'image/png', 'text/plain'.")] string contentType,
        [Description("Absolute path to the file on disk, e.g. '/tmp/report.pdf'.")] string? filePath = null,
        [Description("Base64-encoded file content. Use this instead of filePath when the file is not on the host filesystem.")] string? contentBase64 = null,
        [Description("File name for the attachment (required when using contentBase64), e.g. 'report.pdf'.")] string? fileName = null)
    {
        try
        {
            byte[] fileBytes;
            string attachmentName;

            if (!string.IsNullOrWhiteSpace(contentBase64))
            {
                try
                {
                    fileBytes = Convert.FromBase64String(contentBase64);
                }
                catch (FormatException)
                {
                    return GraphResponseHelper.FormatError("Invalid base64 content in 'contentBase64' parameter.");
                }
                attachmentName = !string.IsNullOrWhiteSpace(fileName) ? fileName : "attachment";
            }
            else if (!string.IsNullOrWhiteSpace(filePath))
            {
                if (!File.Exists(filePath))
                    return GraphResponseHelper.FormatError($"File not found: {filePath}");

                fileBytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                attachmentName = Path.GetFileName(filePath);
            }
            else
            {
                return GraphResponseHelper.FormatError("Either 'filePath' or 'contentBase64' must be provided.");
            }

            var attachment = new FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = attachmentName,
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
