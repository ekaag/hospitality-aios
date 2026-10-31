import { createApiClient } from "@hospitalityos/api-client";
import { DevSignIn, useDevSession } from "@hospitalityos/dev-auth";
import { useMemo } from "react";
import { Stay } from "./stay/Stay";

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
      <main>
        {session ? (
          <Stay client={client} token={session.token} persona={session.persona.id} />
        ) : (
          <DevSignIn client={client} roles={roles} onSignedIn={signIn} />
        )}
      </main>
    </>
  );
}
