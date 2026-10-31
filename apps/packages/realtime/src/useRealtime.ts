import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";
import { useEffect, useRef } from "react";

/**
 * Makes the connection for a token; tests pass their own.
 */
export type Connect = (token: string) => HubConnection;

/**
 * Calls `onMessage` whenever the API sends a message of `type` on `/hubs/realtime` (`WorkChanged` for staff,
 * `StayChanged` for guests), and again after reconnecting, since messages may have been missed meanwhile. Messages say
 * what changed, never what it is: the caller reads it again through the API, which decides what it may see. The
 * connection reconnects by itself; while it is down, nothing is heard.
 */
export function useRealtime(token: string | undefined, type: string, onMessage: () => void, connect: Connect = hub) {
  const latest = useRef(onMessage);

  useEffect(() => {
    latest.current = onMessage;
  }, [onMessage]);

  useEffect(() => {
    if (!token) {
      return;
    }

    const connection = connect(token);
    connection.on(type, () => latest.current());
    connection.onreconnected(() => latest.current());
    const started = connection.start().catch(() => undefined);

    // Stopping while the connection negotiates fails it noisily (React mounts twice in development), so it stops once started.
    return () => {
      void started.then(() => connection.stop());
    };
  }, [token, type, connect]);
}

function hub(token: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl("/hubs/realtime", { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
