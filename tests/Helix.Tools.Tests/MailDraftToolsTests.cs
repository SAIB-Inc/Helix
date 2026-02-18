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
}
