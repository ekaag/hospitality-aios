import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import type { Connect } from "@hospitalityos/realtime";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { rememberConversation } from "./conversation";
import { Stay } from "./Stay";

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

const message = (id: string, sender: string, text: string, clientMessageId: string | null = null) => ({
  id,
  conversationId: "c1",
  sender,
  text,
  clientMessageId,
  sentAt: "2026-10-31T10:22:00Z",
});

/**
 * The guest API as the stay page uses it: one conversation, its messages, and the stay's requests, with every call
 * recorded; `failNextPost` makes the next message fail to send.
 */
function hotel() {
  const state = {
    messages: [] as ReturnType<typeof message>[],
    requests: [] as unknown[],
    calls: [] as string[],
    posted: [] as { text: string; clientMessageId: string }[],
    failNextPost: false,
  };
  const respond = async (request: Request): Promise<Response> => {
    const path = new URL(request.url).pathname;
    state.calls.push(`${request.method} ${path}`);

    if (request.method === "POST" && path === "/api/v1/conversations") {
      return json({ id: "c1", propertyId: "p", stayContextId: "s", status: "Open", startedAt: "2026-10-31T10:00:00Z" }, 201);
    }

    if (request.method === "GET" && path === "/api/v1/conversations/c1") {
      return json({ id: "c1", propertyId: "p", stayContextId: "s", status: "Open", startedAt: "2026-10-31T10:00:00Z" });
    }

    if (request.method === "GET" && path === "/api/v1/conversations/c1/messages") {
      return json(state.messages);
    }

    if (request.method === "POST" && path === "/api/v1/conversations/c1/messages") {
      const body = (await request.json()) as { text: string; clientMessageId: string };
      state.posted.push(body);

      if (state.failNextPost) {
        state.failNextPost = false;

        return json({ title: "Unavailable", status: 503 }, 503);
      }

      const recorded = message(`m-${body.clientMessageId}`, "Guest", body.text, body.clientMessageId);
      state.messages.push(recorded);

      return json(recorded, 201);
    }

    if (request.method === "GET" && path === "/api/v1/service-requests") {
      return json(state.requests);
    }

    return json({ title: "Not found", status: 404 }, 404);
  };
  const client: ApiClient = createApiClient({
    baseUrl: "http://api.test",
    token: () => "t",
    fetch: ((request: Request) => respond(request)) as typeof globalThis.fetch,
  });

  return { state, client };
}

function hub() {
  let changed: (() => void) | undefined;
  const connect: Connect = () =>
    ({
      on: (_type: string, handler: () => void) => {
        changed = handler;
      },
      onreconnected: () => undefined,
      start: () => Promise.resolve(),
      stop: () => Promise.resolve(),
    }) as unknown as ReturnType<Connect>;

  return { connect, change: () => act(() => changed?.()) };
}

beforeEach(() => localStorage.clear());
afterEach(cleanup);

describe("Stay", () => {
  it("chats with the hotel: the reply arrives when the stay changes", async () => {
    const { state, client } = hotel();
    const live = hub();
    let ids = 0;
    render(<Stay client={client} token="t" persona="guest-alex" newId={() => `id-${String(++ids)}`} connect={live.connect} />);

    fireEvent.click(await screen.findByRole("button", { name: "Could I get two towels please?" }));

    await screen.findByText("The concierge is replying…");
    expect(state.posted).toEqual([{ text: "Could I get two towels please?", clientMessageId: "id-1" }]);

    state.messages.push(message("m2", "Concierge", "Of course. I've asked our team to bring 2 bath towels to Room 412."));
    state.requests.push({
      id: "r1",
      serviceName: "Housekeeping amenities",
      itemName: "Bath towel",
      quantity: 2,
      description: null,
      location: "Room 412",
      status: "Open",
      localRequestedAt: "2026-10-31T10:22:00",
    });
    live.change();

    await screen.findByText("Of course. I've asked our team to bring 2 bath towels to Room 412.");
    expect(screen.getByText("2 × Bath towel")).toBeTruthy();
    expect(screen.getByText("Received")).toBeTruthy();
    expect(screen.queryByText("The concierge is replying…")).toBeNull();
  });

  it("sends a message that failed again with the same ID, so it is recorded once", async () => {
    const { state, client } = hotel();
    state.failNextPost = true;
    let ids = 0;
    render(<Stay client={client} token="t" persona="guest-alex" newId={() => `id-${String(++ids)}`} connect={hub().connect} />);

    const box = await screen.findByLabelText("Message");
    await waitFor(() => expect(state.calls).toContain("GET /api/v1/conversations/c1/messages"));
    fireEvent.change(box, { target: { value: "Is the pool open?" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    fireEvent.click(await screen.findByRole("button", { name: "Try again" }));

    await waitFor(() => expect(screen.queryByText("Not sent.", { exact: false })).toBeNull());
    expect(state.posted.map((posted) => posted.clientMessageId)).toEqual(["id-1", "id-1"]);
    expect(screen.getAllByText("Is the pool open?")).toHaveLength(1);
  });

  it("carries on with the guest's open conversation", async () => {
    const { state, client } = hotel();
    rememberConversation("guest-alex", "c1");
    state.messages.push(message("m1", "Guest", "Hello", "x"), message("m2", "Concierge", "Hello! How can I help?"));

    render(<Stay client={client} token="t" persona="guest-alex" connect={hub().connect} />);

    await screen.findByText("Hello! How can I help?");
    expect(state.calls).not.toContain("POST /api/v1/conversations");
  });

  it("starts a new conversation when the remembered one is gone", async () => {
    const { state, client } = hotel();
    rememberConversation("guest-alex", "gone");

    render(<Stay client={client} token="t" persona="guest-alex" connect={hub().connect} />);

    await waitFor(() => expect(state.calls).toContain("POST /api/v1/conversations"));
    expect(localStorage.getItem("hospitalityos.guest-web.conversation.guest-alex")).toBe("c1");
  });
});
