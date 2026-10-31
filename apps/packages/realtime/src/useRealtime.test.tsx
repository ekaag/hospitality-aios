import type { HubConnection } from "@microsoft/signalr";
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { useRealtime, type Connect } from "./useRealtime";

/**
 * A connection that records what the hook does with it; `start` resolves when the test says so.
 */
function fake() {
  const handlers = new Map<string, () => void>();
  let reconnected: (() => void) | undefined;
  let finishStart: () => void = () => undefined;
  const connection = {
    on: vi.fn((type: string, handler: () => void) => handlers.set(type, handler)),
    onreconnected: vi.fn((handler: () => void) => {
      reconnected = handler;
    }),
    start: vi.fn(() => new Promise<void>((resolve) => (finishStart = resolve))),
    stop: vi.fn(() => Promise.resolve()),
  };

  return {
    connection,
    connect: vi.fn<Connect>(() => connection as unknown as HubConnection),
    send: (type: string) => handlers.get(type)?.(),
    reconnect: () => reconnected?.(),
    started: () => finishStart(),
  };
}

function Listener({ token, onMessage, connect }: { token: string | undefined; onMessage: () => void; connect: Connect }) {
  useRealtime(token, "StayChanged", onMessage, connect);

  return null;
}

afterEach(cleanup);

describe("useRealtime", () => {
  it("calls back for its message type and after reconnecting", () => {
    const hub = fake();
    const onMessage = vi.fn();
    render(<Listener token="t" onMessage={onMessage} connect={hub.connect} />);

    hub.send("StayChanged");
    hub.send("WorkChanged");
    hub.reconnect();

    expect(hub.connect).toHaveBeenCalledWith("t");
    expect(onMessage).toHaveBeenCalledTimes(2);
  });

  it("does not connect without a token", () => {
    const hub = fake();
    render(<Listener token={undefined} onMessage={vi.fn()} connect={hub.connect} />);

    expect(hub.connect).not.toHaveBeenCalled();
  });

  it("stops only once the connection has started, never while it negotiates", async () => {
    const hub = fake();
    const { unmount } = render(<Listener token="t" onMessage={vi.fn()} connect={hub.connect} />);

    unmount();
    await Promise.resolve();
    expect(hub.connection.stop).not.toHaveBeenCalled();

    hub.started();
    await vi.waitFor(() => expect(hub.connection.stop).toHaveBeenCalledTimes(1));
  });
});
