import { describe, expect, it } from "vitest";
import { rememberConversation, rememberedConversation } from "./conversation";

function memory(): Storage {
  const items = new Map<string, string>();

  return {
    get length() {
      return items.size;
    },
    clear: () => items.clear(),
    getItem: (name) => items.get(name) ?? null,
    key: (index) => [...items.keys()][index] ?? null,
    removeItem: (name) => items.delete(name),
    setItem: (name, value) => items.set(name, value),
  };
}

const broken = {
  getItem: () => {
    throw new Error("blocked");
  },
  setItem: () => {
    throw new Error("blocked");
  },
} as unknown as Storage;

describe("conversation", () => {
  it("remembers each guest's conversation", () => {
    const storage = memory();

    rememberConversation("guest-alex", "c1", storage);

    expect(rememberedConversation("guest-alex", storage)).toBe("c1");
    expect(rememberedConversation("guest-sam", storage)).toBeUndefined();
  });

  it("does without storage", () => {
    expect(() => rememberConversation("guest-alex", "c1", broken)).not.toThrow();
    expect(rememberedConversation("guest-alex", broken)).toBeUndefined();
    expect(rememberedConversation("guest-alex", undefined)).toBeUndefined();
  });
});
