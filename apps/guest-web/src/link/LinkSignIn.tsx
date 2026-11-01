import type { ApiClient } from "@hospitalityos/api-client";
import type { DevSession } from "@hospitalityos/dev-auth";
import { useEffect, useState } from "react";
import { openSession, type LinkProblem } from "./guestLink";

export interface LinkSignInProps {
  client: ApiClient;
  code: string;
  onSignedIn: (session: DevSession) => void;
  /** Called when the link no longer opens the stay, so the app forgets it. */
  onEnded: () => void;
}

const messages: Record<LinkProblem, string> = {
  ended: "This link no longer works. Ask the front desk for a new one.",
  unreachable: "We could not reach the hotel. Check your connection and try again.",
};

/**
 * Opens the stay from its check-in link (K4c), quietly: the guest sees only a moment's wait, or why it did not work.
 */
export function LinkSignIn({ client, code, onSignedIn, onEnded }: LinkSignInProps) {
  const [problem, setProblem] = useState<LinkProblem | undefined>();
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;

    void openSession(client, code).then((opened) => {
      if (!current) {
        return;
      }

      if (typeof opened === "string") {
        setProblem(opened);

        if (opened === "ended") {
          onEnded();
        }
      } else {
        onSignedIn(opened);
      }
    });

    return () => {
      current = false;
    };
  }, [client, code, attempt, onSignedIn, onEnded]);

  if (!problem) {
    return <p>Opening your stay…</p>;
  }

  return (
    <section aria-label="Your stay">
      <p role="alert">{messages[problem]}</p>
      {problem === "unreachable" && (
        <button
          type="button"
          onClick={() => {
            setProblem(undefined);
            setAttempt((count) => count + 1);
          }}
        >
          Try again
        </button>
      )}
    </section>
  );
}
