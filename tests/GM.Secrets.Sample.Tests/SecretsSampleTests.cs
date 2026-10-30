using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GM.Secrets.Sample.Tests;

// Boots the sample app end to end. appsecrets.json is gitignored, so the factory supplies the demo
// secrets itself, as configuration — the app can't tell where they came from.
public sealed class SampleAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Development").ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sample:User"] = "app_user",
            ["Sample:Password"] = "p@ssw0rd",
        }));
}

public class SecretsSampleTests(SampleAppFactory factory) : IClassFixture<SampleAppFactory>
{
    [Fact]
    public async Task Root_ReturnsTheConfiguredUser_ButNotThePassword()
    {
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/");
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("app_user", doc.RootElement.GetProperty("user").GetString());
        Assert.True(doc.RootElement.GetProperty("passwordConfigured").GetBoolean());
        Assert.DoesNotContain("p@ssw0rd", body); // the password never leaves the app
    }

    [Fact]
    public async Task WithoutAnySecrets_StartsAndReportsNotConfigured()
    {
        // An empty content root: no appsecrets.json (whatever a developer has locally), nothing else set.
        var contentRoot = Directory.CreateTempSubdirectory("gmsample-").FullName;
        try
        {
            using var bare = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production").UseContentRoot(contentRoot));

            var response = await bare.CreateClient().GetAsync(new Uri("/", UriKind.Relative));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}
