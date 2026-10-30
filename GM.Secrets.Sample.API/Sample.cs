namespace GM.Secrets.Sample.API;

/// <summary>
/// The <c>Sample</c> configuration section, bound as options — from appsettings, appsecrets.json or a
/// secret store, whichever layer has it. In a store the keys are <c>Sample__User</c> / <c>Sample__Password</c>
/// (Key Vault: <c>Sample--User</c>).
/// </summary>
public sealed class Sample
{
    /// <summary>A user name — not sensitive, so the sample returns it.</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>A password — sensitive: never returned or logged.</summary>
    public string Password { get; set; } = string.Empty;
}
