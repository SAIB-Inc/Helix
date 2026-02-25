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
        string? listId = await GetAnyListId();
        if (listId is null)
        {
            return;
        }

        string result = await _tools.ListListItems(fixture.SiteId, listId, top: 5);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task CreateGetUpdateDeleteListItemFullCycle()
    {
        string? listId = await GetGenericListId();
        if (listId is null)
        {
            return; // No generic list available — skip
        }

        string? itemId = null;
        try
        {
            // Create
            string createResult = await _tools.CreateListItem(fixture.SiteId, listId, "{\"Title\": \"Helix Test Item\"}");
            IntegrationFixture.AssertSuccess(createResult);
            using JsonDocument createDoc = JsonDocument.Parse(createResult);
            itemId = createDoc.RootElement.GetProperty("id").GetString()!;

            // Get
            string getResult = await _tools.GetListItem(fixture.SiteId, listId, itemId);
            IntegrationFixture.AssertSuccess(getResult);
            using JsonDocument getDoc = JsonDocument.Parse(getResult);
            Assert.True(getDoc.RootElement.TryGetProperty("id", out _));

            // Update
            string updateResult = await _tools.UpdateListItem(fixture.SiteId, listId, itemId, "{\"Title\": \"Helix Test - Updated\"}");
            IntegrationFixture.AssertSuccess(updateResult);
            using JsonDocument updateDoc = JsonDocument.Parse(updateResult);

            // Delete
            string deleteResult = await _tools.DeleteListItem(fixture.SiteId, listId, itemId);
            IntegrationFixture.AssertSuccessNoData(deleteResult);
            itemId = null; // Prevent double-delete in finally
        }
        finally
        {
            if (itemId is not null)
            {
                _ = await _tools.DeleteListItem(fixture.SiteId, listId, itemId);
            }
        }
    }

    private async Task<string?> GetAnyListId()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
        {
            return null;
        }

        string result = await _siteTools.ListSiteLists(fixture.SiteId).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(result);
        return !doc.RootElement.TryGetProperty("value", out JsonElement values) || values.GetArrayLength() == 0
            ? null
            : values[0].GetProperty("id").GetString();
    }

    private async Task<string?> GetGenericListId()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
        {
            return null;
        }

        string result = await _siteTools.ListSiteLists(fixture.SiteId).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(result);
        if (!doc.RootElement.TryGetProperty("value", out JsonElement values))
        {
            return null;
        }

        foreach (JsonElement list in values.EnumerateArray())
        {
            if (list.TryGetProperty("list", out JsonElement listProp) &&
                listProp.TryGetProperty("template", out JsonElement template) &&
                template.GetString() == "genericList")
            {
                return list.GetProperty("id").GetString();
            }
        }

        return null;
    }
}
