# Threat model

What this server defends against and, more importantly, what it does not.

## Assumptions

- The server is the source of truth for licence validity. Software clients ask, the server answers, and the answer is signed.
- An attacker can fully control the desktop client process: attach a debugger, dump memory, patch instructions, intercept syscalls. Any check that runs only on the client is bypassable.
- An attacker can observe network traffic between the client and the server, but cannot break TLS within the response window.
- An attacker may attempt offline attacks against a stolen database dump: the pepper, signing keys, and refresh-token plaintexts must not live in the database.
- An admin's password is the most attractive target inside the dashboard side. If it leaks, every licence and product is at risk.

## What we defend against

**Forged licence-verify responses.** Each response is a compact-JWS signed with an ES256 key the server controls. Clients embed the public key at build time. Forgery requires either signing-key compromise or shipping a patched client (which is out of scope  - see "what we do not defend against").

**Replay of a captured licence-verify response.** Each response carries a 60-second `exp` and the client-supplied `nonce`. A replay outside the window is rejected; a replay within the window only succeeds if the client sent the exact same nonce. Per-call random nonces close that gap entirely.

**Database-only compromise.** A pure DB dump leaks: hashed passwords (Argon2id, expensive to brute-force), HMAC'd licence keys + HWIDs (offline brute-force requires the pepper), and SHA-256'd refresh tokens (32 bytes of randomness, not feasibly brute-forceable). It does not leak: the signing keys, the pepper, the refresh-token plaintexts, or any licence key plaintext.

**Stolen access JWT.** Access tokens last 15 minutes. The blast radius is bounded by that TTL plus the operator's reaction time. The user can immediately call `DELETE /sessions/all` to invalidate every refresh token; existing access tokens still work until expiry but cannot be refreshed.

**Stolen refresh token.** Refresh tokens rotate on every use. Reusing a revoked-then-replaced refresh token is treated as compromise: the entire refresh-token chain for that user is revoked. An attacker who steals a refresh token must use it before the legitimate user's next refresh, and even then the legitimate user's next refresh attempt will detect the reuse and lock the account out.

**Account suspension propagation.** Suspending a user transactionally revokes every live refresh token for them. The kicked user is cut off from the dashboard within at most one access-token TTL (15 minutes).

**Rate-limit-driven brute force.** Login is bucketed per (IP, email-lowercase) at 10/min. `/licences/verify` is bucketed per licence key at 60/min. An attacker probing licence keys against a single IP is constrained to those windows; spreading across many IPs raises the cost without breaking the system.

**HWID lift to a different machine.** The first successful verify pins the HWID's HMAC. Subsequent verifies must present the same HWID. An attacker who lifts the licence key to a fresh machine fails the HWID check and is denied with the same vague error as any other failure mode. An admin can clear the pin if the legitimate user changes hardware.

**IP allowlist bypass via spoofed `X-Forwarded-For`.** ASP.NET Core's `ForwardedHeadersOptions` is configured to trust loopback only by default. Production must lock `KnownProxies` / `KnownNetworks` to the real reverse proxy; otherwise an attacker can connect directly to Kestrel and spoof the header.

**Information disclosure via verify failures.** Every `/licences/verify` failure returns the same `400 invalid_licence` body. An attacker probing for "is this licence key real" or "is this product mismatched" gets no signal. The audit log records the real reason for admin review.

**Algorithm-confusion attacks on session JWTs.** The verifier pins `alg` to `ES256` and validates `iss` / `aud`. The classic `none` and `HS256-with-RSA-public-key-as-secret` footguns are neutralized.

**Key + pepper rotation as a kill switch.** All three secrets are loaded as sets with a designated active entry. After a leak the operator generates the new version, flips active, restarts, waits the retention window, and removes the old entry. Removing the old pepper from config makes every licence hashed under it unverifiable  - the intended kill switch for "we believe the pepper leaked."

## What we do not defend against

**A patched desktop client.** If an attacker controls the binary, they can patch out the signature check, hardcode an "approved" response, or skip verification entirely. The signed-response model raises the bar (patching a binary is harder than flipping a boolean), but client-side enforcement always loses to root access on the client. Mitigations like obfuscation, anti-tamper, or remote attestation are out of scope.

**A compromised admin password.** Once an admin authenticates, every licence and product is theirs to revoke, suspend, or re-issue. There is no MFA today (Chunk J, deferred). Password reset is also deferred.

**Server-side compromise.** If an attacker gets shell on the server, the signing keys, pepper, and database are all available to them. Defence in depth (file permissions, secret-manager integration) is on the roadmap (Chunk N), but the current model assumes the server itself is trusted.

**Cross-instance correlation in distributed deploys.** Rate limiting is per-instance in-memory. A multi-instance deployment without a shared backing store (Redis, etc.) lets an attacker spread a brute-force attempt across instances and effectively multiply the per-bucket budget. Distributed rate-limit backing is a Chunk N item.

**Long-lived telemetry of denied verifies.** The `licence_verification_attempts` table grows unbounded today. Audit retention and pruning land in Chunk M. Until then, very high verify volumes will gradually grow the table.

**Offline brute force of an HMAC if both pepper and DB are leaked together.** The pepper exists precisely so a database-only leak doesn't enable this. If both leak  - e.g. via a server-side compromise  - an attacker can compute HMACs for candidate licence keys at the cost of one HMAC per guess. With ~125-bit licence-key entropy this is still infeasible, but anything reduces the safety margin.

**Client SDK compromise after a key rotation.** Once a client SDK ships with the licence-verify public key embedded, that SDK trusts only that key. Rotating the licence-verify signing key means EOL'ing or updating every shipped SDK that doesn't pull the JWKS at runtime. Pulling at runtime is unsafe (an attacker who controls the client machine can redirect the JWKS fetch); embedding is the right design but constrains rotation.

**Email-address enumeration via login.** Login uses the same response shape for missing user and wrong password. Argon2 verify runs on the missing-user path so timing matches. A patient attacker with a list of emails will not learn which are registered from login responses alone  - but the registration-by-admin model means new emails arrive through `POST /users`, which is admin-only and therefore not an enumeration surface.

## Out of scope

- DDoS at the network layer (handled by infrastructure / CDN, not the application).
- Side-channel attacks against the server hardware (timing, power, EM).
- Insider threats with database access plus the secret store.
- Supply-chain attacks against the .NET runtime or NuGet packages.
- Anything physical: stolen laptops, shoulder surfing, social engineering.
