# Frontends

React + TypeScript + Vite apps over the HospitalityOS API, in one pnpm workspace:

| Folder | What | Signs in as | Dev server |
|---|---|---|---|
| `guest-web` | The guest's chat and request status (J3) | Guest | http://localhost:5173 |
| `staff-web` | The property's work in four views, with live updates (J2) | Worker, Manager | http://localhost:5174 |
| `admin-web` | The property's configuration, integrations, and the AI trace viewer (J4); managers read, administrators also edit | Admin, Manager | http://localhost:5175 |
| `packages/api-client` | The typed API client, generated from `openapi.json` | | |
| `packages/dev-auth` | The local "sign in as" picker | | |
| `packages/realtime` | Live updates: `useRealtime` over the API's SignalR hub | | |
| `packages/theme` | The look: Fluent 2 tokens and styles (light and dark), the app bar, and the Aurora Grand illustration | | |

Apps never import from each other; shared code lives in `packages/` (solution-structure.md §7).

Every app imports `@hospitalityos/theme/fluent.css` first, then its own layout. The theme follows Fluent 2 (colour,
type, radius, and shadow tokens; acrylic surfaces; light and dark from the system setting, or `data-theme` on `<html>`)
by styling plain semantic HTML, so components keep their roles and labels and no component library is needed. The hotel
picture (`aurora-grand.svg`) is original artwork drawn for the reference hotel.

## Run locally

1. Start the API with its launch profile, which seeds the Aurora Grand and turns dev tokens on:
   `dotnet run --project src/Hosts/HospitalityOS.Api --launch-profile HospitalityOS.Api` (http://localhost:5080).
2. In `apps/`: `pnpm install`, then `pnpm --filter @hospitalityos/staff-web dev` (or `guest-web`, `admin-web`).

Each dev server proxies `/api` (and `guest-web` and `staff-web` also `/hubs`, the live updates) to the API; set `HOSPITALITYOS_API_URL` in an app's `.env.local` to use another one.

The launch profile also has the Fake models play a script for the Aurora Grand, so without a model provider the guest chat answers questions about the pool, gym, children, check-out, pets, smoking, Wi-Fi, parking, dining, and tonight's events, and turns requests ("two towels", "a pillow", "the air con is not working") into work on the staff board. Anything else gets "I don't have confirmed information", and so do status questions in the chat (the requests panel shows where each is).

If a page stays blank after dependencies change, restart its dev server with `--force` (`pnpm --filter @hospitalityos/staff-web exec vite --force`), which rebuilds Vite's dependency cache.

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

## The demo in the browser (L2)

`e2e/` plays the Phase 1 demo script (spec §43) through the real guest and staff apps against the real API: Alex asks
about the pool, the gym policy, vegetarian food, and tonight's events, then asks for two towels; housekeeping sees
"Room 412", accepts, starts, and completes it; Alex is told. The API runs with the Aurora Grand seeded, the Fake models
playing their local script, dev sign-in, and a demo clock (`Demo:Clock:Start`, Development only) set to 17:00 UTC today,
so tonight's events are always ahead.

```sh
pnpm --filter @hospitalityos/e2e exec playwright install chromium   # once
pnpm --filter @hospitalityos/e2e e2e
```

It builds and starts its own API (into `artifacts/e2e`) and apps on ports 5280, 5273, and 5274, so the dev servers on
5080, 5173, 5174, and 5175 keep running. CI runs it in the "Demo in the browser" job; a failure keeps the report and
traces. Running it against an environment in Azure waits for the web apps to be hosted and for test sign-ins there.
