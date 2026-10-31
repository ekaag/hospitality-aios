import { describe, expect, it } from "vitest";
import { clearSession, loadSession, saveSession, type DevSession } from "./session";

function memory(): Storage {
  const items = new Map<string, string>();

  return {
    get length() {
      return items.size;
    },
    clear: () => items.clear(),
    getItem: (key) => items.get(key) ?? null,
    key: (index) => [...items.keys()][index] ?? null,
    removeItem: (key) => void items.delete(key),
    setItem: (key, value) => void items.set(key, value),
  };
}

const session: DevSession = {
  token: "token",
  expiresAt: "2026-10-30T20:00:00Z",
  persona: { id: "worker-sam", displayName: "Sam Patel", description: "Housekeeping, on duty.", role: "Worker" },
};

describe("sessions", () => {
  it("are remembered per app until they expire", () => {
    const storage = memory();
    saveSession("staff-web", session, storage);

    expect(loadSession("staff-web", new Date("2026-10-30T19:59:00Z"), storage)).toEqual(session);
    expect(loadSession("admin-web", new Date("2026-10-30T19:59:00Z"), storage)).toBeUndefined();
    expect(loadSession("staff-web", new Date("2026-10-30T20:00:00Z"), storage)).toBeUndefined();
  });

  it("are forgotten on sign-out", () => {
    const storage = memory();
    saveSession("staff-web", session, storage);
    clearSession("staff-web", storage);

    expect(loadSession("staff-web", new Date("2026-10-30T19:00:00Z"), storage)).toBeUndefined();
  });

  it("survive storage that refuses access", () => {
    const blocked = () => {
      throw new Error("blocked");
    };
    const refusing: Storage = { ...memory(), getItem: blocked, setItem: blocked, removeItem: blocked };

    expect(() => saveSession("staff-web", session, refusing)).not.toThrow();
    expect(() => clearSession("staff-web", refusing)).not.toThrow();
    expect(loadSession("staff-web", new Date("2026-10-30T19:00:00Z"), refusing)).toBeUndefined();
  });
});
