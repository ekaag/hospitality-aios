import { createApiClient, type ApiClient } from "@hospitalityos/api-client";
import { DevSignIn, useDevSession, type DevSession } from "@hospitalityos/dev-auth";
import { entraProvider, readSignInConfig, StaffSignIn } from "@hospitalityos/sign-in";
import { AppBar, Hero, HotelPicker, lookOf, useChosenHotel, useHotelLook, Welcome } from "@hospitalityos/theme";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Admin } from "./admin/Admin";

const roles = ["Admin", "Manager"] as const;

// Staff sign in through Entra External ID when the build names it (VITE_SIGNIN_*), otherwise as the dev personas.
const signInConfig = readSignInConfig(import.meta.env);
const provider = signInConfig && entraProvider(signInConfig);

/**
 * Property configuration and the AI trace viewer (J4). Signed out, it signs in through the identity provider,
 * or offers the dev personas for its roles locally.
 */
export function App() {
  const { session, signIn, signOut } = useDevSession("admin-web");
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
        area="Admin"
        user={session && { name: session.persona.displayName, role: session.persona.role }}
        onSignOut={signOutEverywhere}
      />
      <main>
        {session ? (
          <Signed client={client} session={session} />
        ) : (
          <Welcome title="Welcome back" subtitle={<p>Your hotels' configuration, integrations, and what the AI did and why.</p>}>
            {provider ? <StaffSignIn provider={provider} roles={roles} onSignedIn={signIn} /> : <DevSignIn client={client} roles={roles} onSignedIn={signIn} />}
          </Welcome>
        )}
      </main>
    </>
  );
}

/**
 * The admin surface for one of the tenant's hotels, chosen from the banner (the one last chosen here, else the first by
 * name); the page takes on that hotel's look.
 */
function Signed({ client, session }: { client: ApiClient; session: DevSession }) {
  const [hotels, setHotels] = useState<{ id: string; name: string }[] | undefined>();
  const [problem, setProblem] = useState<string | undefined>();
  const [propertyId, choose] = useChosenHotel("admin-web", hotels);
  const hotel = hotels?.find((each) => each.id === propertyId);
  useHotelLook(lookOf(hotel?.name));

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

  return propertyId && hotel ? (
    <>
      <Hero
        title={hotel.name}
        size="compact"
        look={lookOf(hotel.name)}
        aside={hotels && hotels.length > 1 ? <HotelPicker hotels={hotels} value={propertyId} onChange={choose} /> : undefined}
      >
        <p>Configure the hotel, and see what the AI did and why.</p>
      </Hero>
      <Admin key={propertyId} client={client} propertyId={propertyId} isAdmin={session.persona.role === "Admin"} />
    </>
  ) : (
    <p>Loading…</p>
  );
}
