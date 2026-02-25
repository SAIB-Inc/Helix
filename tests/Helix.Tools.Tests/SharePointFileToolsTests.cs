using System.Text.Json;
using Helix.Tools.SharePoint;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class SharePointFileToolsTests(IntegrationFixture fixture)
{
    private readonly SharePointFileTools _tools = new(fixture.GraphClient);

    [Fact]
    public async Task ListSiteDrivesReturnsDrives()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
            return;

        var result = await _tools.ListSiteDrives(fixture.SiteId);

        var values = IntegrationFixture.AssertHasValues(result);
        Assert.True(values.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ListDriveChildrenRootReturnsItems()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var result = await _tools.ListDriveChildren(driveId);

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task ListDriveChildrenSubfolderReturnsItems()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        // Find a folder
        var rootResult = await _tools.ListDriveChildren(driveId);
        using var rootDoc = JsonDocument.Parse(rootResult);
        var items = rootDoc.RootElement.GetProperty("value");

        string? folderId = null;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("folder", out _))
            {
                folderId = item.GetProperty("id").GetString();
                break;
            }
        }
        if (folderId is null) return;

        var result = await _tools.ListDriveChildren(driveId, itemId: folderId);

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task SearchDriveItemsReturnsResults()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var result = await _tools.SearchDriveItems(driveId, "test");

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task GetDriveItemReturnsItem()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        // Get first item
        var rootResult = await _tools.ListDriveChildren(driveId);
        using var rootDoc = JsonDocument.Parse(rootResult);
        var items = rootDoc.RootElement.GetProperty("value");
        if (items.GetArrayLength() == 0) return;

        var itemId = items[0].GetProperty("id").GetString()!;

        var result = await _tools.GetDriveItem(driveId, itemId);

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task DownloadDriveItemReturnsDownloadUrl()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        // Find a file (not folder)
        var rootResult = await _tools.ListDriveChildren(driveId);
        using var rootDoc = JsonDocument.Parse(rootResult);
        var items = rootDoc.RootElement.GetProperty("value");

        string? fileId = null;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("file", out _))
            {
                fileId = item.GetProperty("id").GetString();
                break;
            }
        }

        // If no files at root, look inside first folder
        if (fileId is null)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("folder", out _)) continue;
                var folderId = item.GetProperty("id").GetString()!;
                var subResult = await _tools.ListDriveChildren(driveId, itemId: folderId);
                using var subDoc = JsonDocument.Parse(subResult);
                var subItems = subDoc.RootElement.GetProperty("value");
                foreach (var subItem in subItems.EnumerateArray())
                {
                    if (subItem.TryGetProperty("file", out _))
                    {
                        fileId = subItem.GetProperty("id").GetString();
                        break;
                    }
                }
                if (fileId is not null) break;
            }
        }

        if (fileId is null) return; // No files available

        var result = await _tools.DownloadDriveItem(driveId, fileId);

        Assert.Contains("DownloadUrl:", result, StringComparison.Ordinal);
        Assert.Contains("Name:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateDriveFolderSucceeds()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var shortId = Guid.NewGuid().ToString("N")[..8];
        var folderName = $"Helix-Test-{shortId}";
        var result = await _tools.CreateDriveFolder(driveId, folderName);

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("id", out _));
        Assert.Equal(folderName, doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UploadDriveItemBase64Succeeds()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var b64 = Convert.ToBase64String("Upload test content"u8.ToArray());
        var result = await _tools.UploadDriveItem(
            driveId: driveId,
            fileName: $"helix-test-{Guid.NewGuid().ToString("N")[..8]}.txt",
            contentBase64: b64);

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task UploadDriveItemFilePathSucceeds()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var repoRoot = FindRepoRoot();
        var result = await _tools.UploadDriveItem(
            driveId: driveId,
            fileName: $"helix-readme-{Guid.NewGuid().ToString("N")[..8]}.md",
            filePath: Path.Combine(repoRoot, "README.md"));

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task CreateFolderUploadBase64ListGetDownloadFullCycle()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var shortId = Guid.NewGuid().ToString("N")[..8];
        var folderName = $"Helix-Test-{shortId}";

        // Create folder
        var createFolderResult = await _tools.CreateDriveFolder(driveId, folderName);
        IntegrationFixture.AssertSuccess(createFolderResult);
        using var folderDoc = JsonDocument.Parse(createFolderResult);
        var folderId = folderDoc.RootElement.GetProperty("id").GetString()!;
        Assert.Equal(folderName, folderDoc.RootElement.GetProperty("name").GetString());

        // Upload file via base64 into the folder
        var b64 = Convert.ToBase64String("Hello World!"u8.ToArray());
        var uploadResult = await _tools.UploadDriveItem(
            driveId: driveId,
            fileName: "helloworld.txt",
            parentItemId: folderId,
            contentBase64: b64);
        IntegrationFixture.AssertSuccess(uploadResult);
        using var uploadDoc = JsonDocument.Parse(uploadResult);
        var fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;
        Assert.Equal("helloworld.txt", uploadDoc.RootElement.GetProperty("name").GetString());

        // List folder children — should contain the uploaded file
        var listResult = await _tools.ListDriveChildren(driveId, itemId: folderId);
        IntegrationFixture.AssertSuccess(listResult);
        using var listDoc = JsonDocument.Parse(listResult);
        var children = listDoc.RootElement.GetProperty("value");
        Assert.True(children.GetArrayLength() >= 1);
        Assert.Contains(children.EnumerateArray(),
            item => item.GetProperty("name").GetString() == "helloworld.txt");

        // Get item metadata
        var getResult = await _tools.GetDriveItem(driveId, fileId);
        IntegrationFixture.AssertSuccess(getResult);
        using var getDoc = JsonDocument.Parse(getResult);
        Assert.Equal("helloworld.txt", getDoc.RootElement.GetProperty("name").GetString());

        // Download item
        var downloadResult = await _tools.DownloadDriveItem(driveId, fileId);
        Assert.Contains("DownloadUrl:", downloadResult, StringComparison.Ordinal);
        Assert.Contains("Name: helloworld.txt", downloadResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateFolderUploadFilePathListGetDownloadFullCycle()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var shortId = Guid.NewGuid().ToString("N")[..8];
        var folderName = $"Helix-Test-{shortId}";

        // Create folder
        var createFolderResult = await _tools.CreateDriveFolder(driveId, folderName);
        IntegrationFixture.AssertSuccess(createFolderResult);
        using var folderDoc = JsonDocument.Parse(createFolderResult);
        var folderId = folderDoc.RootElement.GetProperty("id").GetString()!;

        // Create a temp file on disk and upload via filePath
        var tempFile = Path.Combine(Path.GetTempPath(), $"helix-test-{shortId}.txt");
        await File.WriteAllTextAsync(tempFile, "Hello World from file path!", TestContext.Current.CancellationToken);
        try
        {
            var uploadResult = await _tools.UploadDriveItem(
                driveId: driveId,
                fileName: "helloworld.txt",
                parentItemId: folderId,
                filePath: tempFile);
            IntegrationFixture.AssertSuccess(uploadResult);
            using var uploadDoc = JsonDocument.Parse(uploadResult);
            var fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;
            Assert.Equal("helloworld.txt", uploadDoc.RootElement.GetProperty("name").GetString());

            // List folder children
            var listResult = await _tools.ListDriveChildren(driveId, itemId: folderId);
            IntegrationFixture.AssertSuccess(listResult);
            using var listDoc = JsonDocument.Parse(listResult);
            var children = listDoc.RootElement.GetProperty("value");
            Assert.True(children.GetArrayLength() >= 1);
            Assert.Contains(children.EnumerateArray(),
                item => item.GetProperty("name").GetString() == "helloworld.txt");

            // Get item metadata
            var getResult = await _tools.GetDriveItem(driveId, fileId);
            IntegrationFixture.AssertSuccess(getResult);
            using var getDoc = JsonDocument.Parse(getResult);
            Assert.Equal("helloworld.txt", getDoc.RootElement.GetProperty("name").GetString());

            // Download item
            var downloadResult = await _tools.DownloadDriveItem(driveId, fileId);
            Assert.Contains("DownloadUrl:", downloadResult, StringComparison.Ordinal);
            Assert.Contains("Name: helloworld.txt", downloadResult, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateNestedFolderAndUploadFullCycle()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var shortId = Guid.NewGuid().ToString("N")[..8];

        // Create parent folder
        var parentResult = await _tools.CreateDriveFolder(driveId, $"Helix-Test-Parent-{shortId}");
        IntegrationFixture.AssertSuccess(parentResult);
        using var parentDoc = JsonDocument.Parse(parentResult);
        var parentId = parentDoc.RootElement.GetProperty("id").GetString()!;

        // Create child folder inside parent
        var childResult = await _tools.CreateDriveFolder(driveId, "example", parentItemId: parentId);
        IntegrationFixture.AssertSuccess(childResult);
        using var childDoc = JsonDocument.Parse(childResult);
        var childId = childDoc.RootElement.GetProperty("id").GetString()!;
        Assert.Equal("example", childDoc.RootElement.GetProperty("name").GetString());

        // Upload helloworld.txt into the nested "example" folder
        var b64 = Convert.ToBase64String("Hello World!"u8.ToArray());
        var uploadResult = await _tools.UploadDriveItem(
            driveId: driveId,
            fileName: "helloworld.txt",
            parentItemId: childId,
            contentBase64: b64);
        IntegrationFixture.AssertSuccess(uploadResult);
        using var uploadDoc = JsonDocument.Parse(uploadResult);
        var fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;

        // Verify parent folder contains "example" subfolder
        var parentChildren = await _tools.ListDriveChildren(driveId, itemId: parentId);
        IntegrationFixture.AssertSuccess(parentChildren);
        using var parentChildrenDoc = JsonDocument.Parse(parentChildren);
        var parentItems = parentChildrenDoc.RootElement.GetProperty("value");
        Assert.Contains(parentItems.EnumerateArray(),
            item => item.GetProperty("name").GetString() == "example");

        // Verify "example" folder contains helloworld.txt
        var childChildren = await _tools.ListDriveChildren(driveId, itemId: childId);
        IntegrationFixture.AssertSuccess(childChildren);
        using var childChildrenDoc = JsonDocument.Parse(childChildren);
        var childItems = childChildrenDoc.RootElement.GetProperty("value");
        Assert.Contains(childItems.EnumerateArray(),
            item => item.GetProperty("name").GetString() == "helloworld.txt");

        // Get and download the nested file
        var getResult = await _tools.GetDriveItem(driveId, fileId);
        IntegrationFixture.AssertSuccess(getResult);

        var downloadResult = await _tools.DownloadDriveItem(driveId, fileId);
        Assert.Contains("DownloadUrl:", downloadResult, StringComparison.Ordinal);
        Assert.Contains("Name: helloworld.txt", downloadResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadDriveItemMissingContentReturnsError()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        // Neither filePath nor contentBase64 provided
        var result = await _tools.UploadDriveItem(driveId: driveId, fileName: "should-fail.txt");

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var err) && err.GetBoolean());
    }

    [Fact]
    public async Task UploadDriveItemInvalidBase64ReturnsError()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var result = await _tools.UploadDriveItem(
            driveId: driveId,
            fileName: "should-fail.txt",
            contentBase64: "!!!not-valid-base64!!!");

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var err) && err.GetBoolean());
    }

    [Fact]
    public async Task UploadDriveItemFileNotFoundReturnsError()
    {
        var driveId = await GetDriveId();
        if (driveId is null) return;

        var result = await _tools.UploadDriveItem(
            driveId: driveId,
            fileName: "should-fail.txt",
            filePath: "/nonexistent/path/to/file.txt");

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var err) && err.GetBoolean());
    }

    private async Task<string?> GetDriveId()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
            return null;

        var result = await _tools.ListSiteDrives(fixture.SiteId).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(result);
        if (!doc.RootElement.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
            return null;

        return values[0].GetProperty("id").GetString();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "README.md")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Could not find repo root.");
    }
}
