import type { Schema } from "@hospitalityos/api-client";

type Weekly = Schema<"WeeklyHours">[];
type Day = NonNullable<Schema<"WeeklyHours">["day"]>;

export const days: readonly Day[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

const time = (value: string | null | undefined) => (value ?? "").slice(0, 5);

/**
 * One day's hours as people write them, e.g. `07:00-12:00, 14:00-22:00`; empty is closed.
 */
export function dayText(weekly: Weekly, day: Day): string {
  const hours = weekly.find((each) => each.day === day);

  return (hours?.intervals ?? []).map((interval) => `${time(interval.opens)}-${time(interval.closes)}`).join(", ");
}

/**
 * The week at a glance, days with the same hours together, e.g. `Every day 07:00-22:00`.
 */
export function hoursText(weekly: Weekly): string {
  const texts = days.map((day) => dayText(weekly, day) || "closed");

  if (texts.every((text) => text === texts[0])) {
    return `Every day ${texts[0] ?? "closed"}`;
  }

  return days.map((day, index) => `${day.slice(0, 3)} ${texts[index] ?? "closed"}`).join("; ");
}

/**
 * The week from each day's text, or the first day that does not read as `HH:mm-HH:mm, ...`.
 */
export function parseWeek(texts: Readonly<Record<Day, string>>): { weekly: Weekly } | { invalid: Day } {
  const weekly: Weekly = [];

  for (const day of days) {
    const text = texts[day].trim();

    if (!text) {
      continue;
    }

    const intervals = text.split(",").map((part) => /^\s*(\d{2}:\d{2})\s*[-–]\s*(\d{2}:\d{2})\s*$/.exec(part));

    if (intervals.some((match) => !match)) {
      return { invalid: day };
    }

    weekly.push({ day, intervals: intervals.map((match) => ({ opens: `${match?.[1] ?? ""}:00`, closes: `${match?.[2] ?? ""}:00` })) });
  }

  return { weekly };
}
