import createClient, { type Client, type Middleware } from "openapi-fetch";
import type { components, paths } from "./schema";

export type { components, paths } from "./schema";

/**
 * A schema of the API's contract by name, e.g. `Schema<"StaffMemberResponse">`.
 */
export type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];

/**
 * A failure as the API sends it (RFC 9457); act on `code`, not `title`.
 */
export type Problem = Schema<"Problem">;

export type ApiClient = Client<paths>;

export interface ApiClientOptions {
  /** Where the API is; the apps call it through their dev server's `/api` proxy, so the default is the page's origin. */
  baseUrl?: string;
  /** The caller's bearer token, read for every request; `undefined` sends none. */
  token: () => string | undefined;
  /** Called when the API refuses the token (401), e.g. to sign out. */
  onUnauthorized?: () => void;
  fetch?: typeof globalThis.fetch;
}

/**
 * The typed client: paths, parameters, bodies, and responses all come from the API's OpenAPI document, so a change to
 * the API that the frontends do not follow fails their type check.
 */
export function createApiClient(options: ApiClientOptions): ApiClient {
  const client = createClient<paths>({
    baseUrl: options.baseUrl ?? "",
    ...(options.fetch ? { fetch: options.fetch } : {}),
  });
  client.use(bearer(options));

  return client;
}

function bearer(options: ApiClientOptions): Middleware {
  return {
    onRequest({ request }) {
      const token = options.token();

      if (token) {
        request.headers.set("Authorization", `Bearer ${token}`);
      }

      return request;
    },
    onResponse({ response }) {
      if (response.status === 401) {
        options.onUnauthorized?.();
      }

      return response;
    },
  };
}
