using System.Text.Json;
using Helix.Tools.SharePoint;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class SharePointSiteToolsTests(IntegrationFixture fixture)
{
    private readonly SharePointSiteTools _tools = new(fixture.GraphClient);

    [Fact]
    public async Task SearchSitesReturnsSites()
    {
        var result = await _tools.SearchSites("saib");

        var values = IntegrationFixture.AssertHasValues(result);
        Assert.True(values.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetSiteReturnsSiteDetails()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
            return; // Skip if no test site configured

        var result = await _tools.GetSite(fixture.SiteId);

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("displayName", out _));
    }

    [Fact]
    public async Task ListSiteListsReturnsLists()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
            return;

        var result = await _tools.ListSiteLists(fixture.SiteId);

        var values = IntegrationFixture.AssertHasValues(result);
        Assert.True(values.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetSiteListReturnsListWithColumns()
    {
        if (string.IsNullOrEmpty(fixture.SiteId))
            return;

        // Get a list ID first
        var listsResult = await _tools.ListSiteLists(fixture.SiteId);
        using var listsDoc = JsonDocument.Parse(listsResult);
        var lists = listsDoc.RootElement.GetProperty("value");
        if (lists.GetArrayLength() == 0)
            return;

        var listId = lists[0].GetProperty("id").GetString()!;

        var result = await _tools.GetSiteList(fixture.SiteId, listId);

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("displayName", out _));
    }
}
