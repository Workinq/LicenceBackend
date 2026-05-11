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

## UI components (shadcn/ui)

Primitives live in `src/components/ui/` and are added with the shadcn CLI:

```bash
pnpm dlx shadcn@latest add <component>
```

`src/components/ui/` is treated as generated code: it is excluded from ESLint but still type-checked via imports. The `@/` import alias maps to `src/` (configured in `vite.config.ts` and `tsconfig.app.json`). Note: because `tsconfig.app.json` has no `baseUrl`, the shadcn CLI may write new components to `frontend/@/components/ui/` instead of `frontend/src/components/ui/` - if that happens, move them to the correct path. The theme is defined entirely in `src/index.css` - the warm palette is mapped onto shadcn's semantic CSS variables there; there is no `tailwind.config.ts`. App-specific layout components (header, sidebar, shell, error pages) are in `src/components/layout/`.

## Regenerate the API client

```bash
pnpm orval
```

Requires the .NET API to be running on HTTPS. Run this after any backend OpenAPI changes, then commit the diff in `src/api/generated/`.

The client is generated in `fetch` mode (`client: 'fetch'` in `orval.config.ts`); the generated functions delegate to the `apiClient` mutator in `src/auth/api-client.ts`, which adds the `/api` prefix, attaches the bearer token, retries once on a 401 after a silent refresh, and throws `ApiError` on other non-2xx responses.

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
