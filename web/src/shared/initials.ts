// initials.ts
// The initials of a person shown in a round badge: the header, the participants of the home page (annex B 1.3).

/** The first letter of the first and of the last word, or the first two letters of a single word; `?` without a name. */
export function initialsOf(name: string): string {
  const words = name.trim().split(/[\s._-]+/).filter((word) => word !== '');
  const first = words[0];
  const last = words.at(-1);
  if (first === undefined || last === undefined) return '?';
  if (words.length === 1) return first.slice(0, 2).toUpperCase();
  return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
}
