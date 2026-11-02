import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { DevSignIn } from "./DevSignIn";
import { hotelsOf } from "./personas";

const personas = [
  { id: "guest-alex", displayName: "Alex Morgan", description: "The in-house guest in room 412.", role: "Guest" },
  { id: "worker-sam", displayName: "Sam Patel", description: "Housekeeping, on duty.", role: "Worker" },
  { id: "admin", displayName: "Priya Shah", description: "Administrator.", role: "Admin" },
];

const sam = { id: "worker-sam", displayName: "Sam Patel", description: "Housekeeping.", role: "Worker", propertyId: "aurora", propertyName: "Aurora Grand" };

const hotels = [
  sam,
  { id: "worker-lakshmi", displayName: "Lakshmi Iyer", description: "Housekeeping.", role: "Worker", propertyId: "mysore", propertyName: "Mysore Grand" },
  { id: "manager", displayName: "Dana Reyes", description: "Duty manager.", role: "Manager", propertyId: null, propertyName: null },
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

  it("at a chosen hotel, offers its people and those who work across hotels", async () => {
    render(<DevSignIn client={api(() => json(hotels))} roles={workerRoles} hotel="mysore" onSignedIn={vi.fn()} />);

    expect(await screen.findByRole("button", { name: "Lakshmi Iyer" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Dana Reyes" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Sam Patel" })).toBeNull();
  });

  it("lists the hotels the personas belong to, once each", () => {
    expect(hotelsOf([...hotels, { ...sam, id: "worker-jo" }])).toEqual([
      { id: "aurora", name: "Aurora Grand" },
      { id: "mysore", name: "Mysore Grand" },
    ]);
  });

  it("says so when dev tokens are off", async () => {
    render(<DevSignIn client={api(() => new Response(null, { status: 404 }))} roles={workerRoles} onSignedIn={vi.fn()} />);

    expect((await screen.findByRole("alert")).textContent).toContain("dev tokens are off");
  });
});
