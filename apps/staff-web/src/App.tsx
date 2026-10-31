import { createApiClient, type ApiClient, type Schema } from "@hospitalityos/api-client";
import { DevSignIn, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { useEffect, useMemo, useState } from "react";

const roles = ["Worker", "Manager"] as const;

/**
 * Staff work: assigned human actions and their steps (J2). Signed out, it offers the dev personas for its roles.
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
 * The first property's roster, read through the typed client to show the sign-in works; the work views arrive with J2.
 */
function Signed({ client }: { client: ApiClient; session: DevSession }) {
  const [roster, setRoster] = useState<Schema<"StaffMemberResponse">[] | undefined>();
  const [problem, setProblem] = useState<string | undefined>();

  useEffect(() => {
    let current = true;

    void (async () => {
      const properties = await client.GET("/api/v1/properties");
      const property = properties.data?.[0];
      const staff = property ? await client.GET("/api/v1/staff", { params: { query: { propertyId: property.id } } }) : undefined;

      if (current) {
        setRoster(staff?.data);
        setProblem(staff?.data ? undefined : "The roster could not be read.");
      }
    })();

    return () => {
      current = false;
    };
  }, [client]);

  return (
    <section aria-label="Roster">
      <h2>On the roster</h2>
      {problem && <p role="alert">{problem}</p>}
      <ul>
        {roster?.map((member) => (
          <li key={member.id}>
            {member.displayName} ({member.skills.join(", ")}){member.isOnDuty ? ", on duty" : ""}
          </li>
        ))}
      </ul>
    </section>
  );
}
