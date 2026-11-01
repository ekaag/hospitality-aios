import type { ApiClient } from "@hospitalityos/api-client";
import type { DevSession } from "@hospitalityos/dev-auth";

const key = "hospitalityos.guest-web.link";

/**
 * The check-in link's code from the address (`#link=…`), removed from it so it is not left in the address bar or
 * shared by accident. The fragment is never sent to a server.
 */
export function takeLinkFromAddress(location: Location = globalThis.location, history: History = globalThis.history): string | undefined {
  const code = new URLSearchParams(location.hash.replace(/^#/, "")).get("link") ?? undefined;

  if (code) {
    history.replaceState(null, "", location.pathname + location.search);
  }

  return code || undefined;
}

/**
 * The code this device opened the stay with, kept so the app can open a session again when its token expires. Storage
 * can be unavailable (private windows, blocked site data): then the guest scans the link again.
 */
export function loadLink(storage: Storage | undefined = localStorageOrNothing()): string | undefined {
  try {
    return storage?.getItem(key) ?? undefined;
  } catch {
    return undefined;
  }
}

export function saveLink(code: string, storage: Storage | undefined = localStorageOrNothing()): void {
  try {
    storage?.setItem(key, code);
  } catch {
    // Not remembered; the guest scans the link again next time.
  }
}

export function forgetLink(storage: Storage | undefined = localStorageOrNothing()): void {
  try {
    storage?.removeItem(key);
  } catch {
    // Nothing to forget.
  }
}

/**
 * Why a link opened no session: it no longer works (replaced, revoked, or the stay ended), or the API could not be reached.
 */
export type LinkProblem = "ended" | "unreachable";

/**
 * Opens a session from a link's code: a stay token, as a session the app treats like any other. The session is named by
 * its stay, so a remembered conversation belongs to the stay.
 */
export async function openSession(client: ApiClient, code: string): Promise<DevSession | LinkProblem> {
  try {
    const { data, response } = await client.POST("/api/v1/guest-sessions", { body: { code } });

    if (!data) {
      return response.status === 404 || response.status === 422 ? "ended" : "unreachable";
    }

    return { token: data.accessToken, expiresAt: data.expiresAt, persona: { id: `stay-${stayOf(data.accessToken)}`, displayName: "", description: "", role: "Guest" } };
  } catch {
    return "unreachable";
  }
}

function stayOf(token: string): string {
  try {
    const payload = token.split(".")[1] ?? "";
    const claims = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/"))) as { stay_id?: string };
    return claims.stay_id ?? "unknown";
  } catch {
    return "unknown";
  }
}

function localStorageOrNothing(): Storage | undefined {
  try {
    return globalThis.localStorage;
  } catch {
    return undefined;
  }
}
