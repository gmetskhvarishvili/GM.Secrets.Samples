# GM.Secrets.Samples

A runnable API demonstrating [GM.Secrets](https://github.com/gmetskhvarishvili/GM.Secrets): the app
reads its secrets through plain `IConfiguration` / `IOptionsMonitor<T>` — **without ever exposing
their values** — and never knows whether a value is a setting, a local secret, or came from a secret
store picked per environment.

> Builds against the sibling source checkout (`../GM.Secrets`) when it exists, so the sample can track
> unreleased changes; otherwise it uses the published packages. Force either with
> `-p:UseLocalGMSecrets=true|false`.

## Configuration layers

```csharp
builder.AddGMSecrets();          // SECRETS_PROVIDER picks the store; nothing else in code

builder.Services.Configure<Sample>(builder.Configuration.GetSection("Sample"));
```

The four store packages are referenced in the `.csproj`; `SECRETS_PROVIDER` loads the one it names
at startup, and the rest are never loaded.

Lowest to highest precedence:

| Layer | Committed? | Holds |
| --- | --- | --- |
| `appsettings.json`, `appsettings.Local.json`, `appsettings.{Environment}.json` | yes | settings only — no secret values |
| `appsecrets.json`, `appsecrets.{Environment}.json` | **no** in a real app (gitignored, never published) — the sample commits a demo `appsecrets.json` | local secret values; loaded in any environment when the file exists — or from wherever the `SECRETS_FILE` environment variable points |
| the store `SECRETS_PROVIDER` selects — Doppler, AWS Secrets Manager, Azure Key Vault, or Vault | — | an environment's secret values, loaded at startup |
| environment variables | — | always win |

Which store to use and how to reach it (`SECRETS_PROVIDER`, `DOPPLER_TOKEN`, `VAULT_ADDR`, ...) come
from **environment variables only** — never appsettings or `appsecrets.json`, which hold the app's
own secrets. The store is fetched at startup and, with `SECRETS_RELOAD_INTERVAL`, again on that
interval; requests never call it.

## Run

The sample ships an example [`GM.Secrets.Sample.API/appsecrets.json`](GM.Secrets.Sample.API/appsecrets.json)
with demo values:

```json
{
  "Sample": { "User": "app_user_111", "Password": "p@ssw0rd" }
}
```

It's committed only so the example works out of the box. In your own app, gitignore
`appsecrets*.json` and put real local values there; other `appsecrets.*.json` files stay ignored
here too.

```bash
dotnet run --project GM.Secrets.Sample.API
```

```bash
curl -s http://localhost:5000/
```

`/` returns `{"user":"app_user_111","passwordConfigured":true}`: the `Sample` section bound as
`IOptionsMonitor<Sample>` — the user, never the password. Without a `Sample` section it
returns 404. `appsecrets.json` is loaded whatever the environment — from `dotnet run`, Rider, or the
built `.exe` — and kept out of `dotnet publish`, so it never reaches a server.

## Deployed: environment variables

A deployed app has no `appsecrets.json`, so the platform (or `doppler run --`, a Kubernetes secret,
ECS task secrets, ...) can inject the same keys as environment variables (`:` becomes `__`):

```bash
ASPNETCORE_ENVIRONMENT=Production Sample__User=app_user Sample__Password=x \
  dotnet run --project GM.Secrets.Sample.API --no-launch-profile
```

## Deployed: a secret store

Set `SECRETS_PROVIDER` and that store's variables in the environment. The store's secrets are loaded
into configuration, so `IConfiguration`, `IOptionsMonitor<T>` and connection strings all see them; set
`SECRETS_RELOAD_INTERVAL=00:05:00` to pick up rotated values without a restart.

| Store | `SECRETS_PROVIDER` | Variables | Secret naming |
| --- | --- | --- | --- |
| Doppler | `Doppler` | `DOPPLER_TOKEN` (service token; implies project + config), or a personal token plus `DOPPLER_PROJECT` — the config then follows the app environment (Development → `dev`, Staging → `stg`, Production → `prd`) unless `DOPPLER_CONFIG` is set | `SAMPLE__USER` — `__` is a section |
| AWS Secrets Manager | `AwsSecretsManager` | `AWS_SECRETS_MANAGER_SECRET_IDS=gm-sample/production` (comma-separated), `AWS_REGION`; credentials from the IAM role, or `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` / `AWS_PROFILE` | one JSON secret: `{"Sample__User": "...", ...}` |
| Azure Key Vault | `AzureKeyVault` | `AZURE_KEYVAULT_URI`, optional `AZURE_KEYVAULT_PREFIX`; credentials from the managed identity, `az login`, or an app registration (`AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`) | `Sample--User` — `--` is a section |
| HashiCorp Vault | `Vault` | `VAULT_ADDR` (https), `VAULT_TOKEN` (or `VAULT_ROLE_ID` + `VAULT_SECRET_ID`), `VAULT_PATHS=gm-sample/production` (comma-separated), `VAULT_MOUNT` (default `secret`), `VAULT_NAMESPACE` | KV v2 data; nested JSON becomes sections |

### Connect to your stores from your machine

The `vault`, `aws`, `azure` and `doppler` launch profiles in `Properties/launchSettings.json` list
every variable their store reads. Settings that only say *where* (an address, a region, a path) can
sit in the profile; **credentials (tokens, keys, client secrets) should not** — see
[Keep credentials out of git](#keep-credentials-out-of-git). The store sits above `appsecrets.json`,
so its values win; a key the store lacks falls back to your local file.

Each store needs the same two secrets, `Sample:User` and `Sample:Password`, named the way it allows.

**HashiCorp Vault** — free to self-host; dev mode runs in memory, nothing to install but the binary:

```powershell
vault server -dev -dev-root-token-id=dev-root-token
```

```powershell
$env:VAULT_ADDR = "http://127.0.0.1:8200"; $env:VAULT_TOKEN = "dev-root-token"; vault kv put secret/gm-secrets-sample/staging Sample__User=vault_user Sample__Password=vault-pass
```

Profile: `VAULT_ADDR=http://127.0.0.1:8200`, `VAULT_TOKEN=dev-root-token`, `VAULT_PATHS=gm-secrets-sample/staging`.
The web UI is at `http://127.0.0.1:8200/ui` (method *Token*). Dev mode forgets everything on restart,
and plain `http` is accepted only for an address on this machine — a real Vault must use `https`.
For a deployed app use AppRole (`VAULT_ROLE_ID` + `VAULT_SECRET_ID`) instead of a token;
`VAULT_NAMESPACE` is only for Vault Enterprise / HCP.

**AWS Secrets Manager** — one secret holding JSON:

1. Secrets Manager → *Store a new secret* → *Other type of secret* → key/value pairs `Sample__User`,
   `Sample__Password`. Note its name and region (the region is in its ARN,
   `arn:aws:secretsmanager:<region>:...`).
2. IAM → Users → *Create user* (no console access) → Permissions → *Create inline policy* (JSON):
   ```json
   { "Version": "2012-10-17", "Statement": [{ "Effect": "Allow", "Action": "secretsmanager:GetSecretValue",
     "Resource": "arn:aws:secretsmanager:<region>:<account-id>:secret:<secret-name>-*" }] }
   ```
3. The user → *Security credentials* → *Create access key* (CLI) → set `AWS_ACCESS_KEY_ID` and
   `AWS_SECRET_ACCESS_KEY` as **user** environment variables (or `aws configure` / a profile in
   `%USERPROFILE%\.aws\credentials` and `AWS_PROFILE`).

Profile: `AWS_REGION`, `AWS_SECRETS_MANAGER_SECRET_IDS=<secret-name>`. Errors name the fix: *Failed to
resolve AWS credentials* → step 3; *not authorized to perform secretsmanager:GetSecretValue* → step 2;
*ResourceNotFound* → name or region.

**Azure Key Vault** — secrets `Sample--User` and `Sample--Password` (`--` is a section; Key Vault
names can't contain `:` or `_`). The vault uses role-based access, so permissions are set under the
vault's **Access control (IAM)**, never *Access policies*:

1. To see and create secrets in the portal yourself: IAM → *Add role assignment* → **Key Vault
   Secrets Officer** → your account. (*Owner* or *Certificates Officer* don't open secrets.)
2. For the app: Entra ID → *App registrations* → *New registration*; on it, *Certificates & secrets* →
   *New client secret* (copy the **Value**). Then the vault's IAM → **Key Vault Secrets User** → the app.
3. Set `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` (both on the registration's Overview) and
   `AZURE_CLIENT_SECRET` as user environment variables — or skip steps 2–3 and use `az login`.

Profile: `AZURE_KEYVAULT_URI` (the vault's Overview → *Vault URI*). Role changes take a few minutes;
*not allowed by RBAC* means the role is missing or not active yet.

**Doppler** — secrets `SAMPLE__USER` and `SAMPLE__PASSWORD` in a config. Either:

- a **service token** for that config (Access → Service Tokens) as `DOPPLER_TOKEN` — it always reads
  the config it was issued for, whatever `ASPNETCORE_ENVIRONMENT` is (one token per environment); or
- your **personal / CLI token** (`doppler configure get token --plain`) as `DOPPLER_TOKEN` plus
  `DOPPLER_PROJECT` — the config then follows `ASPNETCORE_ENVIRONMENT` (Development → `dev`,
  Staging → `stg`, Production → `prd`), unless `DOPPLER_CONFIG` names one.

(Or skip the package: `doppler run -- dotnet run ...` injects the secrets as environment variables,
which configuration already reads.)

Then run the profile (`dotnet run --project GM.Secrets.Sample.API --launch-profile <store>`) and
`curl -s localhost:5000/` returns the store's user. With `SECRETS_RELOAD_INTERVAL` set, change it in the
store and `/` follows without a restart — keep the interval at a minute or more outside testing, as
AWS bills per read and Doppler rate-limits. If the store can't be reached or a credential is missing,
the app stops at startup with the reason (never a secret value).

## Keep credentials out of git

`Properties/launchSettings.json` is committed, so treat it as public:

- **Credentials go in user environment variables**, not the profile:
  `[Environment]::SetEnvironmentVariable("DOPPLER_TOKEN", "...", "User")`, then restart Rider / VS.
  Rider's run configuration (stored under `.idea/`, which is gitignored) works too.
- **Delete the key from the profile** rather than leaving it empty — a key in a launch profile, even
  `""`, overrides the same variable from your environment.
- **Rotate anything that was committed or shared** (pasted in a chat, a screenshot): AWS IAM →
  *Create access key*, then deactivate the old one; Azure → *New client secret*, delete the old one;
  Doppler → revoke the service token.
- `appsecrets*.json` is kept out of `dotnet publish`; gitignore it in your app (this sample commits
  only a demo `appsecrets.json`). `appsettings*.json` is committed — never put a real secret in it.
- Before committing: `git diff --cached` and look for `TOKEN`, `SECRET`, `KEY` values.

## Tests

```bash
dotnet test
```

`tests/GM.Secrets.Sample.Tests` boots the app and checks `/` returns the configured user — and never
the password. It supplies the secrets in memory, so it doesn't need a local `appsecrets.json`.
