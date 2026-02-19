using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Me.Messages.Item.CreateForward;
using Microsoft.Graph.Me.Messages.Item.CreateReply;
using Microsoft.Graph.Me.Messages.Item.CreateReplyAll;
using Microsoft.Graph.Me.Messages.Item.Send;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.Mail;

[McpServerToolType]
public class MailDraftTools(GraphServiceClient graphClient)
{
    [McpServerTool(Name = "create-draft-message"),
     Description("Create a draft email message in the Drafts folder. "
        + "Returns the draft message with its ID, which can be used with 'add-mail-attachment' to attach files "
        + "and then 'send-draft-message' to send it. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> CreateDraftMessage(
        [Description("Email subject line.")] string subject,
        [Description("Email body content (plain text or HTML depending on bodyContentType).")] string body,
        [Description("Comma-separated 'To' recipient email addresses.")] string toRecipients,
        [Description("Comma-separated 'CC' recipient email addresses.")] string? ccRecipients = null,
        [Description("Comma-separated 'BCC' recipient email addresses.")] string? bccRecipients = null,
        [Description("Message importance: low, normal, or high (default: normal).")] string? importance = null,
        [Description("Body content type: text or html (default: text).")] string? bodyContentType = null)
    {
        try
        {
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = MailTools.ParseBodyContentType(bodyContentType),
                    Content = body
                },
                ToRecipients = MailTools.ParseRecipients(toRecipients)
            };

            var cc = MailTools.ParseRecipients(ccRecipients);
            if (cc.Count > 0)
                message.CcRecipients = cc;

            var bcc = MailTools.ParseRecipients(bccRecipients);
            if (bcc.Count > 0)
                message.BccRecipients = bcc;

            if (!string.IsNullOrWhiteSpace(importance))
                message.Importance = MailTools.ParseImportance(importance);

            var draft = await graphClient.Me.Messages.PostAsync(message).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(draft);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "create-reply-draft"),
     Description("Create a draft reply to an existing message (reply to sender only). "
        + "The draft is created in the Drafts folder with correct threading and quoted original message. "
        + "Use 'update-draft-message' to modify the draft, 'add-mail-attachment' to attach files, "
        + "and 'send-draft-message' to send it. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> CreateReplyDraft(
        [Description("The unique identifier of the message to reply to.")] string messageId,
        [Description("Reply body text to include above the quoted original message.")] string? comment = null)
    {
        try
        {
            var requestBody = new CreateReplyPostRequestBody();
            if (!string.IsNullOrWhiteSpace(comment))
                requestBody.Comment = comment;

            var draft = await graphClient.Me.Messages[messageId].CreateReply
                .PostAsync(requestBody).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(draft);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "create-reply-all-draft"),
     Description("Create a draft reply-all to an existing message (reply to sender and all recipients). "
        + "The draft is created in the Drafts folder with correct threading and quoted original message. "
        + "Use 'update-draft-message' to modify the draft, 'add-mail-attachment' to attach files, "
        + "and 'send-draft-message' to send it. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> CreateReplyAllDraft(
        [Description("The unique identifier of the message to reply to.")] string messageId,
        [Description("Reply body text to include above the quoted original message.")] string? comment = null)
    {
        try
        {
            var requestBody = new CreateReplyAllPostRequestBody();
            if (!string.IsNullOrWhiteSpace(comment))
                requestBody.Comment = comment;

            var draft = await graphClient.Me.Messages[messageId].CreateReplyAll
                .PostAsync(requestBody).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(draft);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "create-forward-draft"),
     Description("Create a draft forward of an existing message. "
        + "The draft is created in the Drafts folder with the original message as quoted content. "
        + "Use 'update-draft-message' to modify the draft, 'add-mail-attachment' to attach files, "
        + "and 'send-draft-message' to send it. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> CreateForwardDraft(
        [Description("The unique identifier of the message to forward.")] string messageId,
        [Description("Comma-separated email addresses to forward to.")] string? toRecipients = null,
        [Description("Comment text to include above the forwarded message.")] string? comment = null)
    {
        try
        {
            var requestBody = new CreateForwardPostRequestBody();
            if (!string.IsNullOrWhiteSpace(comment))
                requestBody.Comment = comment;

            var recipients = MailTools.ParseRecipients(toRecipients);
            if (recipients.Count > 0)
                requestBody.ToRecipients = recipients;

            var draft = await graphClient.Me.Messages[messageId].CreateForward
                .PostAsync(requestBody).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(draft);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "update-draft-message"),
     Description("Update a draft email message. Only provided fields are updated. "
        + "Use this to modify the subject, body, or recipients of a draft before sending. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> UpdateDraftMessage(
        [Description("The unique identifier of the draft message to update.")] string messageId,
        [Description("Updated email subject line.")] string? subject = null,
        [Description("Updated email body content (plain text or HTML depending on bodyContentType).")] string? body = null,
        [Description("Body content type: text or html.")] string? bodyContentType = null,
        [Description("Updated comma-separated 'To' recipient email addresses. Replaces all existing To recipients.")] string? toRecipients = null,
        [Description("Updated comma-separated 'CC' recipient email addresses. Replaces all existing CC recipients.")] string? ccRecipients = null,
        [Description("Updated comma-separated 'BCC' recipient email addresses. Replaces all existing BCC recipients.")] string? bccRecipients = null,
        [Description("Message importance: low, normal, or high.")] string? importance = null)
    {
        try
        {
            var message = new Message();

            if (!string.IsNullOrEmpty(subject))
                message.Subject = subject;

            if (!string.IsNullOrWhiteSpace(body))
            {
                message.Body = new ItemBody
                {
                    ContentType = MailTools.ParseBodyContentType(bodyContentType),
                    Content = body
                };
            }

            if (!string.IsNullOrWhiteSpace(toRecipients))
                message.ToRecipients = MailTools.ParseRecipients(toRecipients);

            if (!string.IsNullOrWhiteSpace(ccRecipients))
                message.CcRecipients = MailTools.ParseRecipients(ccRecipients);

            if (!string.IsNullOrWhiteSpace(bccRecipients))
                message.BccRecipients = MailTools.ParseRecipients(bccRecipients);

            if (!string.IsNullOrWhiteSpace(importance))
                message.Importance = MailTools.ParseImportance(importance);

            var updated = await graphClient.Me.Messages[messageId].PatchAsync(message).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(updated);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "send-draft-message"),
     Description("Send an existing draft message by its ID. "
        + "Use after 'create-draft-message' and optionally 'add-mail-attachment'. "
        + "IMPORTANT: Always confirm with the user before calling this tool.")]
    public async Task<string> SendDraftMessage(
        [Description("The unique identifier of the draft message to send.")] string messageId)
    {
        try
        {
            await graphClient.Me.Messages[messageId].Send.PostAsync(null).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}
