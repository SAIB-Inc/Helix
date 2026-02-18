using System.Text.Json;
using Helix.Tools.Users;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class UserToolsTests(IntegrationFixture fixture)
{
    private readonly UserTools _tools = new(fixture.GraphClient);

    [Fact]
    public async Task GetCurrentUserReturnsUserProfile()
    {
        var result = await _tools.GetCurrentUser();

        IntegrationFixture.AssertSuccess(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("displayName", out _));
        Assert.True(doc.RootElement.TryGetProperty("mail", out _) ||
                    doc.RootElement.TryGetProperty("userPrincipalName", out _));
    }
}
