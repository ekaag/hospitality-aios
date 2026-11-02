import { act, cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HotelPicker } from "./HotelPicker";
import { lookOf } from "./hotels";
import { useChosenHotel } from "./useChosenHotel";
import { useHotelLook } from "./useHotelLook";

const hotels = [
  { id: "m", name: "Mysore Grand" },
  { id: "a", name: "Aurora Grand" },
];

beforeEach(() => localStorage.clear());
afterEach(cleanup);

describe("lookOf", () => {
  it("gives each hotel with a look its own, and any other the Aurora Grand's", () => {
    expect(lookOf("Mysore Grand")).toBe("mysore-grand");
    expect(lookOf("Aurora Grand")).toBe("aurora-grand");
    expect(lookOf("Harbour Inn")).toBe("aurora-grand");
    expect(lookOf(undefined)).toBe("aurora-grand");
  });
});

describe("HotelPicker", () => {
  it("offers the hotels by name and says which was chosen", () => {
    const choose = vi.fn();
    render(<HotelPicker hotels={hotels} value="a" onChange={choose} />);

    const picker = screen.getByRole("combobox", { name: "Hotel" });
    expect([...picker.querySelectorAll("option")].map((option) => option.textContent)).toEqual(["Aurora Grand", "Mysore Grand"]);

    fireEvent.change(picker, { target: { value: "m" } });
    expect(choose).toHaveBeenCalledWith("m");
  });
});

describe("useChosenHotel", () => {
  it("starts with the first hotel by name, and remembers a choice on this device", () => {
    const { result } = renderHook(() => useChosenHotel("staff-web", hotels));
    expect(result.current[0]).toBe("a");

    act(() => result.current[1]("m"));
    expect(result.current[0]).toBe("m");

    const again = renderHook(() => useChosenHotel("staff-web", hotels));
    expect(again.result.current[0]).toBe("m");
  });

  it("forgets a remembered hotel that is no longer one of them", () => {
    localStorage.setItem("hospitalityos.admin-web.hotel", "gone");

    const { result } = renderHook(() => useChosenHotel("admin-web", hotels));

    expect(result.current[0]).toBe("a");
  });
});

describe("useHotelLook", () => {
  it("dresses the page in the hotel's palette while shown", () => {
    const shown = renderHook(() => useHotelLook("mysore-grand"));
    expect(document.documentElement.dataset.hotel).toBe("mysore-grand");

    shown.unmount();
    expect(document.documentElement.dataset.hotel).toBeUndefined();
  });
});
