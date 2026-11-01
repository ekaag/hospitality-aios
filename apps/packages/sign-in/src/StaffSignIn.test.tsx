import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ProviderToken, SignInProvider } from "./provider";
import { readSignInConfig } from "./provider";
import { StaffSignIn } from "./StaffSignIn";

const token: ProviderToken = { token: "provider-token", expiresAt: "2026-11-01T10:00:00Z" };
const unlinked = { isLinked: false, isOperator: false, tenantId: null, accessId: null, displayName: null, role: null, staffMemberId: null };
const dana = { ...unlinked, isLinked: true, tenantId: "t-1", accessId: "a-1", displayName: "Dana Reyes", role: "Manager" };
const roles = ["Worker", "Manager"];

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": status < 300 ? "application/json" : "application/problem+json" } });

function provider(signedIn: ProviderToken | undefined): SignInProvider {
  return { token: vi.fn(() => Promise.resolve(signedIn)), signIn: vi.fn(() => Promise.resolve()), signOut: vi.fn(() => Promise.resolve()) };
}

function api(respond: (request: Request) => Response): (bearer: string) => ApiClient {
  return (bearer) =>
    createApiClient({
      baseUrl: "http://api.test",
      token: () => bearer,
      fetch: ((request: Request) => Promise.resolve(respond(request))) as typeof globalThis.fetch,
    });
}

afterEach(cleanup);

describe("StaffSignIn", () => {
  it("offers to sign in when nobody is", async () => {
    const signedOut = provider(undefined);
    render(<StaffSignIn provider={signedOut} roles={roles} onSignedIn={vi.fn()} clientFor={api(() => json(unlinked))} />);

    fireEvent.click(await screen.findByRole("button", { name: "Sign in" }));

    expect(signedOut.signIn).toHaveBeenCalled();
  });

  it("hands over a linked sign-in as a session with its role", async () => {
    const onSignedIn = vi.fn();
    const seen: string[] = [];
    render(
      <StaffSignIn
        provider={provider(token)}
        roles={roles}
        onSignedIn={onSignedIn}
        clientFor={api((request) => {
          seen.push(request.headers.get("Authorization") ?? "");
          return json(dana);
        })}
      />,
    );

    await waitFor(() =>
      expect(onSignedIn).toHaveBeenCalledWith({
        token: "provider-token",
        expiresAt: token.expiresAt,
        persona: { id: "a-1", displayName: "Dana Reyes", description: "", role: "Manager" },
      }),
    );
    expect(seen).toEqual(["Bearer provider-token"]);
  });

  it("asks for the invite code until the sign-in is linked, saying why a code fails", async () => {
    const onSignedIn = vi.fn();
    let redeemed = false;
    const client = api((request) => {
      if (request.method === "POST") {
        return redeemed ? json({}) : json({ status: 404, code: "access.invite_not_found" }, 404);
      }

      return json(redeemed ? dana : unlinked);
    });
    render(<StaffSignIn provider={provider(token)} roles={roles} onSignedIn={onSignedIn} clientFor={client} />);
    const input = await screen.findByLabelText("Invite code");

    fireEvent.change(input, { target: { value: "WRONG-CODE" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue" }));
    expect((await screen.findByRole("alert")).textContent).toContain("does not open any invite");

    redeemed = true;
    fireEvent.click(screen.getByRole("button", { name: "Continue" }));

    await waitFor(() => expect(onSignedIn).toHaveBeenCalled());
  });

  it("tells someone whose role is not the app's, and lets them sign out", async () => {
    const signedIn = provider(token);
    render(<StaffSignIn provider={signedIn} roles={roles} onSignedIn={vi.fn()} clientFor={api(() => json({ ...dana, role: "Admin" }))} />);

    expect((await screen.findByRole("alert")).textContent).toBe("You sign in as Admin; this app is for Worker and Manager.");
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    expect(signedIn.signOut).toHaveBeenCalled();
  });

  it("says so when the API is not reachable", async () => {
    render(<StaffSignIn provider={provider(token)} roles={roles} onSignedIn={vi.fn()} clientFor={api(() => new Response(null, { status: 502 }))} />);

    expect((await screen.findByRole("alert")).textContent).toBe("The API is not reachable.");
  });
});

describe("readSignInConfig", () => {
  it("needs every part, so a half-configured build keeps the dev sign-in", () => {
    const full = { VITE_SIGNIN_AUTHORITY: "https://a", VITE_SIGNIN_CLIENT_ID: "c", VITE_SIGNIN_SCOPE: "api://x/access" };

    expect(readSignInConfig(full)).toEqual({ authority: "https://a", clientId: "c", scope: "api://x/access" });
    expect(readSignInConfig({ ...full, VITE_SIGNIN_SCOPE: "" })).toBeUndefined();
    expect(readSignInConfig({})).toBeUndefined();
  });
});
