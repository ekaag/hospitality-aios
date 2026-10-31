import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { fakeApi } from "./fakeApi";
import { Integrations } from "./Integrations";
import { MenusSection, PoliciesSection } from "./sections";
import { Traces } from "./Traces";

const property = "p1";
const gym = "a1";
const names = new Map([[gym, "Gym"]]);

const policy = (status: string, version = 2) => ({
  id: "pol1",
  propertyId: property,
  type: "Children",
  title: "Children in the gym",
  content: "Guests aged 12 to 15 may use the gym with an adult.",
  effectiveFrom: null,
  effectiveTo: null,
  appliesTo: { kind: "Amenity", id: gym },
  status,
  version,
});

afterEach(cleanup);

describe("PoliciesSection", () => {
  it("saves the whole policy with the version it was edited on, keeping what the form does not show", async () => {
    const { client, received } = fakeApi(({ method, path }) =>
      method === "GET" && path === "/api/v1/policies" ? { body: [policy("Active")] } : method === "PUT" ? { body: policy("Active", 3) } : undefined,
    );
    render(<PoliciesSection client={client} propertyId={property} canEdit names={names} />);

    fireEvent.click(await screen.findByRole("button", { name: /Children in the gym/ }));
    expect(screen.getByText("Amenity: Gym")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Policy"), { target: { value: "Guests aged 12 to 15 may use the gym only with an adult." } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await screen.findByText("Saved.");
    const put = received.find((request) => request.method === "PUT");
    expect(put?.path).toBe("/api/v1/policies/pol1");
    expect(put?.ifMatch).toBe('"2"');
    expect(put?.body).toEqual({
      type: "Children",
      title: "Children in the gym",
      content: "Guests aged 12 to 15 may use the gym only with an adult.",
      effectiveFrom: null,
      effectiveTo: null,
      appliesTo: { kind: "Amenity", id: gym },
    });
  });

  it("offers the step its state allows: a draft is activated", async () => {
    const { client, received } = fakeApi(({ method, path }) =>
      method === "GET" && path === "/api/v1/policies" ? { body: [policy("Draft", 1)] } : method === "POST" ? { body: policy("Active") } : undefined,
    );
    render(<PoliciesSection client={client} propertyId={property} canEdit names={names} />);

    fireEvent.click(await screen.findByRole("button", { name: /Children in the gym/ }));
    expect(screen.queryByRole("button", { name: "Retire" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Activate" }));

    await waitFor(() => expect(received.some((request) => request.method === "POST" && request.path === "/api/v1/policies/pol1/activate" && request.ifMatch === '"1"')).toBe(true));
  });

  it("says when someone else changed it meanwhile, and reads it again", async () => {
    const { client, received } = fakeApi(({ method, path }) =>
      method === "GET" && path === "/api/v1/policies"
        ? { body: [policy("Active")] }
        : method === "PUT"
          ? { status: 412, body: { title: "Precondition failed", status: 412, code: "request.version_mismatch" } }
          : undefined,
    );
    render(<PoliciesSection client={client} propertyId={property} canEdit names={names} />);

    fireEvent.click(await screen.findByRole("button", { name: /Children in the gym/ }));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    expect((await screen.findByRole("alert")).textContent).toContain("Someone else changed this");
    expect(received.filter((request) => request.method === "GET")).toHaveLength(2);
  });

  it("is read-only for those who may not change it", async () => {
    const { client } = fakeApi(({ method }) => (method === "GET" ? { body: [policy("Draft", 1)] } : undefined));
    render(<PoliciesSection client={client} propertyId={property} canEdit={false} names={names} />);

    fireEvent.click(await screen.findByRole("button", { name: /Children in the gym/ }));

    expect(screen.queryByRole("button", { name: "Save" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Activate" })).toBeNull();
  });
});

describe("MenusSection", () => {
  it("marks an item sold out without resending the menu", async () => {
    const menu = {
      id: "m1",
      propertyId: property,
      restaurantId: "r1",
      name: "Dinner",
      description: null,
      items: [{ id: "i1", name: "Paneer tikka", description: null, price: { amount: 12.5, currencyCode: "GBP" }, category: null, dietary: ["Vegetarian"], isAvailable: true }],
      version: 4,
    };
    const { client, received } = fakeApi(({ method, path }) =>
      method === "GET" && path === "/api/v1/restaurants"
        ? { body: [{ id: "r1", propertyId: property, name: "Saffron", cuisine: "Indian", description: null, locationSpaceId: null, version: 1 }] }
        : method === "GET" && path === "/api/v1/menus"
          ? { body: [menu] }
          : method === "PUT"
            ? { body: menu }
            : undefined,
    );
    render(<MenusSection client={client} propertyId={property} canEdit names={new Map([["r1", "Saffron"]])} />);

    fireEvent.click(await screen.findByRole("button", { name: /Dinner/ }));
    expect(screen.getByText("GBP 12.50")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Paneer tikka: mark sold out" }));

    await waitFor(() =>
      expect(received.find((request) => request.method === "PUT")).toMatchObject({
        path: "/api/v1/menus/m1/items/i1/availability",
        ifMatch: '"4"',
        body: { isAvailable: false },
      }),
    );
  });
});

describe("Traces", () => {
  const summary = {
    id: "t1",
    propertyId: property,
    agent: "concierge.answer",
    correlationId: "c0ffee00-0000-4000-8000-000000000001",
    triggerType: "GuestMessage",
    triggerId: "g1",
    intent: "question",
    outcome: "Answered",
    startedAt: "2026-10-31T10:00:00Z",
    completedAt: "2026-10-31T10:00:01Z",
  };
  const trace = {
    ...summary,
    causationId: null,
    redacted: true,
    flowEvents: [],
    body: {
      triggerText: "What time does the pool close?",
      retrieved: [],
      capabilityCalls: [
        {
          purpose: "Tool",
          capability: "Property.GetOperatingHours",
          callId: "k1",
          classification: null,
          authorization: "Allowed",
          input: null,
          output: null,
          invocationId: null,
          errorCode: null,
          withheld: true,
        },
      ],
      modelCalls: [],
      grounding: { replies: [{ accepted: true, rejection: null }] },
      response: { text: "The pool is open daily from 07:00 to 22:00.", citations: [{ type: "OperatingHours", id: "h1000000-0000", version: 1 }] },
      usage: { inputTokens: 10, outputTokens: 5 },
    },
  };

  it("filters by outcome and opens a trace in full, as the role may see it", async () => {
    const { client, received } = fakeApi(({ path }) =>
      path === "/api/v1/ai/traces" ? { body: { traces: [summary], next: null } } : path === "/api/v1/ai/traces/t1" ? { body: trace } : undefined,
    );
    render(<Traces client={client} propertyId={property} />);

    fireEvent.change(await screen.findByLabelText("Outcome"), { target: { value: "Uncertain" } });
    fireEvent.click(screen.getByRole("button", { name: "Filter" }));
    await waitFor(() => expect(received.some((request) => request.query.includes("outcome=Uncertain"))).toBe(true));

    fireEvent.click(screen.getByRole("button", { name: new Date(summary.startedAt).toLocaleString() }));

    expect(await screen.findByText("The pool is open daily from 07:00 to 22:00.")).toBeTruthy();
    expect(screen.getByRole("note").textContent).toBe("Some of this trace is withheld for your role.");
    expect(screen.getByText("Input and output withheld.")).toBeTruthy();
    expect(screen.getByText("Accepted")).toBeTruthy();
  });
});

describe("Integrations", () => {
  it("shows what the property and platform use, by name", async () => {
    const { client } = fakeApi(({ path }) =>
      path === "/api/v1/integrations"
        ? {
            body: {
              connectors: [
                { kind: "Pms", provider: "MockPms", status: "Active", healthy: true, problem: null, availableProviders: ["MockPms"] },
                { kind: "Pos", provider: null, status: "NotConfigured", healthy: null, problem: null, availableProviders: [] },
              ],
              ports: [{ port: "EventTransport", scope: "Default", adapter: "InMemory" }],
              inferenceTasks: [{ task: "concierge.answer", capability: "ChatModel", provider: "fake", adapter: "Fake", model: "fake-concierge", fallbacks: [] }],
            },
          }
        : undefined,
    );
    render(<Integrations client={client} propertyId={property} />);

    expect(await screen.findAllByText("MockPms")).toHaveLength(2);
    expect(screen.getByText("Not configured")).toBeTruthy();
    expect(screen.getByText("fake (Fake)")).toBeTruthy();
    expect(screen.getByText("InMemory")).toBeTruthy();
  });
});
