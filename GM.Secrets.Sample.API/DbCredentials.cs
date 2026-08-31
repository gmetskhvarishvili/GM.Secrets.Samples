namespace GM.Secrets.Sample.API;

/// <summary>The shape of the demo <c>DbCredentials</c> JSON secret, deserialized via <c>GetSecretAsync&lt;T&gt;</c>.</summary>
public sealed record DbCredentials(string User, string Password);
