import { describe, expect, it } from "vitest";
import { describe as explain, ifMatch, optional } from "./outcome";

describe("outcome", () => {
  it("sends the version as the API's ETag", () => {
    expect(ifMatch(3)).toEqual({ "If-Match": '"3"' });
  });

  it("explains a stale edit, and otherwise what the API said", () => {
    expect(explain({ code: "request.version_mismatch", title: "Precondition failed" })).toContain("Someone else changed this");
    expect(explain({ title: "Invalid", fieldErrors: [{ field: "name", code: "name.required", message: "Required." }] })).toBe("Invalid name: Required.");
  });

  it("sends empty text as none", () => {
    expect(optional("  ")).toBeNull();
    expect(optional(" Pool ")).toBe("Pool");
  });
});
