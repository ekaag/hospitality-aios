import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { DevSignIn, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { useEffect, useMemo, useState } from "react";
import { Board } from "./work/Board";

const roles = ["Worker", "Manager"] as const;

/**
 * Staff work (J2): the four views of the property's human actions and their steps. Signed out, it offers the dev
 * personas for its roles.
 */
export function App() {
  const { session, signIn, signOut } = useDevSession("staff-web");
  const client = useMemo(() => createApiClient({ token: () => session?.token, onUnauthorized: signOut }), [session, signOut]);

  return (
    <>
      <header>
        <h1>HospitalityOS · Staff</h1>
        {session && (
          <p>
            {session.persona.displayName} ({session.persona.role}){" "}
            <button type="button" onClick={signOut}>
              Sign out
            </button>
          </p>
        )}
      </header>
      <main>{session ? <Signed client={client} session={session} /> : <DevSignIn client={client} roles={roles} onSignedIn={signIn} />}</main>
    </>
  );
}

/**
 * The board for the caller's property: the tenant's first, as Phase 1 runs one property per tenant.
 */
function Signed({ client, session }: { client: ApiClient; session: DevSession }) {
  const [propertyId, setPropertyId] = useState<string | undefined>();
  const [problem, setProblem] = useState<string | undefined>();

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

  return propertyId ? (
    <Board client={client} token={session.token} propertyId={propertyId} oversees={session.persona.role !== "Worker"} />
  ) : (
    <p>Loading…</p>
  );
}
