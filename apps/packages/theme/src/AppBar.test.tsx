import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AppBar } from "./AppBar";
import { colourOf, initials } from "./names";

afterEach(cleanup);

describe("AppBar", () => {
  it("names the app, and who is signed in with a way to sign out", () => {
    const signOut = vi.fn();
    render(<AppBar product="Aurora Grand" area="Guest" user={{ name: "Alex Morgan", role: "Guest" }} onSignOut={signOut} />);

    expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Aurora Grand · Guest");
    expect(screen.getByText("Alex Morgan")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    expect(signOut).toHaveBeenCalledOnce();
  });

  it("offers no sign-out while nobody is signed in", () => {
    render(<AppBar product="HospitalityOS" area="Staff" onSignOut={vi.fn()} />);

    expect(screen.queryByRole("button", { name: "Sign out" })).toBeNull();
  });
});

describe("Avatar", () => {
  it("shows the first and last initials", () => {
    expect(initials("Alex Morgan")).toBe("AM");
    expect(initials("Priya")).toBe("P");
    expect(initials("Jo van der Berg")).toBe("JB");
    expect(initials("  ")).toBe("?");
  });

  it("gives each name the same colour every time", () => {
    expect(colourOf("Sam Patel")).toBe(colourOf("Sam Patel"));
    expect(new Set(["Sam Patel", "Jo Okafor", "Ravi Mehta", "Dana Reyes", "Priya Shah"].map(colourOf)).size).toBeGreaterThan(1);
  });
});
