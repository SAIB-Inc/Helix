using System.Text.Json;
using Helix.Tools.SharePoint;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class SharePointSharingToolsTests(IntegrationFixture fixture)
{
    private readonly SharePointSharingTools _tools = new(fixture.GraphClient);
    private readonly SharePointFileTools _fileTools = new(fixture.GraphClient);

    [Fact]
    public async Task CreateSharingLinkViewOrganizationSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            fileId = await UploadTestFile(driveId);

            string result = await _tools.CreateSharingLink(driveId, fileId, type: "view", scope: "organization");

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            Assert.True(doc.RootElement.TryGetProperty("link", out JsonElement link));
            Assert.True(link.TryGetProperty("webUrl", out _));
            Assert.Equal("view", link.GetProperty("type").GetString());
        }
        finally
        {
            await CleanupFile(driveId, fileId);
        }
    }

    [Fact]
    public async Task CreateSharingLinkEditOrganizationSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            fileId = await UploadTestFile(driveId);

            string result = await _tools.CreateSharingLink(driveId, fileId, type: "edit", scope: "organization");

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            Assert.True(doc.RootElement.TryGetProperty("link", out JsonElement link));
            Assert.True(link.TryGetProperty("webUrl", out _));
        }
        finally
        {
            await CleanupFile(driveId, fileId);
        }
    }

    [Fact]
    public async Task CreateSharingLinkWithExpirationSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            fileId = await UploadTestFile(driveId);

            string expiration = DateTimeOffset.UtcNow.AddDays(7).ToString("O");
            string result = await _tools.CreateSharingLink(driveId, fileId, type: "view", scope: "organization", expirationDateTime: expiration);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            Assert.True(doc.RootElement.TryGetProperty("link", out _));
        }
        finally
        {
            await CleanupFile(driveId, fileId);
        }
    }

    [Fact]
    public async Task CreateSharingLinkInvalidExpirationReturnsError()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string result = await _tools.CreateSharingLink(driveId, "fake-item-id", type: "view", scope: "anonymous", expirationDateTime: "not-a-date");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out JsonElement err) && err.GetBoolean());
    }

    [Fact]
    public async Task ListSharingLinksReturnsLinks()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            fileId = await UploadTestFile(driveId);

            // Create a link first
            string createResult = await _tools.CreateSharingLink(driveId, fileId, type: "view", scope: "organization");
            IntegrationFixture.AssertSuccess(createResult);

            // List links
            string listResult = await _tools.ListSharingLinks(driveId, fileId);

            IntegrationFixture.AssertSuccess(listResult);
            using JsonDocument doc = JsonDocument.Parse(listResult);
            Assert.True(doc.RootElement.TryGetProperty("value", out JsonElement values));
            Assert.True(values.GetArrayLength() > 0);

            // Each link should have an id and link property
            JsonElement firstLink = values[0];
            Assert.True(firstLink.TryGetProperty("id", out _));
            Assert.True(firstLink.TryGetProperty("link", out _));
        }
        finally
        {
            await CleanupFile(driveId, fileId);
        }
    }

    [Fact]
    public async Task ListSharingLinksNoLinksReturnsEmptyArray()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            fileId = await UploadTestFile(driveId);

            string result = await _tools.ListSharingLinks(driveId, fileId);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            Assert.True(doc.RootElement.TryGetProperty("value", out JsonElement values));
            Assert.Equal(JsonValueKind.Array, values.ValueKind);
        }
        finally
        {
            await CleanupFile(driveId, fileId);
        }
    }

    [Fact]
    public async Task DeleteSharingLinkSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            fileId = await UploadTestFile(driveId);

            // Create a link
            string createResult = await _tools.CreateSharingLink(driveId, fileId, type: "view", scope: "organization");
            IntegrationFixture.AssertSuccess(createResult);
            using JsonDocument createDoc = JsonDocument.Parse(createResult);
            string permissionId = createDoc.RootElement.GetProperty("id").GetString()!;

            // Delete the link
            string deleteResult = await _tools.DeleteSharingLink(driveId, fileId, permissionId);
            IntegrationFixture.AssertSuccessNoData(deleteResult);

            // Verify the link is gone
            string listResult = await _tools.ListSharingLinks(driveId, fileId);
            IntegrationFixture.AssertSuccess(listResult);
            using JsonDocument listDoc = JsonDocument.Parse(listResult);
            JsonElement values = listDoc.RootElement.GetProperty("value");

            // Should not contain the deleted permission
            foreach (JsonElement perm in values.EnumerateArray())
            {
                Assert.NotEqual(permissionId, perm.GetProperty("id").GetString());
            }
        }
        finally
        {
            await CleanupFile(driveId, fileId);
        }
    }

    [Fact]
    public async Task DeleteSharingLinkInvalidIdReturnsError()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            fileId = await UploadTestFile(driveId);

            string result = await _tools.DeleteSharingLink(driveId, fileId, "nonexistent-permission-id");

            using JsonDocument doc = JsonDocument.Parse(result);
            Assert.True(doc.RootElement.TryGetProperty("error", out JsonElement err) && err.GetBoolean());
        }
        finally
        {
            await CleanupFile(driveId, fileId);
        }
    }

    [Fact]
    public async Task CreateListDeleteFullCycle()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            fileId = await UploadTestFile(driveId);

            // Create two sharing links
            string link1Result = await _tools.CreateSharingLink(driveId, fileId, type: "view", scope: "organization");
            IntegrationFixture.AssertSuccess(link1Result);
            using JsonDocument link1Doc = JsonDocument.Parse(link1Result);
            string link1Id = link1Doc.RootElement.GetProperty("id").GetString()!;
            string link1Url = link1Doc.RootElement.GetProperty("link").GetProperty("webUrl").GetString()!;
            Assert.NotEmpty(link1Url);

            string link2Result = await _tools.CreateSharingLink(driveId, fileId, type: "edit", scope: "organization");
            IntegrationFixture.AssertSuccess(link2Result);
            using JsonDocument link2Doc = JsonDocument.Parse(link2Result);
            string link2Id = link2Doc.RootElement.GetProperty("id").GetString()!;

            // List — should have at least 2 links
            string listResult = await _tools.ListSharingLinks(driveId, fileId);
            IntegrationFixture.AssertSuccess(listResult);
            using JsonDocument listDoc = JsonDocument.Parse(listResult);
            JsonElement links = listDoc.RootElement.GetProperty("value");
            Assert.True(links.GetArrayLength() >= 2);

            // Delete first link
            string delete1 = await _tools.DeleteSharingLink(driveId, fileId, link1Id);
            IntegrationFixture.AssertSuccessNoData(delete1);

            // Delete second link
            string delete2 = await _tools.DeleteSharingLink(driveId, fileId, link2Id);
            IntegrationFixture.AssertSuccessNoData(delete2);

            // List again — should have no links
            string finalList = await _tools.ListSharingLinks(driveId, fileId);
            IntegrationFixture.AssertSuccess(finalList);
            using JsonDocument finalDoc = JsonDocument.Parse(finalList);
            JsonElement finalLinks = finalDoc.RootElement.GetProperty("value");
            Assert.Equal(0, finalLinks.GetArrayLength());
        }
        finally
        {
            await CleanupFile(driveId, fileId);
        }
    }

    private async Task<string?> GetDriveId()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
        {
            return null;
        }

        string result = await _fileTools.ListSiteDrives(fixture.SiteId).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(result);
        return !doc.RootElement.TryGetProperty("value", out JsonElement values) || values.GetArrayLength() == 0
            ? null
            : values[0].GetProperty("id").GetString();
    }

    private async Task<string> UploadTestFile(string driveId)
    {
        string b64 = Convert.ToBase64String("Sharing link test content"u8.ToArray());
        string result = await _fileTools.UploadDriveItem(
            driveId: driveId,
            fileName: $"helix-sharing-test-{Guid.NewGuid().ToString("N")[..8]}.txt",
            contentBase64: b64).ConfigureAwait(false);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private async Task CleanupFile(string driveId, string? fileId)
    {
        if (fileId is not null)
        {
            _ = await _fileTools.DeleteDriveItem(driveId, fileId).ConfigureAwait(false);
        }
    }
}
