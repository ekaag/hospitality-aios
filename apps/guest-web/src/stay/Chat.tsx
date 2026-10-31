import type { Schema } from "@hospitalityos/api-client";
import { useState, type FormEvent, type KeyboardEvent } from "react";

export type Message = Schema<"GuestMessageResponse">;

/**
 * A message the guest sent that the API has not recorded yet; `failed` once sending it did not work.
 */
export interface Pending {
  clientMessageId: string;
  text: string;
  failed?: boolean;
}

export interface ChatProps {
  messages: Message[];
  pending: Pending[];
  /** Whether a reply is on its way: the guest has the last word. */
  waiting: boolean;
  onSend: (text: string) => Promise<void>;
  onRetry: (message: Pending) => Promise<void>;
}

/**
 * What a guest can ask, offered while the conversation is empty (spec §33): one surface for questions and requests.
 */
const suggestions = [
  "What time does the pool close?",
  "Could I get two towels please?",
  "Anything on tonight?",
  "The air con is not working",
] as const;

const senders: Record<Schema<"MessageSender">, string> = {
  Guest: "You",
  Concierge: "Concierge",
  Staff: "Front desk",
  System: "Update",
};

/**
 * The guest's conversation with the hotel: their messages and the hotel's, newest last, and a box to write in. The guest
 * never needs to know which department answers (spec §33).
 */
export function Chat({ messages, pending, waiting, onSend, onRetry }: ChatProps) {
  const [text, setText] = useState("");
  const [sending, setSending] = useState(false);

  async function send(said: string) {
    const trimmed = said.trim();

    if (!trimmed || sending) {
      return;
    }

    setSending(true);
    setText("");

    try {
      await onSend(trimmed);
    } finally {
      setSending(false);
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    void send(text);
  }

  // Enter sends; Shift+Enter starts a new line.
  function keyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      void send(text);
    }
  }

  return (
    <section aria-label="Chat" className="chat">
      {messages.length === 0 && pending.length === 0 && (
        <div className="suggestions">
          <p>Ask about the hotel, or tell us what you need.</p>
          {suggestions.map((suggestion) => (
            <button key={suggestion} type="button" onClick={() => void send(suggestion)}>
              {suggestion}
            </button>
          ))}
        </div>
      )}
      <ol aria-label="Messages" className="messages">
        {messages.map((message) => (
          <li key={message.id} className={message.sender === "Guest" ? "mine" : "theirs"} data-sender={message.sender}>
            <span className="from">{senders[message.sender]}</span>
            <p>{message.text}</p>
            <time dateTime={message.sentAt}>{new Date(message.sentAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</time>
          </li>
        ))}
        {pending.map((message) => (
          <li key={message.clientMessageId} className="mine pending" data-sender="Guest">
            <span className="from">You</span>
            <p>{message.text}</p>
            {message.failed ? (
              <span role="alert">
                Not sent.{" "}
                <button type="button" onClick={() => void onRetry(message)}>
                  Try again
                </button>
              </span>
            ) : (
              <span>Sending…</span>
            )}
          </li>
        ))}
      </ol>
      {waiting && (
        <p role="status" className="waiting">
          The concierge is replying…
        </p>
      )}
      <form onSubmit={submit} className="composer">
        <label htmlFor="message">Message</label>
        <textarea id="message" rows={2} value={text} maxLength={4000} onChange={(event) => setText(event.target.value)} onKeyDown={keyDown} />
        <button type="submit" disabled={!text.trim() || sending}>
          Send
        </button>
      </form>
    </section>
  );
}
