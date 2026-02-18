using System.Text.Json;
using Helix.Tools.SharePoint;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class SharePointListToolsTests(IntegrationFixture fixture)
{
    private readonly SharePointListTools _tools = new(fixture.GraphClient);
    private readonly SharePointSiteTools _siteTools = new(fixture.GraphClient);

    [Fact]
    public async Task ListListItemsReturnsItems()
    {
        var listId = await GetAnyListId();
        if (listId is null) return;

        var result = await _tools.ListListItems(fixture.SiteId, listId, top: 5);

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task CreateGetUpdateDeleteListItemFullCycle()
    {
        var listId = await GetGenericListId();
        if (listId is null) return; // No generic list available — skip

        string? itemId = null;
        try
        {
            // Create
            var createResult = await _tools.CreateListItem(fixture.SiteId, listId, "{\"Title\": \"Helix Test Item\"}");
            IntegrationFixture.AssertSuccess(createResult);
            using var createDoc = JsonDocument.Parse(createResult);
            itemId = createDoc.RootElement.GetProperty("id").GetString()!;

            // Get
            var getResult = await _tools.GetListItem(fixture.SiteId, listId, itemId);
            IntegrationFixture.AssertSuccess(getResult);
            using var getDoc = JsonDocument.Parse(getResult);
            Assert.True(getDoc.RootElement.TryGetProperty("id", out _));

            // Update
            var updateResult = await _tools.UpdateListItem(fixture.SiteId, listId, itemId, "{\"Title\": \"Helix Test - Updated\"}");
            IntegrationFixture.AssertSuccess(updateResult);
            using var updateDoc = JsonDocument.Parse(updateResult);

            // Delete
            var deleteResult = await _tools.DeleteListItem(fixture.SiteId, listId, itemId);
            IntegrationFixture.AssertSuccessNoData(deleteResult);
            itemId = null; // Prevent double-delete in finally
        }
        finally
        {
            if (itemId is not null)
                await _tools.DeleteListItem(fixture.SiteId, listId, itemId);
        }
    }

    private async Task<string?> GetAnyListId()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
            return null;

        var result = await _siteTools.ListSiteLists(fixture.SiteId).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(result);
        if (!doc.RootElement.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
            return null;

        return values[0].GetProperty("id").GetString();
    }

    private async Task<string?> GetGenericListId()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
            return null;

        var result = await _siteTools.ListSiteLists(fixture.SiteId).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(result);
        if (!doc.RootElement.TryGetProperty("value", out var values))
            return null;

        foreach (var list in values.EnumerateArray())
        {
            if (list.TryGetProperty("list", out var listProp) &&
                listProp.TryGetProperty("template", out var template) &&
                template.GetString() == "genericList")
            {
                return list.GetProperty("id").GetString();
            }
        }

        return null;
    }
}
