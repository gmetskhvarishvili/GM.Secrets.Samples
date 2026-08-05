# GM.Secrets.Samples

A runnable API demonstrating [GM.Secrets](https://github.com/gmetskhvarishvili/GM.Secrets): consuming
secrets through `ISecretsService` — **without ever exposing their values** — with a config-selected
provider and TTL caching.

> References the sibling source repo by **project path**. Swap the `ProjectReference`s in
> `GM.Secrets.Sample.API.csproj` for `PackageReference`s once published.

## Run

```bash
dotnet run --project GM.Secrets.Sample.API
```

It defaults to the **Configuration** provider, reading demo secrets from the `SecretValues` section of
[`appsettings.json`](GM.Secrets.Sample.API/appsettings.json) — so it runs with zero setup. (Real apps
keep secrets in a store, not in committed appsettings.)

## Endpoints

| Endpoint | Shows |
| --- | --- |
| `GET /` | active provider + whether `ApiKey` is configured (**presence, not the value**) |
| `GET /secure` | uses the `ApiKey` secret to authorize a request via the `X-Api-Key` header — the secret is compared, never returned |
| `GET /db-info` | a **typed** JSON secret (`GetSecretAsync<DbCredentials>`); returns the user, not the password |

```bash
curl -s http://localhost:5000/                                  # {"provider":"Configuration","apiKeyConfigured":true}
curl -s -H "X-Api-Key: demo-super-secret-key" localhost:5000/secure   # {"access":"granted"}
curl -s -o /dev/null -w "%{http_code}\n" localhost:5000/secure        # 401 (no/!wrong key)
curl -s http://localhost:5000/db-info                           # {"user":"app_user","passwordConfigured":true}
```

## Switch provider to environment variables

```bash
Secrets__Provider=Environment GMSAMPLE_API_KEY=from-env dotnet run --project GM.Secrets.Sample.API
# /  → {"provider":"Environment","apiKeyConfigured":true}
```

(The `Environment` provider normalizes `ApiKey` → `API_KEY` and applies the `GMSAMPLE_` prefix.)

## Tests

```bash
dotnet test
```

`tests/GM.Secrets.Sample.Tests` boots the app and asserts the secret is usable (auth granted/denied)
and typed access works — while checking the raw secret values never appear in any response body.
