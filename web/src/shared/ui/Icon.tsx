// Icon.tsx
// A Lucide icon of the interface, embedded and hidden from assistive technologies (spec 9.5).

import type { LucideIcon } from 'lucide-react';

export function Icon({ icon: Glyph, size = 16, strokeWidth = 2 }: { icon: LucideIcon; size?: number; strokeWidth?: number }) {
  return <Glyph className="i" size={size} strokeWidth={strokeWidth} aria-hidden="true" focusable="false" />;
}
