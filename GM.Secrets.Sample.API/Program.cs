using GM.Caching;
using GM.Secrets;
using GM.Secrets.Configuration;
using GM.Secrets.Environment;

var builder = WebApplication.CreateBuilder(args);

// Optional cache: secret lookups are cached for Secrets:CacheDuration, so rotation happens on expiry
// and remote providers aren't hit on every request.
builder.Services.AddGMCaching();

// Register both providers; Secrets:Provider (appsettings) selects which one is active. This sample
// defaults to "Configuration" (reads from the SecretValues section) so it runs with no setup; set
// Secrets:Provider = "Environment" to read from environment variables instead.
builder.Services.AddGMEnvironmentSecrets(builder.Configuration);
builder.Services.AddGMConfigurationSecrets(builder.Configuration);
builder.Services.AddGMSecrets(builder.Configuration);

var app = builder.Build();

app.MapGet("/", async (ISecretsService secrets, IConfiguration config) => Results.Ok(new
{
    message = "GM.Secrets sample",
    provider = config["Secrets:Provider"],
    // Report presence, never the value.
    apiKeyConfigured = await secrets.GetSecretAsync("ApiKey") is not null,
}));

// Uses a secret WITHOUT exposing it: compares the caller's X-Api-Key header to the stored secret.
app.MapGet("/secure", async (HttpContext ctx, ISecretsService secrets) =>
{
    var expected = await secrets.GetRequiredSecretAsync("ApiKey");
    var provided = ctx.Request.Headers["X-Api-Key"].ToString();
    return string.Equals(provided, expected, StringComparison.Ordinal)
        ? Results.Ok(new { access = "granted" })
        : Results.Unauthorized();
});

// Typed secret: a JSON credential is deserialized; only the non-sensitive part is returned.
app.MapGet("/db-info", async (ISecretsService secrets) =>
{
    var creds = await secrets.GetSecretAsync<DbCredentials>("DbCredentials");
    return creds is null
        ? Results.NotFound(new { error = "DbCredentials secret not configured" })
        : Results.Ok(new { creds.User, passwordConfigured = !string.IsNullOrEmpty(creds.Password) });
});

app.Run();

public sealed record DbCredentials(string User, string Password);

// Exposed so the test project can spin the app up with WebApplicationFactory.
public partial class Program;
