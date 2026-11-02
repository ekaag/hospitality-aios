import { colourOf, initials } from "./names";

/**
 * A person's initials in a coloured circle (Fluent 2 Avatar), for decoration only: their name is always written beside it.
 */
export function Avatar({ name, size = "medium" }: { name: string; size?: "medium" | "large" }) {
  return (
    <span className={`avatar avatar-${size} avatar-${colourOf(name)}`} aria-hidden="true">
      {initials(name)}
    </span>
  );
}
