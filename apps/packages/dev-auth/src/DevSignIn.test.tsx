import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { DevSignIn } from "./DevSignIn";

const personas = [
  { id: "guest-alex", displayName: "Alex Morgan", description: "The in-house guest in room 412.", role: "Guest" },
  { id: "worker-sam", displayName: "Sam Patel", description: "Housekeeping, on duty.", role: "Worker" },
  { id: "admin", displayName: "Priya Shah", description: "Administrator.", role: "Admin" },
];

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

function api(respond: (request: Request) => Response): ApiClient {
  const fetch = (request: Request) => Promise.resolve(respond(request));

  return createApiClient({ baseUrl: "http://api.test", token: () => undefined, fetch: fetch as typeof globalThis.fetch });
}

const workerRoles = ["Worker", "Manager"];

afterEach(cleanup);

describe("DevSignIn", () => {
  it("offers only the app's personas and signs in as one", async () => {
    const onSignedIn = vi.fn();
    const client = api((request) =>
      request.method === "GET" ? json(personas) : json({ accessToken: "token", expiresAt: "2026-10-30T20:00:00Z", persona: personas[1] }),
    );
    render(<DevSignIn client={client} roles={workerRoles} onSignedIn={onSignedIn} />);

    fireEvent.click(await screen.findByRole("button", { name: "Sam Patel" }));

    await waitFor(() => expect(onSignedIn).toHaveBeenCalledWith({ token: "token", expiresAt: "2026-10-30T20:00:00Z", persona: personas[1] }));
    expect(screen.queryByRole("button", { name: "Alex Morgan" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Priya Shah" })).toBeNull();
  });

  it("says so when dev tokens are off", async () => {
    render(<DevSignIn client={api(() => new Response(null, { status: 404 }))} roles={workerRoles} onSignedIn={vi.fn()} />);

    expect((await screen.findByRole("alert")).textContent).toContain("dev tokens are off");
  });
});
