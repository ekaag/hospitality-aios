import { createApiClient, type ApiClient, type Schema } from "@hospitalityos/api-client";
import { clearSession, DevSignIn, hotelsOf, issueSession, useDevPersonas, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { AppBar, HotelPicker, lookOf, useChosenHotel, useHotelLook, Welcome } from "@hospitalityos/theme";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { forgetLink, loadLink, saveLink, takeLinkFromAddress } from "./link/guestLink";
import { LinkSignIn } from "./link/LinkSignIn";
import { Stay } from "./stay/Stay";

const app = "guest-web";
const roles = ["Guest"] as const;

type GuestStay = Schema<"GuestStayResponse">;

/**
 * The guest's chat with the hotel and the status of their requests (J3). The stay opens from its check-in link (K4c),
 * which this device keeps until the link stops working, so an expired token is renewed without the guest noticing.
 * Without a link it asks for one; local development builds also offer the dev personas, a guest at each reference
 * hotel, and a Hotel picker that switches between them. The whole app follows the stay's hotel: its name and its look.
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
  const [hotel, setHotel] = useState<string | undefined>();
  const ended = useCallback(() => {
    forgetLink();
    setLink(undefined);
  }, []);

  return (
    <>
      <AppBar
        product={hotel ?? "HospitalityOS"}
        area="Guest"
        user={session && !link ? { name: session.persona.displayName, role: session.persona.role } : undefined}
        onSignOut={signOut}
      />
      <main>
        {session ? (
          <SignedIn client={client} session={session} canSwitch={import.meta.env.DEV && !link} onSignedIn={signIn} onHotel={setHotel} />
        ) : link ? (
          <Welcome title="Welcome" subtitle={<p>Your concierge is here day and night: ask anything, or tell us what you need.</p>}>
            <LinkSignIn client={client} code={link} onSignedIn={signIn} onEnded={ended} />
          </Welcome>
        ) : (
          <SignedOut client={client} onSignedIn={signIn} onHotel={setHotel} />
        )}
      </main>
    </>
  );
}

/**
 * The stay, dressed in its hotel's look; in local builds, with a picker that switches to another hotel's guest.
 */
function SignedIn({
  client,
  session,
  canSwitch,
  onSignedIn,
  onHotel,
}: {
  client: ApiClient;
  session: DevSession;
  canSwitch: boolean;
  onSignedIn: (session: DevSession) => void;
  onHotel: (name: string | undefined) => void;
}) {
  const [stay, setStay] = useState<GuestStay | undefined>();
  const look = lookOf(stay?.propertyName);
  useHotelLook(look);

  useEffect(() => {
    let current = true;

    void client.GET("/api/v1/stay").then(({ data }) => {
      if (current) {
        setStay(data);
      }
    });

    return () => {
      current = false;
    };
  }, [client]);

  useEffect(() => onHotel(stay?.propertyName), [stay, onHotel]);

  return (
    <Stay
      key={session.persona.id}
      client={client}
      token={session.token}
      persona={session.persona.id}
      hotel={stay?.propertyName}
      room={stay?.unitNumber ?? undefined}
      look={look}
      aside={canSwitch && stay ? <SwitchGuest client={client} current={stay.propertyId} onSignedIn={onSignedIn} /> : undefined}
    />
  );
}

/**
 * Local builds: picking another hotel signs in as its guest, so the chat, requests, and look are that hotel's.
 */
function SwitchGuest({ client, current, onSignedIn }: { client: ApiClient; current: string; onSignedIn: (session: DevSession) => void }) {
  const { personas } = useDevPersonas(client, roles);
  const hotels = hotelsOf(personas);
  const [, choose] = useChosenHotel(app, hotels);

  if (hotels.length < 2) {
    return null;
  }

  async function switchTo(id: string) {
    choose(id);
    const guest = personas?.find((persona) => persona.propertyId === id);
    const session = guest && (await issueSession(client, guest));

    if (session) {
      onSignedIn(session);
    }
  }

  return <HotelPicker hotels={hotels} value={current} onChange={(id) => void switchTo(id)} />;
}

function SignedOut({ client, onSignedIn, onHotel }: { client: ApiClient; onSignedIn: (session: DevSession) => void; onHotel: (name: string | undefined) => void }) {
  const { personas } = useDevPersonas(client, roles, import.meta.env.DEV);
  const hotels = hotelsOf(personas);
  const [chosen, choose] = useChosenHotel(app, hotels);
  const name = hotels.find((hotel) => hotel.id === chosen)?.name;
  const look = lookOf(name);
  useHotelLook(look);

  useEffect(() => onHotel(name), [name, onHotel]);

  return (
    <Welcomed
      name={name}
      look={look}
      aside={chosen && hotels.length > 1 ? <HotelPicker hotels={hotels} value={chosen} onChange={choose} /> : undefined}
    >
      <section aria-label="Open your stay" className="open-stay">
        <h2>Open your stay</h2>
        <p>Scan the QR code from the front desk, or open the link they gave you, to chat with the hotel.</p>
        {import.meta.env.DEV && <DevSignIn client={client} roles={roles} hotel={chosen} onSignedIn={onSignedIn} />}
      </section>
    </Welcomed>
  );
}

function Welcomed({ name, look, aside, children }: { name: string | undefined; look: ReturnType<typeof lookOf>; aside: ReactNode; children: ReactNode }) {
  return (
    <Welcome
      title={name ? `Welcome to the ${name}` : "Welcome"}
      subtitle={<p>Your concierge is here day and night: ask anything, or tell us what you need.</p>}
      look={look}
      aside={aside}
    >
      {children}
    </Welcome>
  );
}
