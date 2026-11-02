import type { ApiClient } from "@hospitalityos/api-client";
import { useRealtime, type Connect } from "@hospitalityos/realtime";
import { Hero } from "@hospitalityos/theme";
import { useCallback, useEffect, useState } from "react";
import { Chat, type Message, type Pending } from "./Chat";
import { rememberConversation, rememberedConversation } from "./conversation";
import { Requests, type GuestRequest } from "./Requests";

export interface StayProps {
  client: ApiClient;
  token: string;
  /** Who the guest is, to carry on with their conversation after a reload. */
  persona: string;
  /** Makes each message's client ID, which makes sending it again safe (P8). */
  newId?: () => string;
  connect?: Connect;
}

/**
 * The guest's stay (J3, spec §33): one conversation with the hotel for questions and requests alike, and the requests
 * still pending. Both are read again whenever the API says the stay changed (a reply, a notification, a request moving
 * on), so nothing is polled; replies arrive asynchronously, once the concierge has handled the message.
 */
export function Stay({ client, token, persona, newId = () => crypto.randomUUID(), connect }: StayProps) {
  const [conversation, setConversation] = useState<string | undefined>();
  const [messages, setMessages] = useState<Message[]>([]);
  const [pending, setPending] = useState<Pending[]>([]);
  const [requests, setRequests] = useState<GuestRequest[]>([]);
  const [problem, setProblem] = useState<string | undefined>();

  // The conversation the guest was in, if it is still open; otherwise a new one.
  useEffect(() => {
    let current = true;

    void (async () => {
      const remembered = rememberedConversation(persona);
      const found = remembered ? await client.GET("/api/v1/conversations/{id}", { params: { path: { id: remembered } } }) : undefined;

      if (found?.data?.status === "Open") {
        if (current) {
          setConversation(found.data.id);
        }

        return;
      }

      const started = await client.POST("/api/v1/conversations");

      if (!current) {
        return;
      }

      if (started.data) {
        rememberConversation(persona, started.data.id);
        setConversation(started.data.id);
      } else {
        setProblem("The chat is not available right now.");
      }
    })();

    return () => {
      current = false;
    };
  }, [client, persona]);

  const load = useCallback(async () => {
    if (!conversation) {
      return;
    }

    const [read, open] = await Promise.all([
      client.GET("/api/v1/conversations/{id}/messages", { params: { path: { id: conversation } } }),
      client.GET("/api/v1/service-requests"),
    ]);

    if (read.data) {
      setMessages(read.data);
    }

    if (open.data) {
      setRequests(open.data);
    }
  }, [client, conversation]);

  useEffect(() => {
    // Loading sets state once the API answers.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  useRealtime(token, "StayChanged", () => void load(), connect);

  async function post(message: Pending) {
    if (!conversation) {
      return;
    }

    setPending((now) => [...now.filter((each) => each.clientMessageId !== message.clientMessageId), { ...message, failed: false }]);
    const sent = await client
      .POST("/api/v1/conversations/{id}/messages", {
        params: { path: { id: conversation } },
        body: { text: message.text, clientMessageId: message.clientMessageId },
      })
      .catch(() => undefined);

    if (sent?.data) {
      const recorded = sent.data;
      setMessages((now) => (now.some((each) => each.id === recorded.id) ? now : [...now, recorded]));
      setPending((now) => now.filter((each) => each.clientMessageId !== message.clientMessageId));
    } else {
      setPending((now) => now.map((each) => (each.clientMessageId === message.clientMessageId ? { ...each, failed: true } : each)));
    }
  }

  const last = messages.at(-1);

  return (
    <div className="stay">
      <Hero title="Good to have you with us" size="compact">
        <p>Ask the concierge anything about the hotel, or tell us what you need.</p>
      </Hero>
      {problem && <p role="alert">{problem}</p>}
      <Chat
        messages={messages}
        pending={pending}
        waiting={pending.length === 0 && last?.sender === "Guest"}
        ready={conversation !== undefined}
        onSend={(text) => post({ clientMessageId: newId(), text })}
        onRetry={post}
      />
      <Requests requests={requests} />
    </div>
  );
}
