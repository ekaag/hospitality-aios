import { createApiClient, type ApiClient } from "@hospitalityos/api-client";

/**
 * A request the fake API received, with its JSON body if it had one.
 */
export interface Received {
  method: string;
  path: string;
  query: string;
  ifMatch: string | null;
  body: unknown;
}

type Answer = (request: Received) => { status?: number; body?: unknown } | undefined;

/**
 * An API for tests: `answer` replies to each request (404 when it does not), and every request is recorded.
 */
export function fakeApi(answer: Answer): { client: ApiClient; received: Received[] } {
  const received: Received[] = [];
  const fetch = async (request: Request): Promise<Response> => {
    const url = new URL(request.url);
    const text = await request.text();
    const each: Received = {
      method: request.method,
      path: url.pathname,
      query: url.search,
      ifMatch: request.headers.get("If-Match"),
      body: text ? (JSON.parse(text) as unknown) : undefined,
    };
    received.push(each);
    const reply = answer(each) ?? { status: 404, body: { title: "Not found", status: 404 } };

    return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
      status: reply.status ?? 200,
      headers: { "Content-Type": "application/json" },
    });
  };

  return { client: createApiClient({ baseUrl: "http://api.test", token: () => "t", fetch: fetch as typeof globalThis.fetch }), received };
}
