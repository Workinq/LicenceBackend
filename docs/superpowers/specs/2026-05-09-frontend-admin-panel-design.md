# Frontend Admin Panel - Design Spec

**Date:** 2026-05-09
**Brainstorm session:** initial frontend chunk
**Status:** approved, pre-implementation

## Overview

A web admin panel for the LicenceBackend, covering full lifecycle management of licences, products, and users from a browser. Replaces the current admin path of "DevTools CLI + curl + Scalar UI" for day-to-day operations. Customer-facing self-service is **out of scope for this spec** - it ships as a separate later chunk that will reuse the same auth and design system.

The frontend is built and deployed as part of the same Git repository as the backend, served from the same origin in production, so the auth model can rely on `SameSite=Strict` cookies without CSRF token plumbing.

## Audience

- **Admin users** (`role=admin` in the backend): full CRUD on licences, products, users; binding admin (HWID clear, IP allowlist edit); per-licence audit log review.
- Non-admin users are blocked from every route except `/login` and `/me`. They will get the user portal in a later chunk; for now they see a "no access" state if they reach this app.

## Scope

### In v1 (this chunk)

**Auth & session**
- Login, logout (current session), logout-everywhere
- Cookie-based persistent session via httpOnly refresh + in-memory access JWT
- Silent refresh on a 14-minute schedule
- Auto-resume on cold boot from valid cookie

**Licences (full lifecycle)**
- List with search by key/owner/product, filter by status and product
- Create (specifying productId + userId|email, optional expiresAt + notes)
- Detail view: header with key + status, audit log, status history (collapsed), binding history (collapsed)
- Status changes: active ⇄ suspended ⇄ revoked, with reason
- Binding admin: clear HWID (forces re-pin on next verify), edit IP allowlist (CIDR list)
- Per-licence verification audit log

**Products**
- List, create, detail (slug + displayName + licence count), edit displayName

**Users**
- List, create (email + role + optional password - admin-generated password shown once)
- Suspend / activate, with reason
- Status history view
- Reset password (admin-generated, shown once)

### Explicitly out of scope (deferred)

- Cross-licence verification-attempts dashboard (denial feed at `/verification-attempts?outcome=denied`) - deferred at user's call during brainstorm
- Customer self-service portal (`/me/licences` UI) - separate chunk
- Mobile responsive layout (desktop-only, ≥1024px viewport)
- Dark mode
- i18n / multi-language
- Bulk operations (multi-select on lists)
- Real-time updates / WebSockets
- Email delivery for created users / password resets - depends on Chunk J
- E2E test suite (Playwright) - manual smoke until customer portal exists
- SonarCloud frontend coverage upload

## Chunk plan

This spec produces two chunks, shipped in order:

### Chunk P0 - backend cookie-based refresh (small, ~3-4 hours)

Self-contained backend change. Mergeable on its own with no frontend dependency.

- `POST /sessions`: also sets `Set-Cookie: refresh_token=<opaque>; HttpOnly; Secure; SameSite=Strict; Path=/sessions; Max-Age=2592000`. Removes `refreshToken` from the JSON response body. Response shape becomes `{ accessToken }`.
- `POST /sessions/refresh`: reads the refresh token from the cookie instead of the bare-string JSON body. The body shape from Chunk I (a bare JSON string) is removed. On rotation, sets a new `Set-Cookie` with the rotated value. Returns `{ accessToken }` in JSON.
- `DELETE /sessions` and `DELETE /sessions/all`: clear the cookie via `Set-Cookie: refresh_token=; Max-Age=0; Path=/sessions`.
- Cookie scoped to `Path=/sessions` so it's not transmitted to `/licences/verify` or any other endpoint.
- CSRF tokens are intentionally **not** added in P0. Justification: `SameSite=Strict` + same-origin in prod (Section "Build & deploy" below) blocks cross-site cookie attachment, the refresh endpoint is path-scoped, and access-token-bearing endpoints rely on `Authorization: Bearer` (cookie-irrelevant). If we ever go cross-origin we revisit.
- Test additions in `LicenceBackend.Tests/Api/SessionRefreshTests.cs` (or a new file): login sets cookie + omits refresh from JSON; refresh reads cookie + rotates it; logout clears cookie; refresh without cookie returns 401. Existing rotation-race + suspended-user-revoke tests adapted to cookie shape.

**Out of scope for P0:** any frontend code. Any new endpoints. Any change to `/licences/verify` or any other route.

### Chunk P1 - frontend admin panel (larger, multi-day)

Builds on P0. The rest of this spec describes P1.

## Stack

