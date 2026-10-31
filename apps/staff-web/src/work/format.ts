/**
 * How long ago something happened, as staff read it on a card: "just now", "8 minutes ago", "2 hours ago".
 */
export function ago(at: string, now: Date = new Date()): string {
  const minutes = Math.floor((now.getTime() - new Date(at).getTime()) / 60_000);

  if (minutes < 1) {
    return "just now";
  }

  if (minutes < 60) {
    return minutes === 1 ? "1 minute ago" : `${minutes} minutes ago`;
  }

  const hours = Math.floor(minutes / 60);

  return hours === 1 ? "1 hour ago" : `${hours} hours ago`;
}

/**
 * A time of day in the browser's locale, e.g. "10:42 AM"; the API sends UTC.
 */
export function timeOfDay(at: string, locale?: string): string {
  return new Date(at).toLocaleTimeString(locale, { hour: "numeric", minute: "2-digit" });
}

/**
 * Whether a deadline has passed.
 */
export function isOverdue(deadline: string | null | undefined, now: Date = new Date()): boolean {
  return !!deadline && new Date(deadline) < now;
}
