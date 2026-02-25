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
        {
            return;
        }

        string result = await _tools.ListSiteDrives(fixture.SiteId);

        JsonElement values = IntegrationFixture.AssertHasValues(result);
        Assert.True(values.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ListDriveChildrenRootReturnsItems()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string result = await _tools.ListDriveChildren(driveId);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task ListDriveChildrenSubfolderReturnsItems()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        // Find a folder
        string rootResult = await _tools.ListDriveChildren(driveId);
        using JsonDocument rootDoc = JsonDocument.Parse(rootResult);
        JsonElement items = rootDoc.RootElement.GetProperty("value");

        string? folderId = null;
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.TryGetProperty("folder", out _))
            {
                folderId = item.GetProperty("id").GetString();
                break;
            }
        }

        if (folderId is null)
        {
            return;
        }

        string result = await _tools.ListDriveChildren(driveId, itemId: folderId);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task SearchDriveItemsReturnsResults()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string result = await _tools.SearchDriveItems(driveId, "test");

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task GetDriveItemReturnsItem()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        // Get first item
        string rootResult = await _tools.ListDriveChildren(driveId);
        using JsonDocument rootDoc = JsonDocument.Parse(rootResult);
        JsonElement items = rootDoc.RootElement.GetProperty("value");
        if (items.GetArrayLength() == 0)
        {
            return;
        }

        string itemId = items[0].GetProperty("id").GetString()!;

        string result = await _tools.GetDriveItem(driveId, itemId);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task DownloadDriveItemReturnsDownloadUrl()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        // Find a file (not folder)
        string rootResult = await _tools.ListDriveChildren(driveId);
        using JsonDocument rootDoc = JsonDocument.Parse(rootResult);
        JsonElement items = rootDoc.RootElement.GetProperty("value");

        string? fileId = null;
        foreach (JsonElement item in items.EnumerateArray())
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
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("folder", out _))
                {
                    continue;
                }

                string folderId = item.GetProperty("id").GetString()!;
                string subResult = await _tools.ListDriveChildren(driveId, itemId: folderId);
                using JsonDocument subDoc = JsonDocument.Parse(subResult);
                JsonElement subItems = subDoc.RootElement.GetProperty("value");
                foreach (JsonElement subItem in subItems.EnumerateArray())
                {
                    if (subItem.TryGetProperty("file", out _))
                    {
                        fileId = subItem.GetProperty("id").GetString();
                        break;
                    }
                }

                if (fileId is not null)
                {
                    break;
                }
            }
        }

        if (fileId is null)
        {
            return; // No files available
        }

        string result = await _tools.DownloadDriveItem(driveId, fileId);

        Assert.Contains("DownloadUrl:", result, StringComparison.Ordinal);
        Assert.Contains("Name:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateDriveFolderSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? folderId = null;
        try
        {
            string shortId = Guid.NewGuid().ToString("N")[..8];
            string folderName = $"Helix-Test-{shortId}";
            string result = await _tools.CreateDriveFolder(driveId, folderName);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            folderId = doc.RootElement.GetProperty("id").GetString();
            Assert.True(folderId is not null);
            Assert.Equal(folderName, doc.RootElement.GetProperty("name").GetString());
        }
        finally
        {
            if (folderId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, folderId);
            }
        }
    }

    [Fact]
    public async Task UploadDriveItemBase64Succeeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            string b64 = Convert.ToBase64String("Upload test content"u8.ToArray());
            string result = await _tools.UploadDriveItem(
                driveId: driveId,
                fileName: $"helix-test-{Guid.NewGuid().ToString("N")[..8]}.txt",
                contentBase64: b64);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            fileId = doc.RootElement.GetProperty("id").GetString();
            Assert.True(fileId is not null);
        }
        finally
        {
            if (fileId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, fileId);
            }
        }
    }

    [Fact]
    public async Task UploadDriveItemFilePathSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string? fileId = null;
        try
        {
            string repoRoot = FindRepoRoot();
            string result = await _tools.UploadDriveItem(
                driveId: driveId,
                fileName: $"helix-readme-{Guid.NewGuid().ToString("N")[..8]}.md",
                filePath: Path.Combine(repoRoot, "README.md"));

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            fileId = doc.RootElement.GetProperty("id").GetString();
            Assert.True(fileId is not null);
        }
        finally
        {
            if (fileId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, fileId);
            }
        }
    }

    [Fact]
    public async Task CreateFolderUploadBase64ListGetDownloadFullCycle()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string shortId = Guid.NewGuid().ToString("N")[..8];
        string folderName = $"Helix-Test-{shortId}";
        string? folderId = null;
        try
        {
            // Create folder
            string createFolderResult = await _tools.CreateDriveFolder(driveId, folderName);
            IntegrationFixture.AssertSuccess(createFolderResult);
            using JsonDocument folderDoc = JsonDocument.Parse(createFolderResult);
            folderId = folderDoc.RootElement.GetProperty("id").GetString()!;
            Assert.Equal(folderName, folderDoc.RootElement.GetProperty("name").GetString());

            // Upload file via base64 into the folder
            string b64 = Convert.ToBase64String("Hello World!"u8.ToArray());
            string uploadResult = await _tools.UploadDriveItem(
                driveId: driveId,
                fileName: "helloworld.txt",
                parentItemId: folderId,
                contentBase64: b64);
            IntegrationFixture.AssertSuccess(uploadResult);
            using JsonDocument uploadDoc = JsonDocument.Parse(uploadResult);
            string fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;
            Assert.Equal("helloworld.txt", uploadDoc.RootElement.GetProperty("name").GetString());

            // List folder children — should contain the uploaded file
            string listResult = await _tools.ListDriveChildren(driveId, itemId: folderId);
            IntegrationFixture.AssertSuccess(listResult);
            using JsonDocument listDoc = JsonDocument.Parse(listResult);
            JsonElement children = listDoc.RootElement.GetProperty("value");
            Assert.True(children.GetArrayLength() >= 1);
            Assert.Contains(children.EnumerateArray(),
                item => item.GetProperty("name").GetString() == "helloworld.txt");

            // Get item metadata
            string getResult = await _tools.GetDriveItem(driveId, fileId);
            IntegrationFixture.AssertSuccess(getResult);
            using JsonDocument getDoc = JsonDocument.Parse(getResult);
            Assert.Equal("helloworld.txt", getDoc.RootElement.GetProperty("name").GetString());

            // Download item
            string downloadResult = await _tools.DownloadDriveItem(driveId, fileId);
            Assert.Contains("DownloadUrl:", downloadResult, StringComparison.Ordinal);
            Assert.Contains("Name: helloworld.txt", downloadResult, StringComparison.Ordinal);
        }
        finally
        {
            if (folderId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, folderId);
            }
        }
    }

    [Fact]
    public async Task CreateFolderUploadFilePathListGetDownloadFullCycle()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string shortId = Guid.NewGuid().ToString("N")[..8];
        string folderName = $"Helix-Test-{shortId}";
        string? folderId = null;

        // Create folder
        string createFolderResult = await _tools.CreateDriveFolder(driveId, folderName);
        IntegrationFixture.AssertSuccess(createFolderResult);
        using JsonDocument folderDoc = JsonDocument.Parse(createFolderResult);
        folderId = folderDoc.RootElement.GetProperty("id").GetString()!;

        // Create a temp file on disk and upload via filePath
        string tempFile = Path.Combine(Path.GetTempPath(), $"helix-test-{shortId}.txt");
        await File.WriteAllTextAsync(tempFile, "Hello World from file path!", TestContext.Current.CancellationToken);
        try
        {
            string uploadResult = await _tools.UploadDriveItem(
                driveId: driveId,
                fileName: "helloworld.txt",
                parentItemId: folderId,
                filePath: tempFile);
            IntegrationFixture.AssertSuccess(uploadResult);
            using JsonDocument uploadDoc = JsonDocument.Parse(uploadResult);
            string fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;
            Assert.Equal("helloworld.txt", uploadDoc.RootElement.GetProperty("name").GetString());

            // List folder children
            string listResult = await _tools.ListDriveChildren(driveId, itemId: folderId);
            IntegrationFixture.AssertSuccess(listResult);
            using JsonDocument listDoc = JsonDocument.Parse(listResult);
            JsonElement children = listDoc.RootElement.GetProperty("value");
            Assert.True(children.GetArrayLength() >= 1);
            Assert.Contains(children.EnumerateArray(),
                item => item.GetProperty("name").GetString() == "helloworld.txt");

            // Get item metadata
            string getResult = await _tools.GetDriveItem(driveId, fileId);
            IntegrationFixture.AssertSuccess(getResult);
            using JsonDocument getDoc = JsonDocument.Parse(getResult);
            Assert.Equal("helloworld.txt", getDoc.RootElement.GetProperty("name").GetString());

            // Download item
            string downloadResult = await _tools.DownloadDriveItem(driveId, fileId);
            Assert.Contains("DownloadUrl:", downloadResult, StringComparison.Ordinal);
            Assert.Contains("Name: helloworld.txt", downloadResult, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(tempFile);
            if (folderId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, folderId);
            }
        }
    }

    [Fact]
    public async Task CreateNestedFolderAndUploadFullCycle()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string shortId = Guid.NewGuid().ToString("N")[..8];
        string? parentId = null;
        try
        {
            // Create parent folder
            string parentResult = await _tools.CreateDriveFolder(driveId, $"Helix-Test-Parent-{shortId}");
            IntegrationFixture.AssertSuccess(parentResult);
            using JsonDocument parentDoc = JsonDocument.Parse(parentResult);
            parentId = parentDoc.RootElement.GetProperty("id").GetString()!;

            // Create child folder inside parent
            string childResult = await _tools.CreateDriveFolder(driveId, "example", parentItemId: parentId);
            IntegrationFixture.AssertSuccess(childResult);
            using JsonDocument childDoc = JsonDocument.Parse(childResult);
            string childId = childDoc.RootElement.GetProperty("id").GetString()!;
            Assert.Equal("example", childDoc.RootElement.GetProperty("name").GetString());

            // Upload helloworld.txt into the nested "example" folder
            string b64 = Convert.ToBase64String("Hello World!"u8.ToArray());
            string uploadResult = await _tools.UploadDriveItem(
                driveId: driveId,
                fileName: "helloworld.txt",
                parentItemId: childId,
                contentBase64: b64);
            IntegrationFixture.AssertSuccess(uploadResult);
            using JsonDocument uploadDoc = JsonDocument.Parse(uploadResult);
            string fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;

            // Verify parent folder contains "example" subfolder
            string parentChildren = await _tools.ListDriveChildren(driveId, itemId: parentId);
            IntegrationFixture.AssertSuccess(parentChildren);
            using JsonDocument parentChildrenDoc = JsonDocument.Parse(parentChildren);
            JsonElement parentItems = parentChildrenDoc.RootElement.GetProperty("value");
            Assert.Contains(parentItems.EnumerateArray(),
                item => item.GetProperty("name").GetString() == "example");

            // Verify "example" folder contains helloworld.txt
            string childChildren = await _tools.ListDriveChildren(driveId, itemId: childId);
            IntegrationFixture.AssertSuccess(childChildren);
            using JsonDocument childChildrenDoc = JsonDocument.Parse(childChildren);
            JsonElement childItems = childChildrenDoc.RootElement.GetProperty("value");
            Assert.Contains(childItems.EnumerateArray(),
                item => item.GetProperty("name").GetString() == "helloworld.txt");

            // Get and download the nested file
            string getResult = await _tools.GetDriveItem(driveId, fileId);
            IntegrationFixture.AssertSuccess(getResult);

            string downloadResult = await _tools.DownloadDriveItem(driveId, fileId);
            Assert.Contains("DownloadUrl:", downloadResult, StringComparison.Ordinal);
            Assert.Contains("Name: helloworld.txt", downloadResult, StringComparison.Ordinal);
        }
        finally
        {
            if (parentId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, parentId);
            }
        }
    }

    [Fact]
    public async Task UploadDriveItemMissingContentReturnsError()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        // Neither filePath nor contentBase64 provided
        string result = await _tools.UploadDriveItem(driveId: driveId, fileName: "should-fail.txt");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out JsonElement err) && err.GetBoolean());
    }

    [Fact]
    public async Task UploadDriveItemInvalidBase64ReturnsError()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string result = await _tools.UploadDriveItem(
            driveId: driveId,
            fileName: "should-fail.txt",
            contentBase64: "!!!not-valid-base64!!!");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out JsonElement err) && err.GetBoolean());
    }

    [Fact]
    public async Task UploadDriveItemFileNotFoundReturnsError()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string result = await _tools.UploadDriveItem(
            driveId: driveId,
            fileName: "should-fail.txt",
            filePath: "/nonexistent/path/to/file.txt");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out JsonElement err) && err.GetBoolean());
    }

    [Fact]
    public async Task MoveItemToNewFolderSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string shortId = Guid.NewGuid().ToString("N")[..8];
        string? folderAId = null;
        string? folderBId = null;
        try
        {
            // Create folder A and upload a file into it
            string folderAResult = await _tools.CreateDriveFolder(driveId, $"Helix-Test-A-{shortId}");
            IntegrationFixture.AssertSuccess(folderAResult);
            using JsonDocument folderADoc = JsonDocument.Parse(folderAResult);
            folderAId = folderADoc.RootElement.GetProperty("id").GetString()!;

            string b64 = Convert.ToBase64String("move me"u8.ToArray());
            string uploadResult = await _tools.UploadDriveItem(driveId, "movable.txt", parentItemId: folderAId, contentBase64: b64);
            IntegrationFixture.AssertSuccess(uploadResult);
            using JsonDocument uploadDoc = JsonDocument.Parse(uploadResult);
            string fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;

            // Create folder B
            string folderBResult = await _tools.CreateDriveFolder(driveId, $"Helix-Test-B-{shortId}");
            IntegrationFixture.AssertSuccess(folderBResult);
            using JsonDocument folderBDoc = JsonDocument.Parse(folderBResult);
            folderBId = folderBDoc.RootElement.GetProperty("id").GetString()!;

            // Move the file from folder A to folder B
            string moveResult = await _tools.MoveOrRenameDriveItem(driveId, fileId, destinationParentItemId: folderBId);
            IntegrationFixture.AssertSuccess(moveResult);
            using JsonDocument moveDoc = JsonDocument.Parse(moveResult);
            Assert.Equal("movable.txt", moveDoc.RootElement.GetProperty("name").GetString());

            // Verify file is now in folder B
            string childrenResult = await _tools.ListDriveChildren(driveId, itemId: folderBId);
            IntegrationFixture.AssertSuccess(childrenResult);
            using JsonDocument childrenDoc = JsonDocument.Parse(childrenResult);
            JsonElement children = childrenDoc.RootElement.GetProperty("value");
            Assert.Contains(children.EnumerateArray(),
                item => item.GetProperty("name").GetString() == "movable.txt");
        }
        finally
        {
            if (folderAId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, folderAId);
            }

            if (folderBId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, folderBId);
            }
        }
    }

    [Fact]
    public async Task RenameItemSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string shortId = Guid.NewGuid().ToString("N")[..8];
        string? folderId = null;
        try
        {
            // Create folder and upload a file
            string folderResult = await _tools.CreateDriveFolder(driveId, $"Helix-Test-Rename-{shortId}");
            IntegrationFixture.AssertSuccess(folderResult);
            using JsonDocument folderDoc = JsonDocument.Parse(folderResult);
            folderId = folderDoc.RootElement.GetProperty("id").GetString()!;

            string b64 = Convert.ToBase64String("rename me"u8.ToArray());
            string uploadResult = await _tools.UploadDriveItem(driveId, "original.txt", parentItemId: folderId, contentBase64: b64);
            IntegrationFixture.AssertSuccess(uploadResult);
            using JsonDocument uploadDoc = JsonDocument.Parse(uploadResult);
            string fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;

            // Rename the file
            string renameResult = await _tools.MoveOrRenameDriveItem(driveId, fileId, newName: "renamed.txt");
            IntegrationFixture.AssertSuccess(renameResult);
            using JsonDocument renameDoc = JsonDocument.Parse(renameResult);
            Assert.Equal("renamed.txt", renameDoc.RootElement.GetProperty("name").GetString());

            // Verify via get-drive-item
            string getResult = await _tools.GetDriveItem(driveId, fileId);
            IntegrationFixture.AssertSuccess(getResult);
            using JsonDocument getDoc = JsonDocument.Parse(getResult);
            Assert.Equal("renamed.txt", getDoc.RootElement.GetProperty("name").GetString());
        }
        finally
        {
            if (folderId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, folderId);
            }
        }
    }

    [Fact]
    public async Task MoveAndRenameItemSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        string shortId = Guid.NewGuid().ToString("N")[..8];
        string? srcId = null;
        string? dstId = null;
        try
        {
            // Create source folder and upload a file
            string srcResult = await _tools.CreateDriveFolder(driveId, $"Helix-Test-Src-{shortId}");
            IntegrationFixture.AssertSuccess(srcResult);
            using JsonDocument srcDoc = JsonDocument.Parse(srcResult);
            srcId = srcDoc.RootElement.GetProperty("id").GetString()!;

            string b64 = Convert.ToBase64String("move and rename me"u8.ToArray());
            string uploadResult = await _tools.UploadDriveItem(driveId, "before.txt", parentItemId: srcId, contentBase64: b64);
            IntegrationFixture.AssertSuccess(uploadResult);
            using JsonDocument uploadDoc = JsonDocument.Parse(uploadResult);
            string fileId = uploadDoc.RootElement.GetProperty("id").GetString()!;

            // Create destination folder
            string dstResult = await _tools.CreateDriveFolder(driveId, $"Helix-Test-Dst-{shortId}");
            IntegrationFixture.AssertSuccess(dstResult);
            using JsonDocument dstDoc = JsonDocument.Parse(dstResult);
            dstId = dstDoc.RootElement.GetProperty("id").GetString()!;

            // Move and rename in a single call
            string result = await _tools.MoveOrRenameDriveItem(driveId, fileId, destinationParentItemId: dstId, newName: "after.txt");
            IntegrationFixture.AssertSuccess(result);
            using JsonDocument resultDoc = JsonDocument.Parse(result);
            Assert.Equal("after.txt", resultDoc.RootElement.GetProperty("name").GetString());

            // Verify file is in destination folder with new name
            string childrenResult = await _tools.ListDriveChildren(driveId, itemId: dstId);
            IntegrationFixture.AssertSuccess(childrenResult);
            using JsonDocument childrenDoc = JsonDocument.Parse(childrenResult);
            JsonElement children = childrenDoc.RootElement.GetProperty("value");
            Assert.Contains(children.EnumerateArray(),
                item => item.GetProperty("name").GetString() == "after.txt");
        }
        finally
        {
            if (srcId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, srcId);
            }

            if (dstId is not null)
            {
                _ = await _tools.DeleteDriveItem(driveId, dstId);
            }
        }
    }

    [Fact]
    public async Task DeleteDriveItemSucceeds()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        // Create a folder, then delete it
        string shortId = Guid.NewGuid().ToString("N")[..8];
        string createResult = await _tools.CreateDriveFolder(driveId, $"Helix-Test-Delete-{shortId}");
        IntegrationFixture.AssertSuccess(createResult);
        using JsonDocument createDoc = JsonDocument.Parse(createResult);
        string folderId = createDoc.RootElement.GetProperty("id").GetString()!;

        // Delete the folder
        string deleteResult = await _tools.DeleteDriveItem(driveId, folderId);
        IntegrationFixture.AssertSuccess(deleteResult);

        // Verify the item is gone — get should return an error
        string getResult = await _tools.GetDriveItem(driveId, folderId);
        using JsonDocument getDoc = JsonDocument.Parse(getResult);
        Assert.True(getDoc.RootElement.TryGetProperty("error", out JsonElement err) && err.GetBoolean());
    }

    [Fact]
    public async Task MoveOrRenameItemMissingParamsReturnsError()
    {
        string? driveId = await GetDriveId();
        if (driveId is null)
        {
            return;
        }

        // Neither destinationParentItemId nor newName provided
        string result = await _tools.MoveOrRenameDriveItem(driveId, "fake-item-id");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out JsonElement err) && err.GetBoolean());
    }

    private async Task<string?> GetDriveId()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
        {
            return null;
        }

        string result = await _tools.ListSiteDrives(fixture.SiteId).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(result);
        return !doc.RootElement.TryGetProperty("value", out JsonElement values) || values.GetArrayLength() == 0
            ? null
            : values[0].GetProperty("id").GetString();
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "README.md")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        return dir ?? throw new InvalidOperationException("Could not find repo root.");
    }
}
