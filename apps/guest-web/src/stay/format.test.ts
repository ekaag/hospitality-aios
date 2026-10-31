import { describe, expect, it } from "vitest";
import { timeOfDay, what } from "./format";

const towels = {
  id: "r1",
  serviceName: "Housekeeping amenities",
  itemName: "Bath towel",
  quantity: 2,
  description: null,
  location: "Room 412",
  status: "Open" as const,
  localRequestedAt: "2026-10-31T10:22:00",
};

describe("format", () => {
  it("says what a request is for", () => {
    expect(what(towels)).toBe("2 × Bath towel");
    expect(what({ ...towels, itemName: null, description: "The air conditioning is not working" })).toBe("The air conditioning is not working");
    expect(what({ ...towels, itemName: null })).toBe("Housekeeping amenities");
  });

  it("shows the property's own time of day, whatever the browser's zone", () => {
    expect(timeOfDay("2026-10-31T10:22:00")).toBe("10:22");
  });
});
