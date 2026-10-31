import { useCallback, useState } from "react";
import { clearSession, loadSession, saveSession, type DevSession } from "./session";

/**
 * The app's session, remembered for the tab: `signIn` keeps a new one, `signOut` forgets it.
 */
export function useDevSession(app: string) {
  const [session, setSession] = useState<DevSession | undefined>(() => loadSession(app));

  const signIn = useCallback(
    (next: DevSession) => {
      saveSession(app, next);
      setSession(next);
    },
    [app],
  );

  const signOut = useCallback(() => {
    clearSession(app);
    setSession(undefined);
  }, [app]);

  return { session, signIn, signOut };
}
