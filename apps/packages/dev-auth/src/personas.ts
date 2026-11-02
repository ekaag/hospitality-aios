import type { ApiClient } from "@hospitalityos/api-client";
import { useEffect, useState } from "react";
import type { DevPersona, DevSession } from "./session";

const unavailable = "The API is not reachable, or its dev tokens are off. Start it with the HospitalityOS.Api launch profile.";

/**
 * The seed's personas for the app's roles, or why there are none (the API is not running, or dev tokens are off). Not
 * asked for when `enabled` is false, as in builds that sign in for real.
 */
export function useDevPersonas(client: ApiClient, roles: readonly string[], enabled = true): { personas?: DevPersona[]; problem?: string } {
  const [state, setState] = useState<{ personas?: DevPersona[]; problem?: string }>({});

  useEffect(() => {
    if (!enabled) {
      return;
    }

    let current = true;

    client.GET("/api/dev/tokens/personas").then(
      ({ data }) => {
        if (current) {
          setState(data ? { personas: data.filter((persona) => roles.includes(persona.role)) } : { problem: unavailable });
        }
      },
      () => {
        if (current) {
          setState({ problem: unavailable });
        }
      },
    );

    return () => {
      current = false;
    };
  }, [client, roles, enabled]);

  return state;
}

/**
 * The hotels the personas belong to, once each; personas who work across hotels belong to none.
 */
export function hotelsOf(personas: readonly DevPersona[] | undefined): { id: string; name: string }[] {
  const hotels = new Map<string, string>();

  for (const persona of personas ?? []) {
    if (persona.propertyId && persona.propertyName) {
      hotels.set(persona.propertyId, persona.propertyName);
    }
  }

  return [...hotels].map(([id, name]) => ({ id, name }));
}

/**
 * Whether the persona can be signed in as at the hotel: they belong to it, or work across hotels.
 */
export function worksAt(persona: DevPersona, hotel: string | undefined): boolean {
  return !hotel || !persona.propertyId || persona.propertyId === hotel;
}

/**
 * Has a dev token issued for the persona.
 */
export async function issueSession(client: ApiClient, persona: DevPersona): Promise<DevSession | undefined> {
  const { data } = await client.POST("/api/dev/tokens", { body: { persona: persona.id } });

  return data ? { token: data.accessToken, expiresAt: data.expiresAt, persona: data.persona } : undefined;
}
