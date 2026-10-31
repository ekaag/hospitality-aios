import { describe, expect, it, vi } from "vitest";
import { createApiClient } from "./index";

const roster = [{ id: "00000000-0000-4000-8000-000000000300", propertyId: "p", displayName: "Sam Patel", skills: ["housekeeping"], isOnDuty: true, version: 1 }];

function recording(status = 200, body: unknown = roster) {
  const requests: Request[] = [];
  const fetch = vi.fn((input: Request) => {
    requests.push(input);

    return Promise.resolve(new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } }));
  });

  return { requests, fetch: fetch as unknown as typeof globalThis.fetch };
}

describe("createApiClient", () => {
  it("sends the caller's token and reads the typed response", async () => {
    const { requests, fetch } = recording();
    const client = createApiClient({ baseUrl: "http://api.test", token: () => "secret", fetch });

    const { data } = await client.GET("/api/v1/staff", { params: { query: { propertyId: "p" } } });

    expect(requests[0]?.url).toBe("http://api.test/api/v1/staff?propertyId=p");
    expect(requests[0]?.headers.get("Authorization")).toBe("Bearer secret");
    expect(data?.[0]?.displayName).toBe("Sam Patel");
  });

  it("sends no token when there is none", async () => {
    const { requests, fetch } = recording(200, []);
    const client = createApiClient({ baseUrl: "http://api.test", token: () => undefined, fetch });

    await client.GET("/api/dev/tokens/personas");

    expect(requests[0]?.headers.has("Authorization")).toBe(false);
  });

  it("reports a refused token", async () => {
    const { fetch } = recording(401, {});
    const onUnauthorized = vi.fn();
    const client = createApiClient({ baseUrl: "http://api.test", token: () => "expired", onUnauthorized, fetch });

    await client.GET("/api/v1/organizations");

    expect(onUnauthorized).toHaveBeenCalledOnce();
  });
});
