using GM.Secrets;
using GM.Secrets.Sample.API;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// The app's own settings files. appsettings.Local.json is an optional per-developer override.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

builder.Configuration.AddEnvironmentVariables();

// Layers the secrets in between, lowest to highest:
//   the files above → appsecrets.json (or SECRETS_FILE) → the store SECRETS_PROVIDER names → environment variables
// The store and its credentials come from environment variables only (see Properties/launchSettings.json);
// with SECRETS_PROVIDER unset, e.g. locally, only appsecrets.json is used. Fails at startup if the
// selected store is misconfigured or unreachable.
builder.AddGMSecrets();

// A secret is read like any other setting — this code doesn't know which layer it came from.
builder.Services.Configure<Sample>(builder.Configuration.GetSection("Sample"));

var app = builder.Build();

// IOptionsMonitor follows reloads (file edits, SECRETS_RELOAD_INTERVAL), so a rotated value shows up
// without a restart. Only the user is returned; the password never leaves the app.
app.MapGet("/", (IOptionsMonitor<Sample> options) =>
{
    var creds = options.CurrentValue;
    return string.IsNullOrEmpty(creds.User)
        ? Results.NotFound(new { error = "Sample not configured" })
        : Results.Ok(new { creds.User, passwordConfigured = !string.IsNullOrEmpty(creds.Password) });
});

await app.RunAsync();

// Exposed so the test project can spin the app up with WebApplicationFactory.
public partial class Program
{
    /// <summary>Prevents direct instantiation; exists only so <c>WebApplicationFactory&lt;Program&gt;</c> can reference the entry point's type.</summary>
    protected Program()
    {
    }
}
