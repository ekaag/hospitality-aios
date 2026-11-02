export interface HotelPickerProps {
  hotels: readonly { id: string; name: string }[];
  value: string;
  onChange: (id: string) => void;
}

/**
 * Chooses which of the tenant's hotels the app shows: a labelled drop-down, sorted by name.
 */
export function HotelPicker({ hotels, value, onChange }: HotelPickerProps) {
  const sorted = [...hotels].sort((a, b) => a.name.localeCompare(b.name));

  return (
    <label className="hotel-picker">
      <span>Hotel</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        {sorted.map((hotel) => (
          <option key={hotel.id} value={hotel.id}>
            {hotel.name}
          </option>
        ))}
      </select>
    </label>
  );
}
