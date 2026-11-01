import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { DevSignIn, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { entraProvider, readSignInConfig, StaffSignIn } from "@hospitalityos/sign-in";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Guests } from "./guests/Guests";
import { Board } from "./work/Board";

const roles = ["Worker", "Manager"] as const;

// Staff sign in through Entra External ID when the build names it (VITE_SIGNIN_*), otherwise as the dev personas.
const signInConfig = readSignInConfig(import.meta.env);
const provider = signInConfig && entraProvider(signInConfig);

/**
 * Staff work (J2): the four views of the property's human actions and their steps. Signed out, it signs in through
 * the identity provider, or offers the dev personas for its roles locally.
 */
export function App() {
  const { session, signIn, signOut } = useDevSession("staff-web");
  // A refused token signs out here only, so the provider's sign-in picks up again silently.
  const client = useMemo(() => createApiClient({ token: () => session?.token, onUnauthorized: signOut }), [session, signOut]);
  const signOutEverywhere = useCallback(() => {
    signOut();
    void provider?.signOut();
  }, [signOut]);

  return (
    <>
      <header>
        <h1>HospitalityOS · Staff</h1>
        {session && (
          <p>
            {session.persona.displayName} ({session.persona.role}){" "}
            <button type="button" onClick={signOutEverywhere}>
              Sign out
            </button>
          </p>
        )}
      </header>
      <main>{session ? <Signed client={client} session={session} /> : provider ? (
          <StaffSignIn provider={provider} roles={roles} onSignedIn={signIn} />
        ) : (
          <DevSignIn client={client} roles={roles} onSignedIn={signIn} />
        )}</main>
    </>
  );
}

/**
 * The board for the caller's property (the tenant's first, as Phase 1 runs one property per tenant), and for managers the
 * guests' check-in links.
 */
function Signed({ client, session }: { client: ApiClient; session: DevSession }) {
  const [propertyId, setPropertyId] = useState<string | undefined>();
  const [problem, setProblem] = useState<string | undefined>();
  const [area, setArea] = useState<"work" | "guests">("work");

  useEffect(() => {
    let current = true;

    client.GET("/api/v1/properties").then(
      ({ data }) => {
        if (current) {
          setPropertyId(data?.[0]?.id);
          setProblem(data?.[0] ? undefined : "There is no property to show.");
        }
      },
      () => {
        if (current) {
          setProblem("The API is not reachable.");
        }
      },
    );

    return () => {
      current = false;
    };
  }, [client]);

  if (problem) {
    return <p role="alert">{problem}</p>;
  }

  if (!propertyId) {
    return <p>Loading…</p>;
  }

  const oversees = session.persona.role !== "Worker";

  return (
    <>
      {oversees && (
        <nav aria-label="Areas">
          <button type="button" aria-pressed={area === "work"} onClick={() => setArea("work")}>
            Work
          </button>{" "}
          <button type="button" aria-pressed={area === "guests"} onClick={() => setArea("guests")}>
            Guests
          </button>
        </nav>
      )}
      {oversees && area === "guests" ? (
        <Guests client={client} propertyId={propertyId} />
      ) : (
        <Board client={client} token={session.token} propertyId={propertyId} oversees={oversees} />
      )}
    </>
  );
}
