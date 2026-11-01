import { createApiClient } from "@hospitalityos/api-client";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Guests } from "./Guests";

vi.mock("qrcode", () => ({ default: { toDataURL: (text: string) => Promise.resolve(`data:image/png;qr,${text}`) } }));

const stay = (hasGuestLink: boolean) => ({
  id: "s1",
  guestId: "g1",
  guestName: "Alex",
  unitNumber: "412",
  arrival: "2026-11-01",
  departure: "2026-11-04",
  status: "InHouse",
  hasGuestLink,
});

function api(answer: (method: string, path: string) => { status?: number; body?: unknown } | undefined) {
  const requests: string[] = [];
  const fetch = (request: Request) => {
    const path = new URL(request.url).pathname;
    requests.push(`${request.method} ${path}`);
    const reply = answer(request.method, path) ?? { status: 404, body: { status: 404 } };
    return Promise.resolve(new Response(JSON.stringify(reply.body ?? null), { status: reply.status ?? 200, headers: { "Content-Type": "application/json" } }));
  };

  return { client: createApiClient({ baseUrl: "http://api.test", token: () => "t", fetch: fetch as typeof globalThis.fetch }), requests };
}

afterEach(cleanup);

describe("Guests", () => {
  it("makes a stay's link and shows it once as an address and a QR code", async () => {
    let linked = false;
    const { client } = api((method, path) => {
      if (method === "GET" && path === "/api/v1/stays") {
        return { body: [stay(linked)] };
      }

      if (method === "POST" && path === "/api/v1/stays/s1/guest-link") {
        linked = true;
        return { body: { stayId: "s1", code: "K7QF3-MZP9A-4WX2C-H8RTD", url: "https://guest.example/#link=K7QF3-MZP9A-4WX2C-H8RTD" } };
      }

      return undefined;
    });
    render(<Guests client={client} propertyId="p1" />);

    fireEvent.click(await screen.findByRole("button", { name: "Make link" }));

    const shown = await screen.findByLabelText("Check-in link");
    expect(shown.querySelector("a")?.getAttribute("href")).toBe("https://guest.example/#link=K7QF3-MZP9A-4WX2C-H8RTD");
    expect(screen.getByAltText("QR code for Alex's check-in link").getAttribute("src")).toBe("data:image/png;qr,https://guest.example/#link=K7QF3-MZP9A-4WX2C-H8RTD");
    await screen.findByRole("button", { name: "New link" });
  });

  it("revokes a stay's link", async () => {
    let linked = true;
    const { client, requests } = api((method) => {
      if (method === "GET") {
        return { body: [stay(linked)] };
      }

      linked = false;
      return { body: stay(false) };
    });
    render(<Guests client={client} propertyId="p1" />);

    fireEvent.click(await screen.findByRole("button", { name: "Revoke" }));

    await waitFor(() => expect(screen.queryByRole("button", { name: "Revoke" })).toBeNull());
    expect(requests).toContain("DELETE /api/v1/stays/s1/guest-link");
  });
});
