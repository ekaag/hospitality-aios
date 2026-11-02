import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { DevSignIn, hotelsOf, useDevPersonas, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { entraProvider, readSignInConfig, StaffSignIn } from "@hospitalityos/sign-in";
import { AppBar, Hero, HotelPicker, lookOf, useChosenHotel, useHotelLook, Welcome } from "@hospitalityos/theme";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Guests } from "./guests/Guests";
import { Board } from "./work/Board";

const roles = ["Worker", "Manager"] as const;

// Staff sign in through Entra External ID when the build names it (VITE_SIGNIN_*), otherwise as the dev personas.
const signInConfig = readSignInConfig(import.meta.env);
const provider = signInConfig && entraProvider(signInConfig);

/**
 * Staff work (J2): the four views of a hotel's human actions and their steps. Signed out, it signs in through the
 * identity provider, or offers the dev personas for its roles locally. Every page has a Hotel picker, and the whole app
 * follows the hotel chosen: its work, its guests, and its look.
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
      <AppBar
        product="HospitalityOS"
        area="Staff"
        user={session && { name: session.persona.displayName, role: session.persona.role }}
        onSignOut={signOutEverywhere}
      />
      <main>
        {session ? (
          <Signed client={client} session={session} onLeave={signOut} />
        ) : (
          <SignInWelcome client={client} onSignedIn={signIn} />
        )}
      </main>
    </>
  );
}

/**
 * Before signing in: the chosen hotel's picture and a picker to choose another; locally, the people of that hotel and
 * those who work across hotels.
 */
function SignInWelcome({ client, onSignedIn }: { client: ApiClient; onSignedIn: (session: DevSession) => void }) {
  const { personas } = useDevPersonas(client, roles, !provider);
  const hotels = hotelsOf(personas);
  const [chosen, choose] = useChosenHotel("staff-web", hotels);
  const name = hotels.find((hotel) => hotel.id === chosen)?.name;
  const look = lookOf(name);
  useHotelLook(look);

  return (
    <Welcome
      title={name ?? "Welcome back"}
      subtitle={<p>Your hotels' work, live: what to do next, what is waiting, and what is done.</p>}
      look={look}
      aside={chosen && hotels.length > 1 ? <HotelPicker hotels={hotels} value={chosen} onChange={choose} /> : undefined}
    >
      {provider ? <StaffSignIn provider={provider} roles={roles} onSignedIn={onSignedIn} /> : <DevSignIn client={client} roles={roles} hotel={chosen} onSignedIn={onSignedIn} />}
    </Welcome>
  );
}

/**
 * The board for one of the tenant's hotels, and for managers the guests' check-in links, chosen from the banner (the
 * one last chosen here, else the first by name); the page takes on that hotel's look. A worker belongs to one hotel and
 * starts there; choosing another signs them out, to sign in as someone who works there.
 */
function Signed({ client, session, onLeave }: { client: ApiClient; session: DevSession; onLeave: () => void }) {
  const [hotels, setHotels] = useState<{ id: string; name: string }[] | undefined>();
  const [problem, setProblem] = useState<string | undefined>();
  const [area, setArea] = useState<"work" | "guests">("work");
  const [propertyId, choose] = useChosenHotel("staff-web", hotels);
  const hotel = hotels?.find((each) => each.id === propertyId);
  const home = session.persona.propertyId ?? undefined;
  useHotelLook(lookOf(hotel?.name));

  // Someone who belongs to one hotel starts at it.
  useEffect(() => {
    if (home && hotels?.some((each) => each.id === home)) {
      choose(home);
    }
  }, [home, hotels, choose]);

  const pick = (id: string) => {
    choose(id);

    if (home && id !== home) {
      onLeave();
    }
  };

  useEffect(() => {
    let current = true;

    client.GET("/api/v1/properties").then(
      ({ data }) => {
        if (current) {
          setHotels(data?.map((property) => ({ id: property.id, name: property.name })));
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

  if (!propertyId || !hotel) {
    return <p>Loading…</p>;
  }

  const oversees = session.persona.role !== "Worker";

  return (
    <>
      <Hero
        title={hotel.name}
        size="compact"
        look={lookOf(hotel.name)}
        aside={hotels && hotels.length > 1 ? <HotelPicker hotels={hotels} value={propertyId} onChange={pick} /> : undefined}
      >
        <p>{greeting(session.persona.displayName)}</p>
      </Hero>
      {oversees && (
        <nav aria-label="Areas" className="areas">
          <button type="button" aria-pressed={area === "work"} onClick={() => setArea("work")}>
            Work
          </button>
          <button type="button" aria-pressed={area === "guests"} onClick={() => setArea("guests")}>
            Guests
          </button>
        </nav>
      )}
      {oversees && area === "guests" ? (
        <Guests key={propertyId} client={client} propertyId={propertyId} />
      ) : (
        <Board key={propertyId} client={client} token={session.token} propertyId={propertyId} oversees={oversees} />
      )}
    </>
  );
}

function greeting(name: string): string {
  const hour = new Date().getHours();
  const part = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";

  return `${part}, ${name.split(" ")[0] ?? name}.`;
}