| Concern | Tool | Version intent |
|---|---|---|
| Runtime | React | 19.x |
| Build tool | Vite | 7.x |
| Language | TypeScript | strict mode |
| Package manager | pnpm | latest stable |
| UI primitives | Radix UI + shadcn/ui | shadcn copied into repo, owned |
| Styling | Tailwind CSS | v4 |
| Router | TanStack Router | latest, file-based routes, type-safe |
| Server state | TanStack Query | latest |
| Tables | TanStack Table | latest |
| Forms | react-hook-form + zod | latest |
| API client | orval (generated from backend OpenAPI) | latest, output committed |
| Toasts | sonner | latest |
| Tests | Vitest + React Testing Library | latest |
| Lint/format | ESLint + Prettier | flat config; `--max-warnings=0` in CI |

Versions are pinned in `package.json` at install time to the latest stable from each package's registry. If any major version is in beta or pre-release at install, drop to the previous stable.

## Repo layout

```
LicenceBackend/                          (existing repo root)
├── frontend/                            (NEW - this chunk)
│   ├── src/
│   │   ├── api/
│   │   │   └── generated/               (orval output, committed)
│   │   ├── auth/
│   │   │   ├── access-token-store.ts    (zustand or context)
│   │   │   ├── refresh-interceptor.ts   (fetch wrapper)
│   │   │   └── auth-layout.tsx          (TanStack Router layout)
│   │   ├── routes/                      (TanStack file-based)
│   │   │   ├── __root.tsx
│   │   │   ├── login.tsx
│   │   │   ├── _authed/
│   │   │   │   ├── route.tsx            (auth + role gate)
│   │   │   │   ├── licences/...
│   │   │   │   ├── products/...
│   │   │   │   ├── users/...
│   │   │   │   └── me.tsx
│   │   ├── components/                  (shadcn primitives, copied + themed)
│   │   ├── features/
│   │   │   ├── licences/                (LicenceKey, StatusPill, SecretRevealOnce, AuditTimeline, ConfirmDestructive)
│   │   │   ├── products/
│   │   │   └── users/
│   │   ├── lib/                         (formatters, IP redaction, relative-time)
│   │   └── main.tsx
│   ├── public/
│   ├── index.html
│   ├── package.json
│   ├── pnpm-lock.yaml
│   ├── tsconfig.json
│   ├── vite.config.ts
│   ├── tailwind.config.ts
│   └── orval.config.ts
└── (everything else unchanged)
```

## Auth - frontend handling

### Access-token store

A small zustand store (preferred over React context for granular subscriptions) holding:
```ts
{ accessToken: string | null; expiresAt: number | null }
```
Never persisted. Set by the login mutation, the refresh mutation, and the cold-boot bootstrap. Cleared by logout and by 401-after-refresh.

### Refresh interceptor

A wrapper around `fetch` (passed to orval as the request implementation):

1. Reads access token from the store; if present and not expired, attaches `Authorization: Bearer <token>`.
2. Includes `credentials: 'include'` so the refresh cookie is sent on `/sessions/*` requests.
3. On a 401 response from any non-`/sessions/refresh` route: enqueue the in-flight request, fire `POST /sessions/refresh`, on success update the store and replay the queued request once. On refresh failure: clear the store, navigate to `/login`. Single-flight so concurrent 401s don't stampede the refresh endpoint.
4. On a 401 from `/sessions/refresh` itself: clear the store, navigate to `/login`.

### Silent refresh schedule

A TanStack Query mutation triggered every 14 minutes (just inside the 15-min access TTL) using `setInterval` registered in `auth-layout.tsx`. Pauses when `document.visibilityState === 'hidden'` and resumes on visibility change. Avoids burning refresh ops on backgrounded tabs.

### Cold-boot resume

On `_authed/route.tsx` mount: if the access store has no token, fire `POST /sessions/refresh`. On 200, populate the store and proceed. On 401, redirect to `/login`. This makes a returning user with a valid cookie skip the login page entirely.

### Logout

- "Sign out": clear the access store + `DELETE /sessions` (clears cookie). Navigate to `/login`.
- "Sign out everywhere": clear the access store + `DELETE /sessions/all`. Navigate to `/login`.

## Routes

```
/login                      anonymous; redirects authed users to /licences
/                           redirects authed users to /licences
/licences                   list + search + status/product filters
/licences/new               create form
/licences/$id               detail, with sub-routes:
  /licences/$id/audit       (default tab - verification audit log; highest-frequency reason to open a licence)
  /licences/$id/status      status history
  /licences/$id/bindings    binding history + edit HWID/IP
/products                   list
/products/new               create
/products/$id               detail (slug, displayName, licence count, edit displayName)
/users                      list
/users/new                  create
/users/$id                  detail (status, status history, suspend/activate, reset password)
/me                         current admin profile (read-only in v1)
```

