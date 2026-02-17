using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
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
        + "and then 'send-draft-message' to send it.")]
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

    [McpServerTool(Name = "send-draft-message"),
     Description("Send an existing draft message by its ID. "
        + "Use after 'create-draft-message' and optionally 'add-mail-attachment'.")]
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
