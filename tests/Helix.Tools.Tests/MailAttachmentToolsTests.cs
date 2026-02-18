using System.Text.Json;
using Helix.Tools.Mail;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class MailAttachmentToolsTests(IntegrationFixture fixture)
{
    private readonly MailAttachmentTools _attachTools = new(fixture.GraphClient);
    private readonly MailDraftTools _draftTools = new(fixture.GraphClient);
    private readonly MailTools _mailTools = new(fixture.GraphClient);

    [Fact]
    public async Task AddAttachmentBase64Succeeds()
    {
        string? draftId = null;
        try
        {
            draftId = await CreateTestDraft();
            var b64 = Convert.ToBase64String("Hello from test!"u8.ToArray());

            var result = await _attachTools.AddMailAttachment(
                messageId: draftId,
                contentType: "text/plain",
                contentBase64: b64,
                fileName: "test.txt");

            IntegrationFixture.AssertSuccessNoData(result);
        }
        finally
        {
            if (draftId is not null)
                await _mailTools.DeleteMailMessage(draftId);
        }
    }

    [Fact]
    public async Task AddAttachmentFilePathSucceeds()
    {
        string? draftId = null;
        try
        {
            draftId = await CreateTestDraft();

            var result = await _attachTools.AddMailAttachment(
                messageId: draftId,
                contentType: "text/markdown",
                filePath: Path.Combine(FindRepoRoot(), "README.md"));

            IntegrationFixture.AssertSuccessNoData(result);
        }
        finally
        {
            if (draftId is not null)
                await _mailTools.DeleteMailMessage(draftId);
        }
    }

    [Fact]
    public async Task ListAttachmentsReturnsAttachments()
    {
        string? draftId = null;
        try
        {
            draftId = await CreateTestDraft();
            var b64 = Convert.ToBase64String("attachment content"u8.ToArray());
            await _attachTools.AddMailAttachment(draftId, "text/plain", contentBase64: b64, fileName: "file.txt");

            var result = await _attachTools.ListMailAttachments(draftId);

            var values = IntegrationFixture.AssertHasValues(result);
            Assert.True(values.GetArrayLength() > 0);
            Assert.True(values[0].TryGetProperty("name", out _));
        }
        finally
        {
            if (draftId is not null)
                await _mailTools.DeleteMailMessage(draftId);
        }
    }

    [Fact]
    public async Task GetAttachmentReturnsBase64Content()
    {
        string? draftId = null;
        try
        {
            draftId = await CreateTestDraft();
            var originalContent = "base64 test content";
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalContent));
            await _attachTools.AddMailAttachment(draftId, "text/plain", contentBase64: b64, fileName: "b64test.txt");

            // Get attachment ID
            var listResult = await _attachTools.ListMailAttachments(draftId);
            using var listDoc = JsonDocument.Parse(listResult);
            var attachmentId = listDoc.RootElement.GetProperty("value")[0].GetProperty("id").GetString()!;

            var result = await _attachTools.GetMailAttachment(draftId, attachmentId);

            Assert.Contains("ContentBase64:", result, StringComparison.Ordinal);
            Assert.Contains("Name: b64test.txt", result, StringComparison.Ordinal);
            Assert.Contains("ContentType: text/plain", result, StringComparison.Ordinal);
        }
        finally
        {
            if (draftId is not null)
                await _mailTools.DeleteMailMessage(draftId);
        }
    }

    [Fact]
    public async Task DeleteAttachmentSucceeds()
    {
        string? draftId = null;
        try
        {
            draftId = await CreateTestDraft();
            var b64 = Convert.ToBase64String("delete me"u8.ToArray());
            await _attachTools.AddMailAttachment(draftId, "text/plain", contentBase64: b64, fileName: "del.txt");

            var listResult = await _attachTools.ListMailAttachments(draftId);
            using var listDoc = JsonDocument.Parse(listResult);
            var attachmentId = listDoc.RootElement.GetProperty("value")[0].GetProperty("id").GetString()!;

            var result = await _attachTools.DeleteMailAttachment(draftId, attachmentId);

            IntegrationFixture.AssertSuccessNoData(result);
        }
        finally
        {
            if (draftId is not null)
                await _mailTools.DeleteMailMessage(draftId);
        }
    }

    private async Task<string> CreateTestDraft()
    {
        var result = await _draftTools.CreateDraftMessage(
            subject: "Helix Attachment Test",
            body: "Attachment test draft",
            toRecipients: "clark@saib.dev").ConfigureAwait(false);
        using var doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "README.md")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Could not find repo root.");
    }
}
