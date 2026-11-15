// svgIcons.ts
// The icons the image SVG of a map draws (plan 3.1, L7), those of the canvas: their strokes read from the components of lucide rendered as text once, at the export, so that the image draws them as paths of its own, without a reference nor a script.

import { ArrowRightLeft, Circle, Clock, Clock3, FileText, Flame, Hourglass, Layers, LogIn, LogOut, Plus, Timer, UserRound, X, type LucideIcon } from 'lucide-react';
import { createElement } from 'react';

/** The icons of the canvas the image needs: the markers of the gateways, the pictograms, the badges (the handoffs of a detail under the layer « Analysis »), the cases of the value stream. */
const ICONS = {
  decision: X, parallel: Plus, inclusive: Circle, documents: FileText, painPoints: Flame, duration: Timer, detailed: Layers, frequency: Clock,
  throughput: Layers, work: Clock3, lead: Hourglass, fte: UserRound, entry: LogIn, exit: LogOut, handoff: ArrowRightLeft,
} as const satisfies Record<string, LucideIcon>;

export type SvgIconName = keyof typeof ICONS;

/** The strokes of each icon, in its square of 24 units, without their frame. */
export type SvgIcons = Readonly<Record<SvgIconName, string>>;

/** The content of a SVG rendered by React: what its root holds. */
const contentOf = (markup: string): string => markup.replace(/^<svg[^>]*>/, '').replace(/<\/svg>$/, '');

/** The strokes of the icons, the renderer of React loaded only when an image is exported. */
export async function svgIcons(): Promise<SvgIcons> {
  const { renderToStaticMarkup } = await import('react-dom/server');
  const entries = Object.entries(ICONS).map(([name, icon]) => [name, contentOf(renderToStaticMarkup(createElement(icon)))] as const);
  return Object.fromEntries(entries) as Record<SvgIconName, string>;
}
