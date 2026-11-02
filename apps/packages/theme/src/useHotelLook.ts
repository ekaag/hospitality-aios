import { useEffect } from "react";
import type { HotelLook } from "./hotels";

/**
 * Dresses the page in a hotel's palette while it is shown (`data-hotel` on the root element, which the theme's tokens
 * follow), and puts the default back after.
 */
export function useHotelLook(look: HotelLook) {
  useEffect(() => {
    const root = document.documentElement;
    root.dataset.hotel = look;

    return () => {
      delete root.dataset.hotel;
    };
  }, [look]);
}
