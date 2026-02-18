using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Me.Messages.Item.Move;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.Mail;

[McpServerToolType]
public class MailTools(GraphServiceClient graphClient)
{
    private static readonly string[] DefaultSelect =
        ["id", "subject", "from", "receivedDateTime", "isRead", "bodyPreview"];

    [McpServerTool(Name = "list-mail-messages", ReadOnly = true),
     Description("List mail messages from the signed-in user's mailbox. "
        + "Supports OData query parameters for filtering and paging. "
        + "The $search parameter uses KQL syntax and the value MUST be wrapped in double quotes, "
        + "e.g. search = \"\\\"from:bob subject:budget\\\"\". "
        + "Common KQL properties: from, to, subject, body, received, sent, hasAttachment, importance. "
        + "Example filter: \"isRead eq false\". Example orderby: \"receivedDateTime desc\".")]
    public async Task<string> ListMailMessages(
        [Description("Maximum number of messages to return (default 10, max 1000).")] int? top = null,
        [Description("OData $filter expression, e.g. \"isRead eq false\".")] string? filter = null,
        [Description("KQL search query wrapped in double quotes, e.g. \"\\\"from:bob\\\"\".")] string? search = null,
        [Description("Comma-separated properties to return, e.g. \"subject,from,receivedDateTime,isRead\".")] string? select = null,
        [Description("OData $orderby expression, e.g. \"receivedDateTime desc\".")] string? orderby = null,
        [Description("Number of messages to skip for paging.")] int? skip = null)
    {
        try
        {
            var messages = await graphClient.Me.Messages.GetAsync(config =>
            {
                config.QueryParameters.Top = top ?? 10;
                config.QueryParameters.Select = !string.IsNullOrEmpty(select)
                    ? select.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : DefaultSelect;
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

    [McpServerTool(Name = "get-mail-message", ReadOnly = true),
     Description("Get a specific mail message by its ID. "
        + "By default the HTML body is converted to Markdown to save context. "
        + "Set includeFullHtml to true to get the original HTML body.")]
    public async Task<string> GetMailMessage(
        [Description("The unique identifier of the message.")] string messageId,
        [Description("Return the original HTML body instead of Markdown (default: false).")] object? includeFullHtml = null)
    {
        try
        {
            var message = await graphClient.Me.Messages[messageId].GetAsync().ConfigureAwait(false);

            if (message?.Body?.Content is not null
                && message.Body.ContentType == BodyType.Html
                && !GraphResponseHelper.IsTruthy(includeFullHtml))
            {
                message.Body.Content = HtmlHelper.ConvertToMarkdown(message.Body.Content);
                message.Body.ContentType = BodyType.Text;
            }

            return GraphResponseHelper.FormatResponse(message);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "send-mail"),
     Description("Send an email message. "
        + "IMPORTANT: Always confirm with the user before calling this tool. Never guess or fabricate email addresses.")]
    public async Task<string> SendMail(
        [Description("Email subject line.")] string subject,
        [Description("Email body content (plain text or HTML depending on bodyContentType).")] string body,
        [Description("Comma-separated 'To' recipient email addresses.")] string toRecipients,
        [Description("Comma-separated 'CC' recipient email addresses.")] string? ccRecipients = null,
        [Description("Comma-separated 'BCC' recipient email addresses.")] string? bccRecipients = null,
        [Description("Message importance: low, normal, or high (default: normal).")] string? importance = null,
        [Description("Body content type: text or html (default: text).")] string? bodyContentType = null,
        [Description("Comma-separated absolute file paths to attach, e.g. '/tmp/report.pdf,/tmp/image.png'.")] string? attachmentFilePaths = null,
        [Description("Comma-separated MIME types matching each attachment, e.g. 'application/pdf,image/png'.")] string? attachmentContentTypes = null,
        [Description("Comma-separated base64-encoded file contents. Use instead of attachmentFilePaths when files are not on the host filesystem.")] string? attachmentBase64Contents = null,
        [Description("Comma-separated file names matching each base64 attachment, e.g. 'report.pdf,image.png'.")] string? attachmentFileNames = null)
    {
        try
        {
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = ParseBodyContentType(bodyContentType),
                    Content = body
                },
                ToRecipients = ParseRecipients(toRecipients)
            };

            var cc = ParseRecipients(ccRecipients);
            if (cc.Count > 0)
                message.CcRecipients = cc;

            var bcc = ParseRecipients(bccRecipients);
            if (bcc.Count > 0)
                message.BccRecipients = bcc;

            if (!string.IsNullOrWhiteSpace(importance))
                message.Importance = ParseImportance(importance);

            var hasFilePaths = !string.IsNullOrWhiteSpace(attachmentFilePaths);
            var hasBase64 = !string.IsNullOrWhiteSpace(attachmentBase64Contents);

            if (hasFilePaths || hasBase64)
            {
                // Create draft first, then attach, then send
                var draft = await graphClient.Me.Messages.PostAsync(message).ConfigureAwait(false);

                if (hasFilePaths)
                {
                    var paths = attachmentFilePaths!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var types = attachmentContentTypes?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

                    if (types.Length > 0 && types.Length != paths.Length)
                        return GraphResponseHelper.FormatError("attachmentContentTypes count must match attachmentFilePaths count.");

                    foreach (var path in paths)
                    {
                        if (!File.Exists(path))
                            return GraphResponseHelper.FormatError($"File not found: {path}");
                    }

                    for (var i = 0; i < paths.Length; i++)
                    {
                        var fileBytes = await File.ReadAllBytesAsync(paths[i]).ConfigureAwait(false);
                        var attachment = new FileAttachment
                        {
                            OdataType = "#microsoft.graph.fileAttachment",
                            Name = Path.GetFileName(paths[i]),
                            ContentType = types.Length > 0 ? types[i] : "application/octet-stream",
                            ContentBytes = fileBytes
                        };
                        await graphClient.Me.Messages[draft!.Id].Attachments.PostAsync(attachment).ConfigureAwait(false);
                    }
                }

                if (hasBase64)
                {
                    var b64Items = attachmentBase64Contents!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var names = attachmentFileNames?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
                    var types = attachmentContentTypes?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

                    for (var i = 0; i < b64Items.Length; i++)
                    {
                        byte[] fileBytes;
                        try
                        {
                            fileBytes = Convert.FromBase64String(b64Items[i]);
                        }
                        catch (FormatException)
                        {
                            return GraphResponseHelper.FormatError($"Invalid base64 content at position {i}.");
                        }

                        var attachment = new FileAttachment
                        {
                            OdataType = "#microsoft.graph.fileAttachment",
                            Name = names.Length > i ? names[i] : $"attachment-{i}",
                            ContentType = types.Length > i ? types[i] : "application/octet-stream",
                            ContentBytes = fileBytes
                        };
                        await graphClient.Me.Messages[draft!.Id].Attachments.PostAsync(attachment).ConfigureAwait(false);
                    }
                }

                await graphClient.Me.Messages[draft!.Id].Send.PostAsync(null).ConfigureAwait(false);
            }
            else
            {
                await graphClient.Me.SendMail.PostAsync(new SendMailPostRequestBody
                {
                    Message = message
                }).ConfigureAwait(false);
            }

            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "delete-mail-message"),
     Description("Delete a mail message. The message is moved to the Deleted Items folder. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> DeleteMailMessage(
        [Description("The unique identifier of the message to delete.")] string messageId)
    {
        try
        {
            await graphClient.Me.Messages[messageId].DeleteAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "move-mail-message"),
     Description("Move a mail message to a different folder. Use 'list-mail-folders' to get folder IDs. "
        + "Well-known folder names: inbox, drafts, sentitems, deleteditems, archive, junkemail. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> MoveMailMessage(
        [Description("The unique identifier of the message to move.")] string messageId,
        [Description("Destination folder ID or well-known name (e.g. 'inbox', 'archive').")] string destinationFolderId)
    {
        try
        {
            var moved = await graphClient.Me.Messages[messageId].Move.PostAsync(new MovePostRequestBody
            {
                DestinationId = destinationFolderId
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(moved);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "update-mail-message"),
     Description("Update properties of a mail message such as read status, categories, importance, or subject. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> UpdateMailMessage(
        [Description("The unique identifier of the message to update.")] string messageId,
        [Description("Mark as read (true) or unread (false).")] object? isRead = null,
        [Description("Comma-separated categories to assign.")] string? categories = null,
        [Description("Message importance: low, normal, or high.")] string? importance = null,
        [Description("Updated subject line.")] string? subject = null)
    {
        try
        {
            var message = new Message();

            if (isRead is not null)
                message.IsRead = GraphResponseHelper.IsTruthy(isRead);
            if (!string.IsNullOrEmpty(categories))
                message.Categories = [.. categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            if (!string.IsNullOrEmpty(importance))
                message.Importance = ParseImportance(importance);
            if (!string.IsNullOrEmpty(subject))
                message.Subject = subject;

            var updated = await graphClient.Me.Messages[messageId].PatchAsync(message).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(updated);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    internal static List<Recipient> ParseRecipients(string? recipientList)
    {
        if (string.IsNullOrWhiteSpace(recipientList))
            return [];

        return [.. recipientList
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(email => new Recipient
            {
                EmailAddress = new EmailAddress { Address = email }
            })];
    }

    internal static Importance? ParseImportance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.ToUpperInvariant() switch
        {
            "LOW" => Importance.Low,
            "NORMAL" => Importance.Normal,
            "HIGH" => Importance.High,
            _ => Importance.Normal
        };
    }

    internal static BodyType ParseBodyContentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return BodyType.Text;

        return value.ToUpperInvariant() switch
        {
            "HTML" => BodyType.Html,
            _ => BodyType.Text
        };
    }
}
