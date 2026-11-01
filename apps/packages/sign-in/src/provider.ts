import { InteractionRequiredAuthError, PublicClientApplication, type AccountInfo } from "@azure/msal-browser";

/**
 * Where staff sign in (Microsoft Entra External ID): the tenant's authority, the app's client ID, and the API scope its
 * tokens are for. Deployments set them as `VITE_SIGNIN_*` build variables; without them the app uses the dev tokens.
 */
export interface SignInConfig {
  authority: string;
  clientId: string;
  scope: string;
}

/**
 * A signed-in user's access token for the API, and when it stops working.
 */
export interface ProviderToken {
  token: string;
  expiresAt: string;
}

/**
 * The identity provider as the sign-in needs it, so tests can stand in for it.
 */
export interface SignInProvider {
  /** The signed-in user's token, finishing a sign-in redirect first; `undefined` when nobody is signed in. */
  token(): Promise<ProviderToken | undefined>;
  /** Leaves for the provider's sign-in page. */
  signIn(): Promise<void>;
  /** Signs out of the provider too, so the next visit asks again. */
  signOut(): Promise<void>;
}

/**
 * The sign-in configuration from the build's environment, or `undefined` when any part is missing.
 */
export function readSignInConfig(env: Record<string, string | undefined>): SignInConfig | undefined {
  const authority = env.VITE_SIGNIN_AUTHORITY;
  const clientId = env.VITE_SIGNIN_CLIENT_ID;
  const scope = env.VITE_SIGNIN_SCOPE;

  return authority && clientId && scope ? { authority, clientId, scope } : undefined;
}

/**
 * Entra External ID through MSAL: redirect sign-in (authorization code with PKCE), tokens cached for the tab.
 */
export function entraProvider(config: SignInConfig): SignInProvider {
  const msal = new PublicClientApplication({
    auth: { clientId: config.clientId, authority: config.authority, redirectUri: globalThis.location.origin },
    cache: { cacheLocation: "sessionStorage" },
  });
  const ready = msal.initialize().then(() => msal.handleRedirectPromise());
  const scopes = [config.scope];

  async function account(): Promise<AccountInfo | undefined> {
    const redirected = await ready;
    return redirected?.account ?? msal.getAllAccounts()[0];
  }

  return {
    async token() {
      const signedIn = await account();

      if (!signedIn) {
        return undefined;
      }

      try {
        const result = await msal.acquireTokenSilent({ scopes, account: signedIn });
        return { token: result.accessToken, expiresAt: (result.expiresOn ?? new Date(Date.now() + 5 * 60_000)).toISOString() };
      } catch (error) {
        if (error instanceof InteractionRequiredAuthError) {
          return undefined;
        }

        throw error;
      }
    },
    async signIn() {
      await ready;
      await msal.loginRedirect({ scopes });
    },
    async signOut() {
      await ready;
      await msal.logoutRedirect({ account: (await account()) ?? null });
    },
  };
}
