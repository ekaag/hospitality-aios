import type { ApiClient } from "@hospitalityos/api-client";
import { useEffect, useState } from "react";
import type { DevPersona, DevSession } from "./session";

export interface DevSignInProps {
  client: ApiClient;
  /** The roles this app is for, e.g. `["Guest"]`; other personas are not offered. */
  roles: readonly string[];
  onSignedIn: (session: DevSession) => void;
}

const unavailable = "The API is not reachable, or its dev tokens are off. Start it with the HospitalityOS.Api launch profile.";

/**
 * Signs in as one of the seeded personas through the API's dev tokens (Development only). When the API is not running,
 * or dev tokens are off, it says so instead of offering anyone.
 */
export function DevSignIn({ client, roles, onSignedIn }: DevSignInProps) {
  const [personas, setPersonas] = useState<DevPersona[] | undefined>();
  const [problem, setProblem] = useState<string | undefined>();

  useEffect(() => {
    let current = true;

    client.GET("/api/dev/tokens/personas").then(
      ({ data }) => {
        if (current) {
          setPersonas(data?.filter((persona) => roles.includes(persona.role)));
          setProblem(data ? undefined : unavailable);
        }
      },
      () => {
        if (current) {
          setProblem(unavailable);
        }
      },
    );

    return () => {
      current = false;
    };
  }, [client, roles]);

  async function signIn(persona: DevPersona) {
    const { data } = await client.POST("/api/dev/tokens", { body: { persona: persona.id } });

    if (data) {
      onSignedIn({ token: data.accessToken, expiresAt: data.expiresAt, persona: data.persona });
    } else {
      setProblem(`Could not sign in as ${persona.displayName}.`);
    }
  }

  return (
    <section aria-label="Sign in">
      <h2>Sign in as</h2>
      {problem && <p role="alert">{problem}</p>}
      {personas && (
        <ul>
          {personas.map((persona) => (
            <li key={persona.id}>
              <button type="button" onClick={() => void signIn(persona)}>
                {persona.displayName}
              </button>{" "}
              <span>
                {persona.role}: {persona.description}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
