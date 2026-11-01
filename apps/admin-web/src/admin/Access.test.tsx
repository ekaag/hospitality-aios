import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { Access } from "./Access";
import { fakeApi } from "./fakeApi";

const property = "p1";
const sam = { id: "s1", propertyId: property, displayName: "Sam Patel", skills: [], isOnDuty: true, version: 1 };
const jo = { id: "s2", propertyId: property, displayName: "Jo Okafor", skills: [], isOnDuty: false, version: 1 };

const access = (id: string, displayName: string, status: string, staffMemberId: string | null = null) => ({
  id,
  displayName,
  role: staffMemberId ? "Worker" : "Manager",
  staffMemberId,
  status,
  inviteExpiresAt: null,
  linkedAt: null,
  revokedAt: null,
  version: 1,
});

afterEach(cleanup);

describe("Access", () => {
  it("invites a worker as a roster entry without access and shows the code once", async () => {
    const listed = [access("a1", "Sam Patel", "Active", sam.id)];
    const { client, received } = fakeApi(({ method, path }) => {
      if (method === "GET" && path === "/api/v1/access") {
        return { body: listed };
      }

      if (method === "GET" && path === "/api/v1/staff") {
        return { body: [sam, jo] };
      }

      if (method === "POST" && path === "/api/v1/access") {
        const created = access("a2", "Jo Okafor", "Invited", jo.id);
        listed.push(created);
        return { status: 201, body: { access: created, code: "K7QF3-MZP9A-4WX2C-H8RTD", expiresAt: "2026-11-08T09:00:00Z" } };
      }

      return undefined;
    });
    render(<Access client={client} propertyId={property} />);

    await screen.findByRole("row", { name: /Sam Patel/ });
    const rosterEntries = [...(screen.getByLabelText("Roster entry") as HTMLSelectElement).options].map((option) => option.text);
    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Jo Okafor" } });
    fireEvent.change(screen.getByLabelText("Roster entry"), { target: { value: jo.id } });
    fireEvent.click(screen.getByRole("button", { name: "Invite" }));

    expect((await screen.findByLabelText("New invite")).textContent).toContain("K7QF3-MZP9A-4WX2C-H8RTD");
    expect(rosterEntries).toEqual(["Choose…", "Jo Okafor"]);
    expect(received.find((request) => request.method === "POST")?.body).toEqual({ displayName: "Jo Okafor", role: "Worker", staffMemberId: jo.id });
    await screen.findByRole("row", { name: /Jo Okafor/ });
  });

  it("revokes access, and says why it cannot", async () => {
    let revoked = false;
    const { client, received } = fakeApi(({ method, path }) => {
      if (method === "GET" && path === "/api/v1/access") {
        return { body: [access("a1", "Dana Reyes", revoked ? "Revoked" : "Active"), access("a2", "Priya Shah", "Active")] };
      }

      if (method === "GET" && path === "/api/v1/staff") {
        return { body: [] };
      }

      if (path === "/api/v1/access/a1/revoke") {
        revoked = true;
        return { body: access("a1", "Dana Reyes", "Revoked") };
      }

      if (path === "/api/v1/access/a2/revoke") {
        return { status: 403, body: { status: 403, code: "access.revoke_self" } };
      }

      return undefined;
    });
    render(<Access client={client} propertyId={property} />);

    const [dana, priya] = (await screen.findAllByRole("button", { name: "Revoke" })) as [HTMLElement, HTMLElement];
    fireEvent.click(dana);
    await waitFor(() => expect(screen.getByRole("row", { name: /Dana Reyes/ }).textContent).toContain("Revoked"));
    fireEvent.click(priya);

    expect((await screen.findByRole("alert")).textContent).toBe("You cannot revoke your own access.");
    expect(received.filter((request) => request.method === "POST").map((request) => request.path)).toEqual(["/api/v1/access/a1/revoke", "/api/v1/access/a2/revoke"]);
  });
});
