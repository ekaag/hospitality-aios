import { useCallback, useEffect, useState } from "react";

const changed = "hospitalityos:hotel-chosen";

/**
 * Which of the tenant's hotels an app shows: the one last chosen on this device, if it is still one of `hotels`,
 * otherwise the first by name. The choice is a convenience kept in this browser only; every part of the app that asks
 * sees the same choice (the sign-in page and the signed-in app alike).
 */
export function useChosenHotel(app: string, hotels: readonly { id: string; name: string }[] | undefined): [string | undefined, (id: string) => void] {
  const key = `hospitalityos.${app}.hotel`;
  const [chosen, setChosen] = useState<string | undefined>(() => read(key));

  useEffect(() => {
    const follow = (event: Event) => {
      const detail = (event as CustomEvent<{ key: string; id: string }>).detail;

      if (detail.key === key) {
        setChosen(detail.id);
      }
    };

    window.addEventListener(changed, follow);

    return () => window.removeEventListener(changed, follow);
  }, [key]);

  const choose = useCallback(
    (id: string) => {
      setChosen(id);

      try {
        localStorage.setItem(key, id);
      } catch {
        // Storage can be unavailable (a private window); the choice then lasts until the page is closed.
      }

      window.dispatchEvent(new CustomEvent(changed, { detail: { key, id } }));
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
