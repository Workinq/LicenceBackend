# LicenceBackend

A .NET 9 licence server. Software clients call `POST /licences/verify` with a licence key plus a random nonce and get back a compact-JWS envelope (`{ signedPayload }`) that a client verifies with an embedded ES256 public key. The server is authoritative - verification happens once at startup and the response is signed so a forged one requires patching out signature verification, not flipping a boolean. Licences can additionally be bound to the hardware they're first used on (trust-on-first-use HWID pinning) and to an admin-approved CIDR allowlist (server-observed IP, not client-reported), and every verify attempt against a known licence is persisted as an audit row. Human operators sign in with email + password via `POST /sessions`, get back a short-lived access JWT plus a long-lived refresh token, and use the access token to manage users, products, and licences through the admin endpoints.

This repository is being built in incremental chunks. See `.claude/plans/good-morning-today-i-melodic-thunder.md` for the active plan and roadmap.

## Solution layout

| Project | Purpose |
| --- | --- |
| `LicenceBackend.Api` | HTTP layer: Controllers, `Program.cs`, Serilog, OpenAPI + Scalar, JWT Bearer auth. |
| `LicenceBackend.Core` | Domain model + interfaces, zero framework deps. |
| `LicenceBackend.Infrastructure` | Dapper + Npgsql repositories, ECDSA key loading, session JWT issuance, licence-verify JWT signing, Argon2id password hasher, refresh-token generator/hasher, DI wiring. |
| `LicenceBackend.Tests` | xUnit integration tests via `WebApplicationFactory<Program>`. |
| `tools/LicenceBackend.DevTools` | CLI for local-only tasks: generate the session key and pepper, run schema migrations, bootstrap first admin, seed a dev licence. |

## Prerequisites

- .NET 9 SDK (`dotnet --version` -> 9.0.x).
- A running PostgreSQL 14+ instance.

## One-time setup

1. **Create a database** in your Postgres instance. Any name works.

2. **Generate the signing keys and HMAC pepper** into `./secrets/` (gitignored):

   ```sh
   dotnet run --project tools/LicenceBackend.DevTools -- init-secrets
   ```

   Writes versioned **v1** files (everything is rotatable - see the rotation runbook below):
   - `session-signing-key-v1.pem` - ECDSA P-256 for the access JWTs issued by `POST /sessions`.
   - `licence-verify-signing-key-v1.pem` - ECDSA P-256 for signing `POST /licences/verify` responses.
   - `licence-key-pepper-v1.txt` - base64 of 32 random bytes, used to HMAC licence keys + HWIDs at rest.

   `init-secrets` refuses to overwrite existing files; pass `--force` to regenerate from scratch (clobbers everything). To add a *new* version alongside the old one, use the `rotate-*` commands instead.

3. **Store your local Postgres connection string in `dotnet user-secrets`** (so it never touches the repo, even if your gitignore is wrong):

   ```sh
   dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=licencebackend;Username=postgres;Password=postgres" \
     -p LicenceBackend.Api
   ```

   The `<UserSecretsId>` is already in `LicenceBackend.Api.csproj`, so this writes to `~/.microsoft/usersecrets/<id>/secrets.json` (or `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` on Windows). The API auto-loads it in Development; the DevTools resolves it in this order:

   1. `LICENCEBACKEND_POSTGRES` environment variable.
   2. `ConnectionStrings:Postgres` in `dotnet user-secrets` for `LicenceBackend.Api`.
   3. `ConnectionStrings:Postgres` in `LicenceBackend.Api/appsettings.Development.json` (legacy fallback).
   4. `ConnectionStrings:Postgres` in `LicenceBackend.Api/appsettings.json`.

