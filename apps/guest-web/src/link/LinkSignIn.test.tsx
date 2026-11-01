import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { loadLink, saveLink, forgetLink, takeLinkFromAddress } from "./guestLink";
import { LinkSignIn } from "./LinkSignIn";

// A stay token whose payload names stay s-1.
const token = `e30.${btoa(JSON.stringify({ stay_id: "s-1" })).replace(/=+$/, "")}.sig`;

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": status < 300 ? "application/json" : "application/problem+json" } });

function api(respond: (request: Request) => Promise<Response> | Response): { client: ApiClient; bodies: unknown[] } {
  const bodies: unknown[] = [];
  const fetch = async (request: Request) => {
    bodies.push(await request.clone().json());
    return respond(request);
  };
  return { client: createApiClient({ baseUrl: "http://api.test", token: () => undefined, fetch: fetch as typeof globalThis.fetch }), bodies };
}

afterEach(cleanup);

describe("LinkSignIn", () => {
  it("opens the stay with the link's code, as a session named by the stay", async () => {
    const onSignedIn = vi.fn();
    const { client, bodies } = api(() => json({ accessToken: token, expiresAt: "2026-11-01T17:00:00Z" }));
    render(<LinkSignIn client={client} code="K7QF3-MZP9A-4WX2C-H8RTD" onSignedIn={onSignedIn} onEnded={vi.fn()} />);

    await waitFor(() =>
      expect(onSignedIn).toHaveBeenCalledWith({
        token,
        expiresAt: "2026-11-01T17:00:00Z",
        persona: { id: "stay-s-1", displayName: "", description: "", role: "Guest" },
      }),
    );
    expect(bodies).toEqual([{ code: "K7QF3-MZP9A-4WX2C-H8RTD" }]);
  });

  it("forgets a link that no longer works, and says to ask the front desk", async () => {
    const onEnded = vi.fn();
    const { client } = api(() => json({ status: 404, code: "guest_link.not_found" }, 404));
    render(<LinkSignIn client={client} code="OLD" onSignedIn={vi.fn()} onEnded={onEnded} />);

    expect((await screen.findByRole("alert")).textContent).toContain("Ask the front desk");
    expect(onEnded).toHaveBeenCalled();
  });

  it("keeps the link when the hotel cannot be reached, and tries again when asked", async () => {
    const onEnded = vi.fn();
    const onSignedIn = vi.fn();
    let reachable = false;
    const { client } = api(() => (reachable ? json({ accessToken: token, expiresAt: "2026-11-01T17:00:00Z" }) : new Response(null, { status: 502 })));
    render(<LinkSignIn client={client} code="CODE" onSignedIn={onSignedIn} onEnded={onEnded} />);

    await screen.findByRole("alert");
    reachable = true;
    fireEvent.click(screen.getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(onSignedIn).toHaveBeenCalled());
    expect(onEnded).not.toHaveBeenCalled();
  });
});

describe("the link on this device", () => {
  it("is taken out of the address, and kept until forgotten", () => {
    const replaced: string[] = [];
    const location = { hash: "#link=K7QF3-MZP9A", pathname: "/", search: "" } as Location;
    const history = { replaceState: (_: unknown, __: string, url: string) => replaced.push(url) } as unknown as History;
    const store = new Map<string, string>();
    const storage = { getItem: (k: string) => store.get(k) ?? null, setItem: (k: string, v: string) => store.set(k, v), removeItem: (k: string) => store.delete(k) } as unknown as Storage;

    const code = takeLinkFromAddress(location, history);
    saveLink(code ?? "", storage);

    expect(code).toBe("K7QF3-MZP9A");
    expect(replaced).toEqual(["/"]);
    expect(loadLink(storage)).toBe("K7QF3-MZP9A");
    forgetLink(storage);
    expect(loadLink(storage)).toBeUndefined();
    expect(takeLinkFromAddress({ hash: "", pathname: "/", search: "" } as Location, history)).toBeUndefined();
  });
});
