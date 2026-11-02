// Fluent 2's avatar colours: each name always gets the same one.
const colours = ["dark-red", "cranberry", "pumpkin", "peach", "gold", "forest", "seafoam", "teal", "steel", "royal-blue", "lavender", "grape", "plum", "anchor"] as const;

export function initials(name: string): string {
  const words = name.split(/\s+/).filter((word) => /^\p{L}/u.test(word));
  const first = words[0]?.[0] ?? "?";
  const last = words.length > 1 ? (words.at(-1)?.[0] ?? "") : "";

  return (first + last).toUpperCase();
}

export function colourOf(name: string): (typeof colours)[number] {
  let hash = 0;

  for (const character of name) {
    hash = (hash * 31 + (character.codePointAt(0) ?? 0)) >>> 0;
  }

  return colours[hash % colours.length] ?? "steel";
}
