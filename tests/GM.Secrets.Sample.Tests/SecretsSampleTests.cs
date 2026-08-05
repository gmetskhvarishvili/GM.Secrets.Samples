using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GM.Secrets.Sample.Tests;

// Boots the sample app (Configuration provider reading the SecretValues section) end to end.
public class SecretsSampleTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Root_ReportsApiKeyConfigured_WithoutExposingIt()
    {
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/");
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("Configuration", doc.RootElement.GetProperty("provider").GetString());
        Assert.True(doc.RootElement.GetProperty("apiKeyConfigured").GetBoolean());
        Assert.DoesNotContain("demo-super-secret-key", body); // the value is never returned
    }

    [Fact]
    public async Task Secure_GrantsWithCorrectKey_RejectsOtherwise()
    {
        var client = factory.CreateClient();

        var withKey = new HttpRequestMessage(HttpMethod.Get, "/secure");
        withKey.Headers.Add("X-Api-Key", "demo-super-secret-key");
        var ok = await client.SendAsync(withKey);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var wrong = new HttpRequestMessage(HttpMethod.Get, "/secure");
        wrong.Headers.Add("X-Api-Key", "nope");
        var denied = await client.SendAsync(wrong);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task DbInfo_ReturnsTypedSecret_User_ButNotPassword()
    {
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/db-info");
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("app_user", doc.RootElement.GetProperty("user").GetString());
        Assert.True(doc.RootElement.GetProperty("passwordConfigured").GetBoolean());
        Assert.DoesNotContain("p@ssw0rd", body); // password value never leaves the app
    }
}
