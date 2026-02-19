using System.Text.Json;
using Helix.Tools.Mail;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class MailDraftToolsTests(IntegrationFixture fixture)
{
    private readonly MailDraftTools _draftTools = new(fixture.GraphClient);
    private readonly MailTools _mailTools = new(fixture.GraphClient);

    [Fact]
    public async Task CreateDraftReturnsDraftWithId()
    {
        string? draftId = null;
        try
        {
            var result = await _draftTools.CreateDraftMessage(
                subject: "Helix Draft Test - Create",
                body: "Draft body",
                toRecipients: "clark@saib.dev");

            IntegrationFixture.AssertSuccess(result);
            using var doc = JsonDocument.Parse(result);
            draftId = doc.RootElement.GetProperty("id").GetString();
            Assert.NotNull(draftId);
            Assert.Equal("Helix Draft Test - Create", doc.RootElement.GetProperty("subject").GetString());
        }
        finally
        {
            if (draftId is not null)
                await _mailTools.DeleteMailMessage(draftId);
        }
    }

    [Fact]
    public async Task UpdateDraftSubjectAndBodyReturnsUpdated()
    {
        string? draftId = null;
        try
        {
            var createResult = await _draftTools.CreateDraftMessage(
                subject: "Helix Draft Test - Update",
                body: "Original body",
                toRecipients: "clark@saib.dev");

            using var createDoc = JsonDocument.Parse(createResult);
            draftId = createDoc.RootElement.GetProperty("id").GetString()!;

            var updateResult = await _draftTools.UpdateDraftMessage(
                messageId: draftId,
                subject: "Helix Draft Test - Updated Subject",
                body: "Updated body content");

            IntegrationFixture.AssertSuccess(updateResult);
            using var updateDoc = JsonDocument.Parse(updateResult);
            Assert.Equal("Helix Draft Test - Updated Subject", updateDoc.RootElement.GetProperty("subject").GetString());
        }
        finally
        {
            if (draftId is not null)
                await _mailTools.DeleteMailMessage(draftId);
        }
    }

    [Fact]
    public async Task UpdateDraftCcAndImportanceReturnsUpdated()
    {
        string? draftId = null;
        try
        {
            var createResult = await _draftTools.CreateDraftMessage(
                subject: "Helix Draft Test - CC",
                body: "CC test",
                toRecipients: "clark@saib.dev");

            using var createDoc = JsonDocument.Parse(createResult);
            draftId = createDoc.RootElement.GetProperty("id").GetString()!;

            var updateResult = await _draftTools.UpdateDraftMessage(
                messageId: draftId,
                ccRecipients: "clark@saib.dev",
                importance: "high");

            IntegrationFixture.AssertSuccess(updateResult);
            using var updateDoc = JsonDocument.Parse(updateResult);
            Assert.Equal("high", updateDoc.RootElement.GetProperty("importance").GetString());
        }
        finally
        {
            if (draftId is not null)
                await _mailTools.DeleteMailMessage(draftId);
        }
    }

    [Fact]
    public async Task CreateReplyDraftReturnsDraftWithId()
    {
        string? receivedId = null;
        string? replyDraftId = null;
        try
        {
            // Send a real message so we have a received message to reply to
            await _mailTools.SendMail(
                subject: "Helix Draft Test - Reply Original",
                body: "Original message body",
                toRecipients: "clark@saib.dev");

            await Task.Delay(5000, TestContext.Current.CancellationToken);
            receivedId = await FindTestEmail("Helix Draft Test - Reply Original");
            Assert.NotNull(receivedId);

            var replyResult = await _draftTools.CreateReplyDraft(
                messageId: receivedId!,
                comment: "This is a reply");

            IntegrationFixture.AssertSuccess(replyResult);
            using var replyDoc = JsonDocument.Parse(replyResult);
            replyDraftId = replyDoc.RootElement.GetProperty("id").GetString();
            Assert.NotNull(replyDraftId);

            var subject = replyDoc.RootElement.GetProperty("subject").GetString();
            Assert.StartsWith("Re:", subject, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (replyDraftId is not null)
                await _mailTools.DeleteMailMessage(replyDraftId);
            if (receivedId is not null)
                await _mailTools.DeleteMailMessage(receivedId);
        }
    }

    [Fact]
    public async Task CreateReplyAllDraftReturnsDraftWithId()
    {
        string? receivedId = null;
        string? replyDraftId = null;
        try
        {
            // Send a real message so we have a received message to reply-all to
            await _mailTools.SendMail(
                subject: "Helix Draft Test - ReplyAll Original",
                body: "Original message body",
                toRecipients: "clark@saib.dev");

            await Task.Delay(5000, TestContext.Current.CancellationToken);
            receivedId = await FindTestEmail("Helix Draft Test - ReplyAll Original");
            Assert.NotNull(receivedId);

            var replyResult = await _draftTools.CreateReplyAllDraft(
                messageId: receivedId!,
                comment: "This is a reply-all");

            IntegrationFixture.AssertSuccess(replyResult);
            using var replyDoc = JsonDocument.Parse(replyResult);
            replyDraftId = replyDoc.RootElement.GetProperty("id").GetString();
            Assert.NotNull(replyDraftId);

            var subject = replyDoc.RootElement.GetProperty("subject").GetString();
            Assert.StartsWith("Re:", subject, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (replyDraftId is not null)
                await _mailTools.DeleteMailMessage(replyDraftId);
            if (receivedId is not null)
                await _mailTools.DeleteMailMessage(receivedId);
        }
    }

    [Fact]
    public async Task CreateForwardDraftReturnsDraftWithId()
    {
        string? originalId = null;
        string? forwardDraftId = null;
        try
        {
            var originalResult = await _draftTools.CreateDraftMessage(
                subject: "Helix Draft Test - Forward Original",
                body: "Original message body",
                toRecipients: "clark@saib.dev");

            IntegrationFixture.AssertSuccess(originalResult);
            using var originalDoc = JsonDocument.Parse(originalResult);
            originalId = originalDoc.RootElement.GetProperty("id").GetString();

            var forwardResult = await _draftTools.CreateForwardDraft(
                messageId: originalId!,
                toRecipients: "clark@saib.dev",
                comment: "Forwarding this to you");

            IntegrationFixture.AssertSuccess(forwardResult);
            using var forwardDoc = JsonDocument.Parse(forwardResult);
            forwardDraftId = forwardDoc.RootElement.GetProperty("id").GetString();
            Assert.NotNull(forwardDraftId);

            var subject = forwardDoc.RootElement.GetProperty("subject").GetString();
            Assert.Contains("Fw", subject, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (forwardDraftId is not null)
                await _mailTools.DeleteMailMessage(forwardDraftId);
            if (originalId is not null)
                await _mailTools.DeleteMailMessage(originalId);
        }
    }

    [Fact]
    public async Task SendDraftSucceeds()
    {
        var createResult = await _draftTools.CreateDraftMessage(
            subject: "Helix Draft Test - Send",
            body: "Draft to be sent",
            toRecipients: "clark@saib.dev");

        using var createDoc = JsonDocument.Parse(createResult);
        var draftId = createDoc.RootElement.GetProperty("id").GetString()!;

        var sendResult = await _draftTools.SendDraftMessage(draftId);
        IntegrationFixture.AssertSuccessNoData(sendResult);

        // Cleanup: wait and delete the received email
        await Task.Delay(3000, TestContext.Current.CancellationToken);
        var listResult = await _mailTools.ListMailMessages(top: 5, search: "\"Helix Draft Test - Send\"");
        using var listDoc = JsonDocument.Parse(listResult);
        if (listDoc.RootElement.TryGetProperty("value", out var values))
        {
            foreach (var msg in values.EnumerateArray())
            {
                if (msg.TryGetProperty("subject", out var s) && s.GetString()?.Contains("Helix Draft Test - Send", StringComparison.Ordinal) == true)
                    await _mailTools.DeleteMailMessage(msg.GetProperty("id").GetString()!);
            }
        }
    }

    private async Task<string?> FindTestEmail(string subject)
    {
        var result = await _mailTools.ListMailMessages(top: 10, search: $"\"{subject}\"").ConfigureAwait(false);
        using var doc = JsonDocument.Parse(result);
        if (doc.RootElement.TryGetProperty("value", out var values))
        {
            foreach (var msg in values.EnumerateArray())
            {
                if (msg.TryGetProperty("subject", out var s) && s.GetString()?.Contains(subject, StringComparison.Ordinal) == true)
                    return msg.GetProperty("id").GetString();
            }
        }
        return null;
    }
}
