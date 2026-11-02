import type { ReactNode } from "react";
import { pictures, type HotelLook } from "./hotels";

export interface HeroProps {
  title: string;
  children?: ReactNode;
  /** `tall` to welcome, `compact` as a banner over the work. */
  size?: "tall" | "compact";
  /** Whose picture: the Aurora Grand's unless another hotel is shown. */
  look?: HotelLook;
  /** Shown at the banner's top right, such as the hotel picker. */
  aside?: ReactNode;
}

/**
 * The hotel's picture (the Aurora Grand under the northern lights, the Mysore Grand illuminated), with a title over it.
 */
export function Hero({ title, children, size = "tall", look = "aurora-grand", aside }: HeroProps) {
  return (
    <div className={`hero hero-${size}`} data-look={look}>
      <img src={pictures[look]} alt="" className="hero-image" />
      <div className="hero-text">
        <p className="hero-title">{title}</p>
        {children && <div className="hero-subtitle">{children}</div>}
      </div>
      {aside && <div className="hero-aside">{aside}</div>}
    </div>
  );
}

/**
 * The page before signing in: the hotel, and beside it a panel to sign in or open the stay.
 */
export function Welcome({ title, subtitle, children }: { title: string; subtitle: ReactNode; children: ReactNode }) {
  return (
    <div className="welcome">
      <Hero title={title}>{subtitle}</Hero>
      <div className="welcome-panel card">{children}</div>
    </div>
  );
}
