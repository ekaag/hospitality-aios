import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { FailedEvents } from "./FailedEvents";
import { fakeApi } from "./fakeApi";

const failure = (eventId: string, stage: "Publish" | "Handle", handler: string | null, error: string) => ({
  eventId,
  eventType: "HumanActionCompleted",
  stage,
  handler,
  error,
  attempts: 5,
  aggregateType: "HumanAction",
  aggregateId: "a1",
  correlationId: "c1",
  recordedAt: "2026-11-02T09:00:00Z",
  failedAt: stage === "Publish" ? "2026-11-02T09:05:00Z" : null,
});

afterEach(cleanup);

describe("FailedEvents", () => {
  it("lists what failed with its error and flow, and retries a handler's failure for that handler", async () => {
    const listed = [failure("e1", "Handle", "guest-service.human-action-completed", "The PMS is down."), failure("e2", "Publish", null, "TimeoutException")];
    const { client, received } = fakeApi(({ method, path }) => {
      if (method === "GET" && path === "/api/v1/failed-events") {
        return { body: listed };
      }

      if (method === "POST" && path === "/api/v1/failed-events/e1/retry") {
        listed.shift();
        return { body: { eventId: "e1", stage: "Handle", handler: "guest-service.human-action-completed" } };
      }

      return undefined;
    });
    render(<FailedEvents client={client} />);

    const handling = await screen.findByRole("row", { name: /Handling \(guest-service.human-action-completed\)/ });
    expect(handling.textContent).toContain("The PMS is down.");
    expect(handling.textContent).toContain("c1");
    expect(screen.getByRole("row", { name: /Publishing/ }).textContent).toContain("2026-11-02 09:05:00");

    const [first] = screen.getAllByRole("button", { name: "Retry" });
    if (first) fireEvent.click(first);

    expect(await screen.findByRole("status")).toHaveProperty("textContent", "HumanActionCompleted was sent to be processed again.");
    await waitFor(() => expect(screen.queryByRole("row", { name: /Handling/ })).toBeNull());
    const retry = received.find((request) => request.method === "POST");
    expect(retry?.body).toEqual({ handler: "guest-service.human-action-completed" });
  });

  it("retries a publish failure without naming a handler, and says when there is nothing to retry", async () => {
    let listed = [failure("e2", "Publish", null, "TimeoutException")];
    const { client, received } = fakeApi(({ method, path }) => {
      if (method === "GET" && path === "/api/v1/failed-events") {
        return { body: listed };
      }

      if (method === "POST" && path === "/api/v1/failed-events/e2/retry") {
        listed = [];
        return { body: { eventId: "e2", stage: "Publish", handler: null } };
      }

      return undefined;
    });
    render(<FailedEvents client={client} />);

    fireEvent.click(await screen.findByRole("button", { name: "Retry" }));

    expect(await screen.findByText("Nothing has failed. Every event was published and handled.")).toBeTruthy();
    expect(received.find((request) => request.method === "POST")?.body).toEqual({});
  });

  it("says so when a retry is refused, and reads the list again", async () => {
    const { client } = fakeApi(({ method, path }) => {
      if (method === "GET" && path === "/api/v1/failed-events") {
        return { body: [failure("e3", "Handle", "content.index", "boom")] };
      }

      if (method === "POST") {
        return { status: 404, body: { type: "about:blank", title: "Not found", status: 404, code: "failed_event.not_found" } };
      }

      return undefined;
    });
    render(<FailedEvents client={client} />);

    fireEvent.click(await screen.findByRole("button", { name: "Retry" }));

    expect((await screen.findByRole("alert")).textContent).toContain("could not be retried");
  });
});
