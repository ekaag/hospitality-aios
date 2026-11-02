/// <reference types="vite/client" />
import auroraGrand from "./aurora-grand.svg";
import mysoreGrand from "./mysore-grand.svg";

/**
 * The hotels with a look of their own: the Aurora Grand under the northern lights, and the Mysore Grand after Mysore
 * Palace illuminated. Any other hotel gets the Aurora Grand's.
 */
export type HotelLook = "aurora-grand" | "mysore-grand";

export const pictures: Record<HotelLook, string> = {
  "aurora-grand": auroraGrand,
  "mysore-grand": mysoreGrand,
};

/**
 * The look for a hotel, by its name.
 */
export function lookOf(name: string | undefined): HotelLook {
  const slug = (name ?? "").trim().toLowerCase().replace(/[^a-z0-9]+/g, "-");

  return slug in pictures ? (slug as HotelLook) : "aurora-grand";
}
