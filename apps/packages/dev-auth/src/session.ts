import type { Schema } from "@hospitalityos/api-client";

export type DevPersona = Schema<"DevPersona">;

/**
 * Who the app is signed in as: a dev token and its persona.
 */
export interface DevSession {
  token: string;
  expiresAt: string;
  persona: DevPersona;
}

const key = (app: string) => `hospitalityos.${app}.session`;

/**
 * The app's session in this tab, if it has not expired. Storage can be unavailable (private windows, blocked site data),
 * so reading and writing never throw: without it the app simply asks again.
 */
export function loadSession(app: string, now: Date = new Date(), storage: Storage | undefined = sessionStorageOrNothing()): DevSession | undefined {
  try {
    const text = storage?.getItem(key(app));
    const session = text ? (JSON.parse(text) as DevSession) : undefined;

    return session && new Date(session.expiresAt) > now ? session : undefined;
  } catch {
    return undefined;
  }
}

export function saveSession(app: string, session: DevSession, storage: Storage | undefined = sessionStorageOrNothing()): void {
  try {
    storage?.setItem(key(app), JSON.stringify(session));
  } catch {
    // Not remembered; the app asks again next time.
  }
}

export function clearSession(app: string, storage: Storage | undefined = sessionStorageOrNothing()): void {
  try {
    storage?.removeItem(key(app));
  } catch {
    // Nothing to clear.
  }
}

function sessionStorageOrNothing(): Storage | undefined {
  try {
    return globalThis.sessionStorage;
  } catch {
    return undefined;
  }
}
