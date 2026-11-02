import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { clearSession, DevSignIn, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { AppBar, Welcome } from "@hospitalityos/theme";
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { forgetLink, loadLink, saveLink, takeLinkFromAddress } from "./link/guestLink";
import { LinkSignIn } from "./link/LinkSignIn";
import { Stay } from "./stay/Stay";

const app = "guest-web";
const roles = ["Guest"] as const;

/**
 * The guest's chat with the hotel and the status of their requests (J3). The stay opens from its check-in link (K4c),
 * which this device keeps until the link stops working, so an expired token is renewed without the guest noticing.
 * Without a link it asks for one; local development builds also offer the dev personas.
 */
export function App() {
  // Read first: a newly opened link replaces whatever session the device had.
  const [link, setLink] = useState<string | undefined>(() => {
    const fromAddress = takeLinkFromAddress();

    if (fromAddress) {
      saveLink(fromAddress);
      clearSession(app);
    }

    return fromAddress ?? loadLink();
  });
  const { session, signIn, signOut } = useDevSession(app);
  const client = useMemo(() => createApiClient({ token: () => session?.token, onUnauthorized: signOut }), [session, signOut]);
  const ended = useCallback(() => {
    forgetLink();
    setLink(undefined);
  }, []);

  return (
    <>
      <AppBar
        product="Aurora Grand"
        area="Guest"
        user={session && !link ? { name: session.persona.displayName, role: session.persona.role } : undefined}
        onSignOut={signOut}
      />
      <main>
        {session ? (
          <Stay client={client} token={session.token} persona={session.persona.id} />
        ) : link ? (
          <Welcomed>
            <LinkSignIn client={client} code={link} onSignedIn={signIn} onEnded={ended} />
          </Welcomed>
        ) : (
          <Welcomed>
            <SignedOut client={client} onSignedIn={signIn} />
          </Welcomed>
        )}
      </main>
    </>
  );
}

function Welcomed({ children }: { children: ReactNode }) {
  return (
    <Welcome title="Welcome to the Aurora Grand" subtitle={<p>Your concierge is here day and night: ask anything, or tell us what you need.</p>}>
      {children}
    </Welcome>
  );
}

function SignedOut({ client, onSignedIn }: { client: ApiClient; onSignedIn: (session: DevSession) => void }) {
  return (
    <section aria-label="Open your stay" className="open-stay">
      <h2>Open your stay</h2>
      <p>Scan the QR code from the front desk, or open the link they gave you, to chat with the hotel.</p>
      {import.meta.env.DEV && <DevSignIn client={client} roles={roles} onSignedIn={onSignedIn} />}
    </section>
  );
}
