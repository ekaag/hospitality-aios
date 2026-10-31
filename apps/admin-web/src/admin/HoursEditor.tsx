import type { Schema } from "@hospitalityos/api-client";
import { useState, type FormEvent } from "react";
import { dayText, days, parseWeek } from "./hours";
import { describe, type Outcome } from "./outcome";

type Weekly = Schema<"WeeklyHours">[];
type Day = (typeof days)[number];

/**
 * Edits the usual weekly hours, a line per day (`07:00-22:00`, several separated by commas, empty for closed); exceptions
 * and closures are kept as they are.
 */
export function HoursEditor({ weekly, onSave }: { weekly: Weekly; onSave: (weekly: Weekly) => Promise<Outcome> }) {
  const [texts, setTexts] = useState<Record<Day, string>>(() => Object.fromEntries(days.map((day) => [day, dayText(weekly, day)])) as Record<Day, string>);
  const [problem, setProblem] = useState<string | undefined>();

  async function submit(event: FormEvent) {
    event.preventDefault();
    const parsed = parseWeek(texts);

    if ("invalid" in parsed) {
      setProblem(`${parsed.invalid}: write hours as 07:00-22:00, several separated by commas, or leave it empty for closed.`);

      return;
    }

    const outcome = await onSave(parsed.weekly);
    setProblem(outcome.error ? describe(outcome.error) : undefined);
  }

  return (
    <form onSubmit={(event) => void submit(event)} className="edit">
      <h4>Edit weekly hours</h4>
      {problem && <p role="alert">{problem}</p>}
      {days.map((day) => (
        <p key={day}>
          <label htmlFor={`hours-${day}`}>{day}</label>
          <input id={`hours-${day}`} type="text" value={texts[day]} onChange={(event) => setTexts((now) => ({ ...now, [day]: event.target.value }))} />
        </p>
      ))}
      <button type="submit">Save hours</button>
    </form>
  );
}
