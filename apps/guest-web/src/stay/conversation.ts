const key = (persona: string) => `hospitalityos.guest-web.conversation.${persona}`;

/**
 * The conversation this guest was last in, so a reload carries on with it. Storage can be unavailable (private windows,
 * blocked site data), so reading and writing never throw: without it a reload starts a new conversation.
 */
export function rememberedConversation(persona: string, storage: Storage | undefined = localStorageOrNothing()): string | undefined {
  try {
    return storage?.getItem(key(persona)) ?? undefined;
  } catch {
    return undefined;
  }
}

export function rememberConversation(persona: string, id: string, storage: Storage | undefined = localStorageOrNothing()): void {
  try {
    storage?.setItem(key(persona), id);
  } catch {
    // Not remembered; a reload starts a new conversation.
  }
}

function localStorageOrNothing(): Storage | undefined {
  try {
    return globalThis.localStorage;
  } catch {
    return undefined;
  }
}
