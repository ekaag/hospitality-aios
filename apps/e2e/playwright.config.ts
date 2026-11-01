import { resolve } from "node:path";
import { defineConfig, devices } from "@playwright/test";

/**
 * The spec §43 demo in the browser (L2): the real API, with the Aurora Grand seeded and the Fake models playing their
 * local script, and the guest and staff apps in front of it, as people use them. On ports apart from the local dev
 * servers (5080, 5173, 5174), so running the demo never disturbs apps a developer has open.
 */
const api = "http://localhost:5280";
const guest = "http://localhost:5273";
const staff = "http://localhost:5274";

// Today, before the evening's events (the wine tasting at 18:30 and jazz from 20:00 London time), whenever the tests run.
const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/London" }).format(new Date());
const demoStart = `${today}T17:00:00Z`;

const root = resolve(import.meta.dirname, "../..");
const output = resolve(root, "artifacts/e2e/bin/HospitalityOS.Api/debug");

export default defineConfig({
  testDir: "tests",
  // The tests share one seeded hotel and Alex's one stay, so they run one at a time.
  workers: 1,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  reporter: process.env.CI ? [["list"], ["html", { open: "never", outputFolder: "playwright-report" }]] : "list",
  use: {
    ...devices["Desktop Chrome"],
    trace: "retain-on-failure",
  },
  webServer: [
    {
      // Built into its own folder, so an API a developer is running keeps its files.
      command: `dotnet build "${resolve(root, "src/Hosts/HospitalityOS.Api")}" --artifacts-path "${resolve(root, "artifacts/e2e")}" && dotnet "${resolve(output, "HospitalityOS.Api.dll")}" --contentRoot "${output}"`,
      cwd: root,
      url: `${api}/health`,
      timeout: 300_000,
      reuseExistingServer: false,
      stdout: "ignore",
      env: {
        ASPNETCORE_URLS: api,
        ASPNETCORE_ENVIRONMENT: "Development",
        Seed__AuroraGrand__Enabled: "true",
        Authentication__DevTokens__Enabled: "true",
        Inference__Providers__fake__Settings__Script: "aurora-grand",
        GuestLinks__BaseUrl: guest,
        Demo__Clock__Start: demoStart,
      },
    },
    {
      command: "pnpm --filter @hospitalityos/guest-web exec vite --port 5273 --strictPort",
      cwd: resolve(import.meta.dirname, ".."),
      url: guest,
      timeout: 120_000,
      reuseExistingServer: false,
      env: { HOSPITALITYOS_API_URL: api },
    },
    {
      command: "pnpm --filter @hospitalityos/staff-web exec vite --port 5274 --strictPort",
      cwd: resolve(import.meta.dirname, ".."),
      url: staff,
      timeout: 120_000,
      reuseExistingServer: false,
      env: { HOSPITALITYOS_API_URL: api },
    },
  ],
});

export const apps = { guest, staff };
