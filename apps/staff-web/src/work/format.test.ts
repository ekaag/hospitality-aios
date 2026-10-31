import { describe, expect, it } from "vitest";
import { ago, isOverdue } from "./format";

const now = new Date("2026-10-31T10:30:00Z");

describe("ago", () => {
  it.each([
    ["2026-10-31T10:29:40Z", "just now"],
    ["2026-10-31T10:29:00Z", "1 minute ago"],
    ["2026-10-31T10:22:00Z", "8 minutes ago"],
    ["2026-10-31T09:30:00Z", "1 hour ago"],
    ["2026-10-31T07:10:00Z", "3 hours ago"],
  ])("reads %s as %s", (at, expected) => {
    expect(ago(at, now)).toBe(expected);
  });
});

describe("isOverdue", () => {
  it("is true only once the deadline has passed", () => {
    expect(isOverdue("2026-10-31T10:29:00Z", now)).toBe(true);
    expect(isOverdue("2026-10-31T10:42:00Z", now)).toBe(false);
    expect(isOverdue(null, now)).toBe(false);
  });
});
