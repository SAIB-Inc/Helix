using System.Text.Json;
using Helix.Tools.Mail;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class MailToolsTests(IntegrationFixture fixture)
{
    private readonly MailTools _tools = new(fixture.GraphClient);

    [Fact]
    public async Task ListMailMessagesReturnsMessages()
    {
        string result = await _tools.ListMailMessages(top: 3);

        JsonElement values = IntegrationFixture.AssertHasValues(result);
        Assert.True(values.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ListMailMessagesWithFilterReturnsMessages()
    {
        string result = await _tools.ListMailMessages(top: 2, filter: "isRead eq false");

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task ListMailMessagesWithOrderByReturnsMessages()
    {
        string result = await _tools.ListMailMessages(top: 2, orderby: "receivedDateTime desc");

        JsonElement values = IntegrationFixture.AssertHasValues(result);
        Assert.True(values.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetMailMessageDefaultMarkdownConvertsHtmlToMarkdown()
    {
        // Send an HTML email so we have a guaranteed HTML message
        MailDraftTools draftTools = new(fixture.GraphClient);
        string draftResult = await draftTools.CreateDraftMessage(
            subject: "Helix Test - HTML Body",
            body: "<h1>Hello</h1><p>HTML content</p>",
            toRecipients: "clark@saib.dev",
            bodyContentType: "html");
        IntegrationFixture.AssertSuccess(draftResult);
        using JsonDocument draftDoc = JsonDocument.Parse(draftResult);
        string messageId = draftDoc.RootElement.GetProperty("id").GetString()!;

        try
        {
            string result = await _tools.GetMailMessage(messageId);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            Assert.True(doc.RootElement.TryGetProperty("body", out JsonElement body));
            // Default should convert HTML to text (markdown)
            Assert.Equal("text", body.GetProperty("contentType").GetString());
        }
        finally
        {
            _ = await _tools.DeleteMailMessage(messageId);
        }
    }

    [Fact]
    public async Task GetMailMessageFullHtmlBoolReturnsHtml()
    {
        MailDraftTools draftTools = new(fixture.GraphClient);
        string draftResult = await draftTools.CreateDraftMessage(
            subject: "Helix Test - HTML Full Bool",
            body: "<h1>Hello</h1><p>HTML content</p>",
            toRecipients: "clark@saib.dev",
            bodyContentType: "html");
        IntegrationFixture.AssertSuccess(draftResult);
        using JsonDocument draftDoc = JsonDocument.Parse(draftResult);
        string messageId = draftDoc.RootElement.GetProperty("id").GetString()!;

        try
        {
            string result = await _tools.GetMailMessage(messageId, includeFullHtml: true);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            JsonElement body = doc.RootElement.GetProperty("body");
            Assert.Equal("html", body.GetProperty("contentType").GetString());
        }
        finally
        {
            _ = await _tools.DeleteMailMessage(messageId);
        }
    }

    [Fact]
    public async Task GetMailMessageFullHtmlStringCoworkCompat()
    {
        MailDraftTools draftTools = new(fixture.GraphClient);
        string draftResult = await draftTools.CreateDraftMessage(
            subject: "Helix Test - HTML Full String",
            body: "<h1>Hello</h1><p>HTML content</p>",
            toRecipients: "clark@saib.dev",
            bodyContentType: "html");
        IntegrationFixture.AssertSuccess(draftResult);
        using JsonDocument draftDoc = JsonDocument.Parse(draftResult);
        string messageId = draftDoc.RootElement.GetProperty("id").GetString()!;

        try
        {
            // Test with string "true" (Cowork proxy sends booleans as strings)
            string result = await _tools.GetMailMessage(messageId, includeFullHtml: "true");

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            JsonElement body = doc.RootElement.GetProperty("body");
            Assert.Equal("html", body.GetProperty("contentType").GetString());
        }
        finally
        {
            _ = await _tools.DeleteMailMessage(messageId);
        }
    }

    [Fact]
    public async Task SendMailPlainTextSucceeds()
    {
        string result = await _tools.SendMail(
            subject: "Helix Integration Test - Plain",
            body: "Automated test from Helix.Tools.Tests.",
            toRecipients: "clark@saib.dev",
            importance: "low");

        IntegrationFixture.AssertSuccessNoData(result);

        // Cleanup: find and delete
        await Task.Delay(3000, TestContext.Current.CancellationToken);
        await CleanupTestEmails("Helix Integration Test - Plain");
    }

    [Fact]
    public async Task SendMailHtmlSucceeds()
    {
        string result = await _tools.SendMail(
            subject: "Helix Integration Test - HTML",
            body: "<h1>Test</h1><p>HTML body test.</p>",
            toRecipients: "clark@saib.dev",
            bodyContentType: "html",
            importance: "low");

        IntegrationFixture.AssertSuccessNoData(result);

        await Task.Delay(3000, TestContext.Current.CancellationToken);
        await CleanupTestEmails("Helix Integration Test - HTML");
    }

    [Fact]
    public async Task UpdateMailMessageIsReadReturnsUpdatedMessage()
    {
        // Send a test email, wait, then update it
        _ = await _tools.SendMail(
            subject: "Helix Integration Test - Update",
            body: "Test for update.",
            toRecipients: "clark@saib.dev",
            importance: "low");
        await Task.Delay(5000, TestContext.Current.CancellationToken);

        string? messageId = null;
        try
        {
            messageId = await FindTestEmail("Helix Integration Test - Update");
            Assert.NotNull(messageId);

            string result = await _tools.UpdateMailMessage(messageId!, isRead: true);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            Assert.True(doc.RootElement.GetProperty("isRead").GetBoolean());
        }
        finally
        {
            if (messageId is not null)
            {
                _ = await _tools.DeleteMailMessage(messageId);
            }
        }
    }

    [Fact]
    public async Task MoveMailMessageReturnsNewId()
    {
        _ = await _tools.SendMail(
            subject: "Helix Integration Test - Move",
            body: "Test for move.",
            toRecipients: "clark@saib.dev",
            importance: "low");
        await Task.Delay(5000, TestContext.Current.CancellationToken);

        string? currentId = null;
        try
        {
            currentId = await FindTestEmail("Helix Integration Test - Move");
            Assert.NotNull(currentId);

            // Move to archive
            string moveResult = await _tools.MoveMailMessage(currentId!, "archive");
            IntegrationFixture.AssertSuccess(moveResult);

            using JsonDocument moveDoc = JsonDocument.Parse(moveResult);
            string movedId = moveDoc.RootElement.GetProperty("id").GetString()!;
            Assert.NotEqual(currentId, movedId);

            // Move back to inbox
            string moveBackResult = await _tools.MoveMailMessage(movedId, "inbox");
            IntegrationFixture.AssertSuccess(moveBackResult);

            using JsonDocument backDoc = JsonDocument.Parse(moveBackResult);
            currentId = backDoc.RootElement.GetProperty("id").GetString();
        }
        finally
        {
            if (currentId is not null)
            {
                _ = await _tools.DeleteMailMessage(currentId);
            }
        }
    }

    [Fact]
    public async Task DeleteMailMessageSucceeds()
    {
        _ = await _tools.SendMail(
            subject: "Helix Integration Test - Delete",
            body: "Test for delete.",
            toRecipients: "clark@saib.dev",
            importance: "low");
        await Task.Delay(5000, TestContext.Current.CancellationToken);

        string? messageId = await FindTestEmail("Helix Integration Test - Delete");
        Assert.NotNull(messageId);

        string result = await _tools.DeleteMailMessage(messageId!);
        IntegrationFixture.AssertSuccessNoData(result);
    }

    private async Task<string?> FindTestEmail(string subject)
    {
        string result = await _tools.ListMailMessages(top: 10, search: $"\"{subject}\"").ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(result);
        if (doc.RootElement.TryGetProperty("value", out JsonElement values))
        {
            foreach (JsonElement msg in values.EnumerateArray())
            {
                if (msg.TryGetProperty("subject", out JsonElement s) && s.GetString()?.Contains(subject, StringComparison.Ordinal) == true)
                {
                    return msg.GetProperty("id").GetString();
                }
            }
        }

        return null;
    }

    private async Task CleanupTestEmails(string subject)
    {
        string? id = await FindTestEmail(subject).ConfigureAwait(false);
        if (id is not null)
        {
            _ = await _tools.DeleteMailMessage(id).ConfigureAwait(false);
        }
    }
}
