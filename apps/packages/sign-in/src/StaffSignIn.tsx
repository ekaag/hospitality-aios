import { createApiClient, type ApiClient, type Schema } from "@hospitalityos/api-client";
import type { DevSession } from "@hospitalityos/dev-auth";
import { useEffect, useState, type FormEvent } from "react";
import type { ProviderToken, SignInProvider } from "./provider";

type Me = Schema<"SignInResponse">;

export interface StaffSignInProps {
  provider: SignInProvider;
  /** The roles this app is for, e.g. `["Worker", "Manager"]`; anyone else is told it is not their app. */
  roles: readonly string[];
  /** Called once the sign-in acts as someone with one of the app's roles. */
  onSignedIn: (session: DevSession) => void;
  /** Calls the API with a token; tests pass their own. */
  clientFor?: (token: string) => ApiClient;
}

type State =
  | { step: "loading" }
  | { step: "signedOut" }
  | { step: "redeem"; token: ProviderToken; problem?: string }
  | { step: "wrongRole"; role: string }
  | { step: "signed"; session: DevSession }
  | { step: "problem"; message: string };

const unreachable = "The API is not reachable.";

const problems: Record<string, string> = {
  "access.invite_not_found": "That code does not open any invite. Check it, or ask your administrator for a new one.",
  "access.invite_expired": "That invite has expired. Ask your administrator for a new one.",
  "access.revoked": "That access has been revoked.",
  "access.already_linked": "This account already has access.",
};

/**
 * Staff sign-in through the identity provider (K4b): signs in, asks the API who the sign-in acts as, and, until an
 * administrator's invite links it, asks for the invite code. The session it hands over has the same shape as a dev
 * session, so the apps treat both alike; when the provider's token expires the API answers 401, the app signs out, and
 * this signs in again silently.
 */
export function StaffSignIn({ provider, roles, onSignedIn, clientFor = defaultClient }: StaffSignInProps) {
  const [state, setState] = useState<State>({ step: "loading" });
  const [code, setCode] = useState("");

  useEffect(() => {
    let current = true;

    async function start() {
      const token = await provider.token();

      if (!token) {
        return { step: "signedOut" } as const;
      }

      return await resolve(token);
    }

    async function resolve(token: ProviderToken): Promise<State> {
      const { data } = await clientFor(token.token).GET("/api/v1/me");

      if (!data) {
        return { step: "problem", message: unreachable };
      }

      return linked(token, data, roles);
    }

    start().then(
      (next) => {
        if (current) {
          setState(next);
        }
      },
      () => {
        if (current) {
          setState({ step: "problem", message: unreachable });
        }
      },
    );

    return () => {
      current = false;
    };
  }, [provider, clientFor, roles]);

  useEffect(() => {
    if (state.step === "signed") {
      onSignedIn(state.session);
    }
  }, [state, onSignedIn]);

  async function redeem(event: FormEvent, token: ProviderToken) {
    event.preventDefault();
    const client = clientFor(token.token);
    const { error, response } = await client.POST("/api/v1/me/invite", { body: { code } });

    if (!response.ok) {
      const problem = (error as { code?: string } | undefined)?.code;
      setState({ step: "redeem", token, problem: (problem && problems[problem]) ?? "Could not redeem that code." });
      return;
    }

    const { data } = await client.GET("/api/v1/me");
    setState(data ? linked(token, data, roles) : { step: "problem", message: unreachable });
  }

  switch (state.step) {
    case "loading":
    case "signed":
      return <p>Signing in…</p>;
    case "signedOut":
      return (
        <section aria-label="Sign in">
          <button type="button" onClick={() => void provider.signIn()}>
            Sign in
          </button>
        </section>
      );
    case "redeem":
      return (
        <section aria-label="Redeem your invite">
          <h2>Enter your invite code</h2>
          <p>Your administrator gave you a code when they invited you. You only need it once.</p>
          {state.problem && <p role="alert">{state.problem}</p>}
          <form onSubmit={(event) => void redeem(event, state.token)}>
            <label>
              Invite code <input value={code} onChange={(event) => setCode(event.target.value)} autoComplete="one-time-code" required />
            </label>{" "}
            <button type="submit">Continue</button>
          </form>
          <button type="button" onClick={() => void provider.signOut()}>
            Use another account
          </button>
        </section>
      );
    case "wrongRole":
      return (
        <section aria-label="Not your app">
          <p role="alert">
            You sign in as {state.role}; this app is for {roles.join(" and ")}.
          </p>
          <button type="button" onClick={() => void provider.signOut()}>
            Sign out
          </button>
        </section>
      );
    case "problem":
      return <p role="alert">{state.message}</p>;
  }
}

function linked(token: ProviderToken, me: Me, roles: readonly string[]): State {
  if (!me.isLinked || !me.role) {
    return { step: "redeem", token };
  }

  if (!roles.includes(me.role)) {
    return { step: "wrongRole", role: me.role };
  }

  return {
    step: "signed",
    session: {
      token: token.token,
      expiresAt: token.expiresAt,
      persona: { id: me.accessId ?? "", displayName: me.displayName ?? "", description: "", role: me.role },
    },
  };
}

function defaultClient(token: string): ApiClient {
  return createApiClient({ token: () => token });
}
