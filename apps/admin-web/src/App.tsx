import { createApiClient, type ApiClient, type Schema } from "@hospitalityos/api-client";
import { DevSignIn, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { useEffect, useMemo, useState } from "react";

const roles = ["Admin", "Manager"] as const;

/**
 * Property configuration and the AI trace viewer (J4). Signed out, it offers the dev personas for its roles.
 */
export function App() {
  const { session, signIn, signOut } = useDevSession("admin-web");
  const client = useMemo(() => createApiClient({ token: () => session?.token, onUnauthorized: signOut }), [session, signOut]);

  return (
    <>
      <header>
        <h1>HospitalityOS · Admin</h1>
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
 * The tenant's properties, read through the typed client to show the sign-in works; configuration and traces arrive
 * with J4.
 */
function Signed({ client }: { client: ApiClient; session: DevSession }) {
  const [properties, setProperties] = useState<Schema<"PropertyResponse">[] | undefined>();
  const [problem, setProblem] = useState<string | undefined>();

  useEffect(() => {
    let current = true;

    client.GET("/api/v1/properties").then(({ data }) => {
      if (current) {
        setProperties(data);
        setProblem(data ? undefined : "The properties could not be read.");
      }
    }, () => current && setProblem("The API is not reachable."));

    return () => {
      current = false;
    };
  }, [client]);

  return (
    <section aria-label="Properties">
      <h2>Properties</h2>
      {problem && <p role="alert">{problem}</p>}
      <ul>
        {properties?.map((property) => (
          <li key={property.id}>{property.name}</li>
        ))}
      </ul>
    </section>
  );
}