4. **Apply the schema migrations** (creates the database if it doesn't exist, then applies any pending migrations from `migrations/`):

   ```sh
   dotnet run --project tools/LicenceBackend.DevTools -- migrate
   ```

   Re-running is a no-op once everything is applied. Use `migrate-status` to see applied vs pending without running anything.

5. **Create the first admin user:**

   ```sh
   dotnet run --project tools/LicenceBackend.DevTools -- create-admin --email you@example.com
   ```

   A 24-character password is generated and printed once. Copy it into your password manager. Alternatives:
   - `--password <your-password>` - supply your own (min 12 chars).
   - `--force` - upsert an existing email to admin with a new password.

   Further admins can be created via `POST /users` by any existing admin; the CLI is only for the chicken-and-egg bootstrap.

6. **Configure the Api.** The repo ships with `LicenceBackend.Api/appsettings.Development.json` containing only non-secret values (Serilog levels, Session issuer, signing-key + pepper *paths* - the actual key files live under `./secrets/`, gitignored). The dev connection string itself lives in `dotnet user-secrets` from step 3, not in this file.

7. **(Optional) Seed a quick dev licence** via the CLI. The admin API is the real path, but `seed-dev` is handy for a one-off:

   ```sh
   dotnet run --project tools/LicenceBackend.DevTools -- seed-dev
   ```

   Creates a test user + product + licence linked together. Prints the user email, generated password, and raw licence key once - copy everything you need.

8. **(Recommended) Enable the local secret-scan pre-commit hook.** A `.githooks/pre-commit` script in the repo runs [`gitleaks`](https://github.com/gitleaks/gitleaks) against your staged changes before each commit, so an accidental `git add` of a PEM or password is caught before it lands in history.

   ```sh
   # one-time per clone
   git config core.hooksPath .githooks

   # one-time per machine
   brew install gitleaks    # macOS
   # or: scoop install gitleaks  / apt install gitleaks  / see https://github.com/gitleaks/gitleaks/releases
   ```

   The hook gracefully skips if `gitleaks` isn't installed, so it never breaks a commit silently. CI runs the same scanner on every push and PR via `.github/workflows/ci.yml`, so even an unhooked commit is caught at push time. Project-specific allowlists for known false positives (the local Postgres dev creds, the `LIC-...` example licence key in this README) live in `.gitleaks.toml`.

## Running the API

```sh
dotnet run --project LicenceBackend.Api
```

Kestrel listens on `https://localhost:5001` by default (see `LicenceBackend.Api/Properties/launchSettings.json`).

- **Scalar UI:** `https://localhost:5001/scalar/v1` (Development only) - interactive docs with "Try it out".
- **OpenAPI spec:** `https://localhost:5001/openapi/v1.json`

### Endpoints

| Method + Path | Auth | Purpose |
| --- | --- | --- |
| `POST /sessions` | public | Log in with email + password; returns `{ accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, user }`. Access JWT lasts 15 min, refresh 30 days. Refuses suspended accounts with `401 account_suspended`. |
| `POST /sessions/refresh` | public | Exchange a refresh token for a new access + refresh pair. Body is the raw token as a JSON string (e.g. `"eyJ..."`). Refresh tokens rotate on every use; reusing a revoked-then-replaced refresh revokes every live refresh for that user. |
| `DELETE /sessions` | any user | Revokes the refresh token tied to the current access JWT (via `sid` claim). |
| `DELETE /sessions/all` | any user | Revokes every non-revoked refresh token for the current user. |
| `GET /me` | any user | Current user's profile. |
| `GET /me/licences` | any user | Paginated list of licences owned by the current user (never returns the raw key). Optional `?status=` filter. |
| `POST /users` | admin | Create a user. |
| `GET /users` | admin | Paginated list of users. |
| `GET /users/{id}` | admin | Single user. |
| `PATCH /users/{id}/status` | admin | Suspend or reactivate a user. Body: `{ "status": "active" \| "suspended", "reason": "..." }`. Admins cannot suspend themselves. Suspension transactionally revokes every live refresh token for that user. |
| `GET /users/{id}/status-history` | admin | Paginated audit trail of status transitions with `changedBy`, `changedByEmail`, `reason`. |
| `POST /products` | admin | Create a product. |
| `GET /products` | admin | Paginated list of products. |
| `GET /products/{id}` | admin | Single product. |
| `POST /licences` | admin | Create a licence owned by a user. Body requires `productId` + **exactly one of** `userId` or `email`. Response **includes the raw licence key exactly once**. |
| `GET /licences` | admin | Paginated list of licences, optional `productId`, `userId`, `status` filters. Responses include owner info (`userId`, `userEmail`). |
| `GET /licences/{id}` | admin | Single licence with owner info (never returns the raw key). |
| `PATCH /licences/{id}/status` | admin | Change a licence's status. Body: `{ "status": "active" \| "suspended" \| "revoked", "reason": "..." }`. Free transitions between all three. |
| `GET /licences/{id}/status-history` | admin | Paginated audit trail of licence status transitions. |
| `PUT /licences/{id}/hwid` | admin | Clear a pinned HWID (so the user can re-pin from a new machine). Body: `{ "hwid": null, "reason": "..." }`. Only clearing is supported - HWIDs are pinned by the first successful verify, not set by an admin. Non-null `hwid` returns `400`. |
| `PUT /licences/{id}/ip-allowlist` | admin | Set or clear the IP allowlist. Body: `{ "cidrs": ["203.0.113.7/32", "::1/128"] \| null, "reason": "..." }`. `null` means unrestricted. An empty array is rejected (use `null`). Each CIDR is validated with `System.Net.IPNetwork.TryParse`. |
| `GET /licences/{id}/binding-history` | admin | Paginated audit trail of HWID pins/clears and IP-allowlist changes. Each row has `bindingType`, `previousValue`, `newValue`, `changeSource` (`admin` \| `first_use`), `changedByUserId`, `changedAt`, `reason`. |
| `GET /licences/{id}/verification-attempts` | admin | Paginated per-licence verify-attempt log. Optional `?outcome=approved\|denied`. Each row has `hwidFingerprint` (base64 HMAC), `sourceIp`, `outcome`, `denialReason`. |
| `GET /me/licences/{id}/verification-attempts` | owner | Paginated approved verifications for a licence you own. Denied attempts are not returned to owners - admins see them via the admin endpoint. |
| `GET /verification-attempts?outcome=denied` | admin | Cross-licence feed of verify attempts (omit `outcome` for all). Useful for surfacing unauthorised-request dashboards without drilling into individual licences. |
| `POST /licences/verify` | public | Software clients verify a licence in one shot. Body: `{ licenceKey, productId, clientNonce, hwid? }` (`clientNonce` is a required 16-128 char string, generate 32 random bytes base64url-encoded). Success returns `{ signedPayload }` - a compact-JWS (`alg=ES256`, `typ=licence-verify+jwt`) whose claims are `iat`, `exp` (iat + 60 s), `nonce` (echoed), `licenceId`, `productId`, `productSlug`, `status`, `licenceExpiresAt?`, `notes?`. Any failure returns a deliberately vague `400 invalid_licence`, unsigned. HWID is pinned on first successful verify (trust-on-first-use); subsequent verifies must present the same HWID. The source IP used for allowlist enforcement is **the server-observed connection IP** (respecting `X-Forwarded-For` behind a trusted reverse proxy), never the request body. |
| `GET /licences/verify/public-key` | public | Returns the **JWKS** (RFC 7517) of every loaded licence-verify key: `{ "keys": [{ "kty":"EC", "crv":"P-256", "x":"...", "y":"...", "kid":"licence-verify-v1", "alg":"ES256", "use":"sig" }, ...] }`. **Fetch once and embed in your client at build time** - fetching at runtime lets an attacker point the client at their own key. The client picks the matching key by JWT-header `kid`. |

Admin endpoints expect `Authorization: Bearer <access-jwt>` where the access JWT comes from `POST /sessions` or `POST /sessions/refresh`.

All of the endpoints above are rate-limited in-process. Login buckets on `(client IP, email-lowercase)`; `/sessions/refresh` and `/licences/verify/public-key` bucket on client IP; `/licences/verify` buckets on the licence key; all authenticated admin + audit + `/me` endpoints bucket on the authenticated user's id. Exceeding the bucket returns `429 rate_limited` with a `Retry-After` header. Defaults live in `appsettings.json` under `RateLimiting`; see the Security posture section for specifics.

### Example: human operator flow

```sh
# Log in
LOGIN=$(curl -s -X POST https://localhost:5001/sessions \
  -H 'Content-Type: application/json' \
  -d '{"email":"you@example.com","password":"<your-password>"}')
ACCESS=$(echo "$LOGIN" | jq -r .accessToken)
REFRESH=$(echo "$LOGIN" | jq -r .refreshToken)

# Create a product
curl -X POST https://localhost:5001/products \
  -H "Authorization: Bearer $ACCESS" \
  -H 'Content-Type: application/json' \
  -d '{"slug":"app-pro","displayName":"App Pro"}'

# When the access token expires (~15 min), exchange the refresh for a new pair
curl -X POST https://localhost:5001/sessions/refresh \
  -H 'Content-Type: application/json' \
  -d "\"$REFRESH\""

# Log out just this session
curl -X DELETE https://localhost:5001/sessions \
  -H "Authorization: Bearer $ACCESS"

# ...or log out everywhere
curl -X DELETE https://localhost:5001/sessions/all \
  -H "Authorization: Bearer $ACCESS"
```

### Example: software client flow

```sh
# Generate a fresh 32-byte nonce per call (base64url-encoded).
NONCE=$(head -c 32 /dev/urandom | base64 | tr '+/' '-_' | tr -d '=')

# Verify once at startup. The server is authoritative and the response is signed.
curl -X POST https://localhost:5001/licences/verify \
  -H 'Content-Type: application/json' \
  -d "{
        \"licenceKey\":\"LIC-ABCDE-FGHJK-MNPQR-STVWX-YZ234\",
        \"productId\":\"<product-id>\",
        \"clientNonce\":\"$NONCE\"
      }"
# -> { "signedPayload": "eyJhbGciOiJFUzI1NiIs..." }
```

The client verifies the `signedPayload` against a **pubkey baked in at build time** (pulled once from `GET /licences/verify/public-key` during the build). Pseudo-code:

```
jws = response.signedPayload
header, payload, signature = split jws on '.'
assert header.alg == "ES256"
assert header.kid == EMBEDDED_PUBKEY_KID
assert verify_es256(signature, header + '.' + payload, EMBEDDED_PUBKEY)
claims = json_decode(base64url_decode(payload))
assert claims.nonce == NONCE                   // the nonce you sent
assert claims.exp > now()                       // 60-second freshness window
assert claims.productId == EXPECTED_PRODUCT_ID
assert claims.status == "active"
```

Do **not** fetch `/licences/verify/public-key` at runtime - an attacker who controls the client machine can redirect that call to their own server. The public key belongs in the compiled binary.

On any licence-verification failure (unknown key, wrong product, suspended/revoked/expired licence, suspended owner, missing/malformed nonce) the response is a deliberately vague `400 invalid_licence`, unsigned - failure modes are indistinguishable by design.

## Tests

```sh
# Point at a DB you don't mind being wiped (each test class drops the schema and re-runs migrations)
export LICENCEBACKEND_TEST_POSTGRES='Host=localhost;Port=5432;Database=licencebackend_test;Username=postgres;Password=postgres'

dotnet test
```

If `LICENCEBACKEND_TEST_POSTGRES` is not set, integration tests skip.

## Schema migrations

The `migrations/` directory at the repo root holds append-only SQL files applied in order by [DbUp](https://dbup.readthedocs.io/). The DevTools `migrate` command runs them; `migrate-status` lists applied vs pending. Integration tests run the same migrator on every test class so the migration path is exercised end-to-end on every run.

- **Filename convention:** `NNN_short_description.sql` - three-digit zero-padded sequence, snake_case description, `.sql` extension. Enforced by a unit test.
- **Forward-only.** No down migrations. To reverse a change, write a new forward migration that undoes it.
- **Adding a migration:** drop a new file with the next number into `migrations/`, run `dotnet run --project tools/LicenceBackend.DevTools -- migrate`, commit the file. That's it.
- **Tracking.** DbUp keeps a `public.__schema_versions` table recording each script that has been applied; that table is the source of truth for "what's the database at right now".

## Security posture

- **Passwords** hashed with Argon2id (t=3, m=64 MiB, p=1), stored as PHC-encoded strings so parameters can be upgraded without migration.
- **Licence keys** stored as HMAC-SHA256 with a server-held pepper; raw keys never hit disk.
- **Session access JWTs** signed with ES256 using a dedicated rotatable ECDSA P-256 key set (`SessionSigning:Keys`). The `kid` header on every issued JWT identifies which key signed it; the verifier loads every configured key and selects by `kid`. Access tokens are short-lived (15 min).
- **Licence-verify responses** signed with ES256 using a *separate* rotatable ECDSA P-256 key set (`LicenceVerifySigning:Keys`). Response JWT lives for 60 seconds and carries the client-provided `nonce` - replay requires either breaking TLS mid-call or patching the client. The full key set is served as a JWKS from `GET /licences/verify/public-key` for build-time embedding; SDKs pick by `kid`.
- **Refresh tokens** are 32-byte random values, base64url-encoded, SHA-256 hashed at rest. Every refresh rotates the token; reuse of a revoked-then-replaced refresh revokes every live refresh for that user (OAuth 2.0 rotation best practice).
- **Suspension** transactionally revokes every live refresh token for the affected user, so a kicked admin is cut off from the dashboard within at most one access-token TTL (15 min).
- **JWT verifier** pins `alg` to `ES256` and validates `iss`/`aud` - neutralises classic JWT `none`/alg-confusion footguns.
- **Key + pepper rotation** is built in. All three secrets - session-signing key, licence-verify signing key, and HMAC pepper - are loaded as *sets* with one designated active entry; old entries stay in config to verify in-flight tokens / look up pre-rotation licences. Rotation is a four-step manual process (generate -> add to config -> flip active -> restart -> wait -> remove old -> restart); see "Rotation runbook" below. Per-licence `key_hmac_pepper_version` and `hwid_hmac_pepper_version` columns track which pepper hashed each row, so verify can compute candidate HMACs under every active pepper in a single query and HWID compare uses the row's stored version.
- **`/licences/verify` errors** are intentionally vague. Session/admin errors are descriptive because the caller is authenticated.
- **Case-insensitive email lookup** via a dedicated `email_lower` column - no "Alice@ vs alice@" duplicate accounts.
- **Role-based admin** via `[Authorize(Roles = "admin")]` on the management controllers. Admin is a column on the user, not a shared static key, so actions can eventually be attributed to an individual.
- **HWID pinning** uses trust-on-first-use: the first successful verify with a non-null `hwid` pins the licence to that HWID's HMAC (same pepper as the licence-key HMAC). Subsequent verifies without a matching HWID fail vaguely. An admin can clear the pin via `PUT /licences/{id}/hwid`; only clearing is supported so the pin always reflects a real first-use signal.
- **IP allowlist** is a JSONB array of CIDRs (IPv4 + IPv6) parsed with `System.Net.IPNetwork`. Enforcement uses the server-observed connection IP. `X-Forwarded-For` is honoured via ASP.NET Core's `ForwardedHeadersOptions`; dev defaults trust loopback only. **In production, lock `KnownProxies`/`KnownNetworks` down to the real reverse proxy** - otherwise clients can spoof `X-Forwarded-For` directly to Kestrel and bypass the allowlist.
- **Verify-attempt audit log** records every hit on `POST /licences/verify` that matches a known licence - approved or denied with a specific reason (`product_mismatch`, `licence_not_usable`, `owner_suspended`, `ip_not_allowlisted`, `hwid_missing`, `hwid_mismatch`). Unknown-key attempts are not recorded to bound table growth. Owners see only their licence's approved attempts; denials stay admin-only.
- **Rate limiting** on all sensitive endpoints via `Microsoft.AspNetCore.RateLimiting` sliding-window partitioned limiters (in-memory, per-instance). Defaults: `POST /sessions` 10/min per (IP, email-lowercase); `POST /sessions/refresh` 30/min per IP; `POST /licences/verify` 60/min per licence key; `GET /licences/verify/public-key` 20/min per IP; all admin + audit endpoints 300/min per authenticated user id. Exceeded buckets return `429 rate_limited` with a `Retry-After` header and a Problem Details body. Tune via the `RateLimiting` section in `appsettings.json`; set `RateLimiting:Enabled=false` to disable entirely. Single-instance today - distributed backing (e.g. Redis) lands with deployment work. Bucket partitioning by client IP reuses the forwarded-headers trust config, so the same `KnownProxies`/`KnownNetworks` deployment caveat applies.
- **Secrets-on-disk** is the deliberate storage model for now: PEMs and pepper files in `./secrets/`, mounted from the host (or in production, mounted from a secret manager into the container at the same paths). Considered + rejected: storing the pepper in the application database (defeats its threat model - the pepper exists *because* a DB compromise on its own should not yield licence-key brute-forceability). Secret-manager / KMS integration lands with deployment work; the `*KeySet` and `HmacPepperSet` abstractions are storage-agnostic (only the loader changes).

## Production deployment

A template `LicenceBackend.Api/appsettings.Production.json` ships with the repo. It points at `/etc/licencebackend/secrets/` for all three secret files and leaves `ConnectionStrings:Postgres` and `Session:Issuer` empty so they must be supplied at deploy time. The .NET configuration system layers overrides as `appsettings.json` -> `appsettings.Production.json` -> environment variables (`__` separator), so anything in the template can be overridden without editing the file.

Minimum environment for a Production process:

```sh
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Postgres='Host=...;Port=5432;Database=...;Username=...;Password=...'
Session__Issuer=https://your-domain.example
```

**Secrets on disk.** Mount the three files into `/etc/licencebackend/secrets/` (or override the paths in `appsettings.Production.json`). Each file must be readable only by the API process owner - the DevTools `init-secrets` and `rotate-*` commands write Unix mode 600, but mount paths from a secret manager need the same restriction enforced by the orchestrator. Generated by:

```sh
dotnet run --project tools/LicenceBackend.DevTools -- init-secrets
```

Run this once per environment, copy the files into the production secret store (do **not** commit them), and then make them available at the mount path.

**Schema.** Apply migrations against the production DB at deploy time:

```sh
LICENCEBACKEND_POSTGRES='Host=...;Port=5432;Database=licencebackend;Username=...;Password=...' \
  dotnet run --project tools/LicenceBackend.DevTools -- migrate
```

Migrations are forward-only and idempotent - re-running is safe and applies anything new since the last deploy. Use `migrate-status` to dry-run the listing.

**Reverse proxy.** ASP.NET Core's `ForwardedHeadersOptions` is configured to trust loopback only by default. **Lock `KnownProxies` / `KnownNetworks` to your real reverse proxy's IP** before going live; otherwise clients can spoof `X-Forwarded-For` directly to Kestrel and bypass IP allowlist enforcement. The current code path is in `LicenceBackend.Api/Program.cs` and will need a Production-aware override.

**Health check.** `GET /health` is unauthenticated and returns 200 with `{ "status": "ok", "db": "ok" }` after a `SELECT 1` against the configured `NpgsqlDataSource`. Returns 503 if the DB probe fails. Wire it into your load balancer's readiness probe.

**Initial admin.** Create the first admin via the DevTools (no API call required, runs locally against the same DB):

```sh
LICENCEBACKEND_POSTGRES='...' \
  dotnet run --project tools/LicenceBackend.DevTools -- create-admin --email you@example.com
```

The generated password prints once; copy it into a password manager. Subsequent admins are created by an existing admin via `POST /users`.

**Logging.** Serilog writes to stdout. In a container, rely on the host's log collector (Docker / journald / Kubernetes log shipping); no file sink is configured. The `appsettings.json` Serilog section can be overridden via env vars if you need a different sink shape.

**What this does NOT include.** No CI workflow, no distributed rate-limit backing, no secret-manager integration, no Docker or Kubernetes manifests. Those are deferred to later chunks.

## Rotation runbook

All three rotatable secrets follow the same recipe:

1. **Generate** the new version with the matching DevTools command:
   - `dotnet run --project tools/LicenceBackend.DevTools -- rotate-session-key`
   - `dotnet run --project tools/LicenceBackend.DevTools -- rotate-licence-verify-key`
   - `dotnet run --project tools/LicenceBackend.DevTools -- rotate-pepper`
   Each prints the snippet to paste into `appsettings.json` and the new file path. By default the kid/version auto-increments (`session-v2`, `pepper version 2`, etc.); pass `--kid <name>` or `--version <n>` to override.
2. **Add the new entry** to the corresponding `Keys` / `Peppers` array in your config - but *leave* the existing entries in place.
3. **Flip the active pointer** (`SessionSigning:ActiveKid`, `LicenceVerifySigning:ActiveKid`, or `Licence:ActivePepperVersion`) to the new id. Restart the API. New tokens / new licences will now be signed/hashed under the new entry; in-flight tokens and pre-rotation licences continue to verify against the retained old entry.
4. **Wait for the retention window** to elapse, then remove the old entry from config and restart again.

Recommended retention windows:
- **Session-signing key:** at least `Session:TtlSeconds + ClockSkew` (≈16 min today). After that, every access JWT under the old key has expired.
- **Licence-verify signing key:** until every shipped client SDK that embedded only the old key has been EOL'd. Effectively manual - set the policy when you cut your first SDK build.
- **HMAC pepper:** until every licence row referencing the old version has been revoked or re-issued, *or* the suspected-leak window has passed. Removing the pepper from config makes those licences un-verifiable - that's the intended kill-switch after a leak.

The new shape replaces the single-key `PrivateKeyPath`/`Kid`/`KeyHmacPepperPath` config from previous chunks. Local devs upgrading should delete `./secrets/` and re-run `init-secrets` to bootstrap v1 of each.

## API versioning

The API is unversioned today. Routes live at the bare resource (`/sessions`, `/licences/verify`, etc.) and the OpenAPI document carries `version: "1.0.0"` as a semver of the surface contract.

Policy: when the first breaking change to a public endpoint ships, that endpoint moves to `/v2/<resource>` and the bare-route `/v1` shape stays live alongside it for a deprecation window. Additive changes (new fields, new endpoints, new optional query params) are not breaking and ship under the existing path. Client SDKs should pin their target version explicitly once `/v2` ships; today they implicitly target `/v1`.

This commits us to: never breaking the bare-route shape without bumping. Change the response payload? `/v2`. Change validation rules in a way that rejects previously-accepted bodies? `/v2`. Rename an endpoint? `/v2`. Add an optional field? Same path, no bump.

## Threat model

A "what we defend against / what we don't" walkthrough lives at [`docs/THREAT_MODEL.md`](docs/THREAT_MODEL.md). Read it before changing anything in the auth, verify, or rotation paths - the design assumptions are load-bearing.

## What's not here yet

Password reset, email verification, MFA, deployment tooling (including secret-manager / KMS integration), audit-log retention/pruning, distributed (cross-instance) rate-limit backing, binding the signed response to cryptographic material the client software needs to function (entitlement sealing), and client SDKs. See the plan file for the roadmap.