Tabs on detail pages are sub-routes (deep-linkable). The default sub-route for licence detail is `audit` - the highest-frequency reason an admin opens a licence. There is no separate "Overview" tab because the page header already shows the identifying fields (key, status pill, owner, product, expires_at) above the tab strip.

## Navigation

**Left sidebar.** Three top-level items: Licences, Products, Users. Active item: charcoal background, cream text. Hover: subtle background tint shift on `surface.sunken`. Bottom of sidebar: avatar (first-initial in `accent.DEFAULT` rust) + name + role; click opens menu (Profile -> `/me`, Sign out, Sign out everywhere).

**No top bar.** Page header lives in the main content area: display-font title (Fraunces), counts pill, primary action button right-aligned. Breadcrumbs only on detail pages, in subtle text above the title.

## Page patterns

### List pages
- Toolbar: search input (full-width flex-grow) + filter pills + primary `+ New <thing>` button (charcoal, ink-light text).
- `<DataTable>` (TanStack Table + shadcn): sortable columns, sticky header, row-hover background, three-dot row menu (Open / Suspend / Revoke / etc.). Server-side pagination using page-based offsets (matches the backend's existing `PagedResponse<T>` shape - `{ items, page, pageSize, total }`).
- Empty state: centered illustration-or-icon + "No licences yet" + primary CTA. Use the rust accent here.

### Detail pages
- Header block: most-identifying field (licence key with `LicenceKey` chip, or user email) + status pill + key actions (Suspend/Revoke for licences, Suspend for users) inline.
- Tabs as sub-routes (see Routes). Each tab is its own component.
- Destructive actions (Revoke, Delete user - though delete isn't in v1) wrapped in `ConfirmDestructive` with typed-confirmation field (user types "REVOKE" to enable the button).

### Create pages
- Full-page form with sectioned cards (e.g., "Identity" / "Expiry" / "Notes" for licence creation).
- Validation: react-hook-form + zod schemas. Field errors inline below input. Form-level errors at top.
- Submit: primary action bottom-right, secondary "Cancel" -> previous list page.
- Success: route to detail page + sonner toast.

## Visual system

### Tailwind tokens

```ts
// tailwind.config.ts excerpt
colors: {
  surface:   { DEFAULT: '#fdf8f3', elevated: '#ffffff', sunken: '#f7efe5' },
  ink:       { DEFAULT: '#2a1f17', muted: '#5d4d3e', subtle: '#8a7a68' },
  border:    { DEFAULT: '#efe5d8', strong: '#d8c8b0' },
  accent:    { DEFAULT: '#b85c3a', soft: '#f4d8cc' },
  status: {
    active:    { bg: '#e8f3e8', fg: '#2d5a2d' },
    suspended: { bg: '#fef0d4', fg: '#8a5a00' },
    revoked:   { bg: '#fce0e0', fg: '#8a2828' },
  },
},
fontFamily: {
  sans:    ['Inter', 'system-ui', 'sans-serif'],
  display: ['Fraunces', 'Georgia', 'serif'],
  mono:    ['JetBrains Mono', 'ui-monospace', 'monospace'],
},
borderRadius: { DEFAULT: '6px', lg: '8px', pill: '99px' },
boxShadow: {
  card: '0 1px 2px rgba(42,31,23,0.04), 0 1px 3px rgba(42,31,23,0.05)',
},
```

Fonts loaded via `@fontsource` packages (Inter, Fraunces, JetBrains Mono) - no external CDN, no FOUT.

### Accent discipline

The rust accent `#b85c3a` appears only on:
- The user avatar in the sidebar
- Primary CTAs in empty states
- Hyperlinks in audit log entries
- Hover underline on the `LicenceKey` chip

Everywhere else, primary actions use charcoal. The dominant feel is cream + charcoal; rust is punctuation.

## Custom components

Beyond shadcn primitives:

- `LicenceKey` - monospace chip, on-click copies to clipboard + sonner toast. Hovering reveals the full key when truncated; default rendering shows `LIC-XXXXX-...-XXXXX` with the middle elided in tables.
- `StatusPill` - three variants (active / suspended / revoked) using `colors.status`.
- `SecretRevealOnce` - large centered display for the licence key shown once on creation. Big monospace text, copy button, "I've saved this" dismiss button. Once dismissed, the key is gone from the UI (matches backend's single-show contract).
- `AuditTimeline` - vertical list of audit entries: timestamp (relative + absolute on hover), actor (admin email or "first-use"), action (color-coded by outcome), redacted IP (`192.168.x.x` style). Reverse-chronological.
- `ConfirmDestructive` - wrapped shadcn Dialog: title, description, optional typed-confirmation field, primary destructive button (status.revoked.bg with status.revoked.fg text). For revoke/delete only.

## API client (orval)

`orval.config.ts` points at the backend's OpenAPI document. Output: typed TanStack Query hooks at `frontend/src/api/generated/`.

- Output is **committed** - type errors surface in PRs, and CI fails if the generated client drifts from what's committed.
- CI step: `pnpm orval` and `git diff --exit-code frontend/src/api/generated/` after - fail if drift.
- Regeneration: developer runs `pnpm orval` after backend changes the OpenAPI doc, commits the resulting diff alongside the API change.

The orval-generated `axios`/`fetch` instance is replaced with our custom fetch wrapper (see "Auth - refresh interceptor") so every request goes through the auth flow.

## Testing

| Layer | Tool | What's covered |
|---|---|---|
| Unit | Vitest | Auth store, zod schemas, format helpers (relative time, IP redaction), refresh-interceptor retry/single-flight logic |
| Component | Vitest + React Testing Library | `LicenceKey` (copy on click), `StatusPill` (variant rendering), `SecretRevealOnce` (reveal/dismiss), `ConfirmDestructive` (typed-confirmation gate). Render + interaction; no network |
| Integration | None in P1 | The orval hooks are thin - exercised manually in dev |
| E2E | None in P1 | Deferred until customer portal exists |
| Lint/typecheck | ESLint + tsc + Prettier | CI: `--max-warnings=0`, `tsc --noEmit`, `prettier --check` |
| Manual smoke | Local Vite + real backend | Standard chunk-completion verification - log in, full CRUD pass on each resource, log out + cold-boot resume |

## Build & deploy

### Dev
- `pnpm --filter frontend dev` runs Vite dev server on `http://localhost:5173`.
- Vite proxy config forwards `/sessions/*`, `/licences/*`, `/products/*`, `/users/*`, `/me`, `/health` to `https://localhost:5001`. Browser sees a single origin -> cookie-based auth works without CORS.
- ASP.NET Core continues running on its own port; both processes run side-by-side during dev.

### Prod
- `pnpm --filter frontend build` outputs to `LicenceBackend.Api/wwwroot/` (configured in `vite.config.ts` `build.outDir`).
- `Program.cs` adds, in non-Development:
  - `app.UseStaticFiles();` (after `UseForwardedHeaders`, before `UseAuthentication`)
  - `app.MapFallbackToFile("index.html");` (after `MapControllers`)
- Single deployable artifact - same `dotnet publish` output as today, with the SPA bundled into `wwwroot/`.
- Same-origin in prod ⇒ `SameSite=Strict` cookie works without CORS, no CSRF tokens.

### CI

Add a `frontend-build-test` job to `.github/workflows/ci.yml`:

- Triggered on changes under `frontend/**` (or always; cheap enough).
- Steps: setup pnpm + Node, `pnpm install --frozen-lockfile`, `pnpm orval` + drift check, `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build`.
- The existing `publish-api` job's `dotnet publish` automatically picks up the built `wwwroot/` if the frontend job ran first; sequence them via `needs:`.

## Open assumptions

- **Production deploy target is still the same `dotnet publish` artifact path** (assumed because Chunk N is deferred). If that changes, the prod-serve story may need to split (frontend on CDN, backend separate). Same-origin posture in this spec assumes single artifact.
- **Backend OpenAPI document is reachable from `pnpm orval`** at dev time. If the doc location moves, `orval.config.ts` updates accordingly.
- **No customer-facing access in v1.** If a real customer ever logs in (because we accidentally created them as `role=user` and they find the URL), they get a "no access" state. The customer portal chunk replaces this with a real `/me/licences` view.
- **`@fontsource` packages for Inter/Fraunces/JetBrains Mono are licensed for our use.** All three are SIL OFL or open licenses at the time of writing - confirm at install time.

## Security posture

This spec inherits the backend's threat model (`docs/THREAT_MODEL.md`). Frontend-specific notes:

- **Access tokens are never persisted.** XSS in the admin panel cannot exfiltrate a long-lived token via `localStorage` because there isn't one.
- **Refresh tokens are httpOnly + same-site strict.** XSS can't read the cookie. Cross-site requests can't trigger the refresh endpoint (cookie not attached).
- **Sensitive responses are not logged.** The `SecretRevealOnce` flow renders directly from the create-licence response and never writes to console / sessionStorage / localStorage. The orval-generated client must not log response bodies in production builds.
- **Generated client is committed and reviewed.** Supply chain: a malicious change to the orval generator could inject code; committing the output makes such changes diff-visible.
- **No third-party analytics in v1.** Avoids any data-egress channel that could carry licence keys or admin identifiers off-site.
