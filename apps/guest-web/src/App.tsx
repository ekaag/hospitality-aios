import { createApiClient } from "@hospitalityos/api-client";
import { DevSignIn, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { useMemo } from "react";

const roles = ["Guest"] as const;

/**
 * The guest's chat with the hotel and the status of their requests (J3). Signed out, it offers the dev personas for its roles.
 */
export function App() {
  const { session, signIn, signOut } = useDevSession("guest-web");
  const client = useMemo(() => createApiClient({ token: () => session?.token, onUnauthorized: signOut }), [session, signOut]);

  return (
    <>
      <header>
        <h1>Aurora Grand · Guest</h1>
        {session && (
          <p>
            {session.persona.displayName} ({session.persona.role}){" "}
            <button type="button" onClick={signOut}>
              Sign out
            </button>
          </p>
        )}
      </header>
      <main>{session ? <Signed session={session} /> : <DevSignIn client={client} roles={roles} onSignedIn={signIn} />}</main>
    </>
  );
}

/**
 * What the guest app will show once signed in; the chat arrives with J3.
 */
function Signed({ session }: { session: DevSession }) {
  return <p>Welcome, {session.persona.displayName}. The chat with the hotel arrives with J3.</p>;
}
