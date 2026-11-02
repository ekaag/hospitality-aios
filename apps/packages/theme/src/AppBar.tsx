import { Avatar } from "./Avatar";
import { BrandMark } from "./BrandMark";

export interface AppBarProps {
  /** Whose app it is, e.g. `Aurora Grand` or `HospitalityOS`. */
  product: string;
  /** Which app, e.g. `Guest`. */
  area: string;
  /** Who is signed in, if anyone. */
  user?: { name: string; role: string } | undefined;
  onSignOut?: () => void;
}

/**
 * The bar across the top of every app (Fluent 2): the brand, the app, and who is signed in with a way to sign out.
 */
export function AppBar({ product, area, user, onSignOut }: AppBarProps) {
  return (
    <header className="app-bar">
      <div className="app-bar-brand">
        <BrandMark />
        <h1>
          {product} <span aria-hidden="true">·</span> <span className="app-bar-area">{area}</span>
        </h1>
      </div>
      {user && (
        <div className="app-bar-user">
          <Avatar name={user.name} />
          <p>
            <span className="app-bar-name">{user.name}</span> <span className="app-bar-role">({user.role})</span>
          </p>
          {onSignOut && (
            <button type="button" className="subtle" onClick={onSignOut}>
              Sign out
            </button>
          )}
        </div>
      )}
    </header>
  );
}
