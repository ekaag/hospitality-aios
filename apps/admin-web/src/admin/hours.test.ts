import type { Schema } from "@hospitalityos/api-client";
import { describe, expect, it } from "vitest";
import { dayText, days, hoursText, parseWeek } from "./hours";

const daily: Schema<"WeeklyHours">[] = days.map((day) => ({ day, intervals: [{ opens: "07:00:00", closes: "22:00:00" }] }));

const blank = { Monday: "", Tuesday: "", Wednesday: "", Thursday: "", Friday: "", Saturday: "", Sunday: "" };

describe("hours", () => {
  it("reads a week at a glance", () => {
    expect(hoursText(daily)).toBe("Every day 07:00-22:00");
    expect(hoursText(daily.filter((day) => day.day !== "Sunday"))).toContain("Sun closed");
  });

  it("writes a day as people do", () => {
    expect(dayText([{ day: "Monday", intervals: [{ opens: "07:00:00", closes: "12:00:00" }, { opens: "14:00:00", closes: "22:00:00" }] }], "Monday")).toBe(
      "07:00-12:00, 14:00-22:00",
    );
    expect(dayText([], "Monday")).toBe("");
  });

  it("parses each day's hours, empty for closed", () => {
    expect(parseWeek({ ...blank, Monday: "07:00-12:00, 14:00 – 22:00" })).toEqual({
      weekly: [
        {
          day: "Monday",
          intervals: [
            { opens: "07:00:00", closes: "12:00:00" },
            { opens: "14:00:00", closes: "22:00:00" },
          ],
        },
      ],
    });
  });

  it("names the first day it cannot read", () => {
    expect(parseWeek({ ...blank, Tuesday: "7am to 10pm" })).toEqual({ invalid: "Tuesday" });
  });
});
