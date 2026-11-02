import { useCallback, useState } from "react";

/**
 * Which of the tenant's hotels an app shows: the one last chosen on this device, if it is still one of `hotels`,
 * otherwise the first by name. The choice is a convenience kept in this browser only.
 */
export function useChosenHotel(app: string, hotels: readonly { id: string; name: string }[] | undefined): [string | undefined, (id: string) => void] {
  const key = `hospitalityos.${app}.hotel`;
  const [chosen, setChosen] = useState<string | undefined>(() => read(key));

  const choose = useCallback(
    (id: string) => {
      setChosen(id);

      try {
        localStorage.setItem(key, id);
      } catch {
        // Storage can be unavailable (a private window); the choice then lasts until the page is closed.
      }
    },
    [key],
  );

  if (!hotels || hotels.length === 0) {
    return [undefined, choose];
  }

  const shown = hotels.some((hotel) => hotel.id === chosen) ? chosen : [...hotels].sort((a, b) => a.name.localeCompare(b.name))[0]?.id;

  return [shown, choose];
}

function read(key: string): string | undefined {
  try {
    return localStorage.getItem(key) ?? undefined;
  } catch {
    return undefined;
  }
}
