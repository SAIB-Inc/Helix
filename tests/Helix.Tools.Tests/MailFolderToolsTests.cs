using System.Text.Json;
using Helix.Tools.Mail;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class MailFolderToolsTests(IntegrationFixture fixture)
{
    private readonly MailFolderTools _tools = new(fixture.GraphClient);

    [Fact]
    public async Task ListMailFoldersReturnsFolders()
    {
        string result = await _tools.ListMailFolders();

        JsonElement values = IntegrationFixture.AssertHasValues(result);
        Assert.True(values.GetArrayLength() > 0);

        // Should contain well-known folders
        List<string> folderNames = [];
        foreach (JsonElement folder in values.EnumerateArray())
        {
            if (folder.TryGetProperty("displayName", out JsonElement name))
            {
                folderNames.Add(name.GetString()!);
            }
        }

        Assert.Contains("Inbox", folderNames);
    }

    [Fact]
    public async Task ListMailFolderMessagesInboxReturnsMessages()
    {
        string result = await _tools.ListMailFolderMessages("inbox", top: 2);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task ListMailFolderMessagesSentItemsReturnsMessages()
    {
        string result = await _tools.ListMailFolderMessages("sentitems", top: 2);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task ListMailFolderMessagesDraftsReturnsMessages()
    {
        string result = await _tools.ListMailFolderMessages("drafts", top: 2);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }
}
