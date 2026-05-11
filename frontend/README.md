# LicenceBackend Admin Frontend

## Prerequisites

Node.js 20 LTS (or later) and pnpm 9 (or later). Install pnpm via `npm install -g pnpm` or `corepack enable`.

## Install dependencies

```bash
pnpm install
```

## Run the dev server

```bash
pnpm dev
```

Opens on `http://localhost:5173`. The .NET API must also be running on `https://localhost:5001` - the Vite dev server proxies all API requests to it. Start the backend in a separate terminal with:

```bash
dotnet run --project LicenceBackend.Api --launch-profile https
```

The `--launch-profile https` flag is required because the backend defaults to HTTP and the Secure cookie used for the refresh token will not be sent over HTTP.

The SPA owns the root path space; all backend calls are made under `/api`, which the dev proxy strips before forwarding to the .NET API (and it rewrites the refresh-cookie `Path` from `/sessions` to `/api/sessions`). This keeps backend routes like `/me` and `/licences` from colliding with client-side routes. In production (Chunk P1f) the host serving `dist/` must do the equivalent: route `/api/*` to the backend with the prefix stripped, or run the backend with `app.UsePathBase("/api")`.

## Build for production

```bash
pnpm build
```

Output goes to `frontend/dist/`. In production the .NET API serves this directory via `app.UseStaticFiles()` and `app.MapFallbackToFile("index.html")` (wired in Chunk P1f).

## Regenerate the API client

```bash
pnpm orval
```

Requires the .NET API to be running on HTTPS. Run this after any backend OpenAPI changes, then commit the diff in `src/api/generated/`.

## Lint

```bash
pnpm lint
```

Runs ESLint with `--max-warnings=0`. Fix all warnings before committing.

## Type check

```bash
pnpm typecheck
```

Runs `tsc --noEmit`. Must pass with zero errors.

## Tests

```bash
pnpm test
```

Runs Vitest in run mode (single-pass, no watch). Use `pnpm test --watch` during development.
