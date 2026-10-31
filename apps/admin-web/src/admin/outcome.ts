import type { Problem } from "@hospitalityos/api-client";

/**
 * What a change through the API came to: nothing on success, else the problem it sent.
 */
export interface Outcome {
  error?: unknown;
}

/**
 * The record's version, as the API expects it back in `If-Match`, so a stale edit is refused (412) rather than
 * overwriting someone else's.
 */
export function ifMatch(version: number): { "If-Match": string } {
  return { "If-Match": `"${String(version)}"` };
}

/**
 * The problem for people: a stale edit says so, anything else says what the API said.
 */
export function describe(error: unknown): string {
  const problem = error as Problem;

  if (problem.code === "request.version_mismatch") {
    return "Someone else changed this meanwhile. It has been reloaded; make your change again.";
  }

  const fields = (problem.fieldErrors ?? []).map((field) => `${field.field}: ${field.message}`).join("; ");

  return [problem.title ?? "That could not be done.", fields].filter(Boolean).join(" ");
}

/**
 * An optional text field as the API takes it: empty is none.
 */
export function optional(value: string | boolean | undefined): string | null {
  return typeof value === "string" && value.trim() ? value.trim() : null;
}
