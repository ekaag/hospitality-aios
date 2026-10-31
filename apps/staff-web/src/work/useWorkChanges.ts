import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";
import { useEffect, useRef } from "react";

/**
 * Calls `onChange` whenever the API says the tenant's work changed (its `WorkChanged` message on `/hubs/realtime`), so
 * the views are read again instead of polled. The message names the work, never what it is: the views are read through
 * the API, which decides what the caller sees. The connection reconnects by itself; while it is down, nothing is heard.
 */
export function useWorkChanges(token: string | undefined, onChange: () => void, connect: (token: string) => HubConnection = hub) {
  const latest = useRef(onChange);

  useEffect(() => {
    latest.current = onChange;
  }, [onChange]);

  useEffect(() => {
    if (!token) {
      return;
    }

    const connection = connect(token);
    connection.on("WorkChanged", () => latest.current());
    // Reconnecting can miss messages, so the views are read again.
    connection.onreconnected(() => latest.current());
    void connection.start().catch(() => undefined);

    return () => {
      void connection.stop();
    };
  }, [token, connect]);
}

function hub(token: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl("/hubs/realtime", { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
