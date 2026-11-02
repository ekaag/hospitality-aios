/// <reference types="vite/client" />
import type { ReactNode } from "react";
import hotel from "./aurora-grand.svg";

export interface HeroProps {
  title: string;
  children?: ReactNode;
  /** `tall` to welcome, `compact` as a banner over the work. */
  size?: "tall" | "compact";
}

/**
 * The Aurora Grand at dusk under the northern lights, with a title over it.
 */
export function Hero({ title, children, size = "tall" }: HeroProps) {
  return (
    <div className={`hero hero-${size}`}>
      <img src={hotel} alt="" className="hero-image" />
      <div className="hero-text">
        <p className="hero-title">{title}</p>
        {children && <div className="hero-subtitle">{children}</div>}
      </div>
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
