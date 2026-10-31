# Frontends

React + TypeScript + Vite apps over the HospitalityOS API, in one pnpm workspace:

| Folder | What | Signs in as | Dev server |
|---|---|---|---|
| `guest-web` | The guest's chat and request status (J3) | Guest | http://localhost:5173 |
| `staff-web` | Assigned work and its steps (J2) | Worker, Manager | http://localhost:5174 |
| `admin-web` | Configuration and the AI trace viewer (J4) | Admin, Manager | http://localhost:5175 |
| `packages/api-client` | The typed API client, generated from `openapi.json` | | |
| `packages/dev-auth` | The local "sign in as" picker | | |

Apps never import from each other; shared code lives in `packages/` (solution-structure.md §7).

## Run locally

1. Start the API with its launch profile, which seeds the Aurora Grand and turns dev tokens on:
   `dotnet run --project src/Hosts/HospitalityOS.Api --launch-profile HospitalityOS.Api` (http://localhost:5080).
2. In `apps/`: `pnpm install`, then `pnpm --filter @hospitalityos/staff-web dev` (or `guest-web`, `admin-web`).

Each dev server proxies `/api` to the API; set `HOSPITALITYOS_API_URL` in an app's `.env.local` to use another one.

## Signing in

In Development the API issues tokens for the seed's people (`/api/dev/tokens`): Alex Morgan, the in-house guest in
room 412; Sam Patel, Jo Okafor, and Ravi Mehta, the workers on the roster; a manager; and an administrator. Each app offers
the personas for its roles. The signing key is made when the API starts, so a restart signs everyone out. Dev tokens can
only be turned on in Development (`Authentication:DevTokens:Enabled`); the apps only send the bearer token, so a real
identity provider replaces them by configuration.

## The API contract

`packages/api-client/openapi.json` is the API's OpenAPI document, committed. The .NET tests fail when the API no longer
matches it. After an intended API change:

1. Rewrite it: run the tests with `HOSPITALITYOS_WRITE_OPENAPI=1`
   (`dotnet test tests/HospitalityOS.IntegrationTests --filter "FullyQualifiedName~OpenApiDocumentTests"`).
2. Regenerate the client: `pnpm generate` in `apps/`, then fix whatever no longer type-checks.

CI checks both steps were done.

## Checks

`pnpm check` runs what CI runs: `lint`, `typecheck`, `test` (Vitest), and `build`.
