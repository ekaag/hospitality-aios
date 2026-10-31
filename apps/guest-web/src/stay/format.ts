import type { Schema } from "@hospitalityos/api-client";

/**
 * What a request is for: the items and how many, or the problem in the guest's words.
 */
export function what(request: Schema<"GuestRequestResponse">): string {
  return request.itemName ? `${String(request.quantity)} × ${request.itemName}` : (request.description ?? request.serviceName);
}

/**
 * The time of day of a property-local time (no offset), as the property's clock shows it, wherever the guest's browser is.
 */
export function timeOfDay(local: string): string {
  return local.slice(11, 16);
}
