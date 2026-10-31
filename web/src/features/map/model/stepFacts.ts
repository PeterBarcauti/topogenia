// stepFacts.ts
// What a step shows besides its label: readable duration, sheet gauge, tint of its lane, detailed step (annex B 2.10, spec 9.4).

import type { Schema } from '../../../api/client.ts';
import { LANE_TINTS } from '../../../shared/theme/laneTints.ts';

/** A duration in minutes as the pictogram writes it: « 45 min », « 2 h », « 1 h 30 » (B-2.10-10). */
export function readableDuration(minutes: number): string {
  const total = Math.round(minutes);
  if (total < 60) return `${String(total)} min`;
  const hours = Math.floor(total / 60);
  const rest = total % 60;
  return rest === 0 ? `${String(hours)} h` : `${String(hours)} h ${String(rest).padStart(2, '0')}`;
}

/** The gauge of a sheet: its state and its width, the completeness with 4 % at least (B-2.10-22). */
export interface Gauge {
  readonly status: Exclude<Schema<'SheetStatus'>, 'empty'>;
  readonly width: number;
}

/** No gauge for an empty sheet (B-2.10-21); the state classes are those of the codes (spec 9.4: the 1.x missed two). */
export function gaugeOf(sheet: Pick<Schema<'SheetSummary'>, 'status' | 'completeness'>): Gauge | null {
  if (sheet.status === 'empty') return null;
  return { status: sheet.status, width: Math.max(4, Math.min(100, sheet.completeness)) };
}

const channels = (hex: string): [number, number, number] =>
  [1, 3, 5].map((index) => parseInt(hex.slice(index, index + 2), 16)) as [number, number, number];

/**
 * The number (1 to 8) of the tint of a lane colour: the tint whose pastel it is, else the nearest one by squared
 * distance in RGB (spec 9.2); an unreadable colour takes the first tint.
 */
export function tintNumber(colour: string): number {
  if (!/^#[0-9A-Fa-f]{6}$/.test(colour)) return 1;
  const exact = LANE_TINTS.findIndex((tint) => tint.pastel.toUpperCase() === colour.toUpperCase());
  if (exact >= 0) return exact + 1;
  const target = channels(colour);
  let best = 0;
  let bestDistance = Number.POSITIVE_INFINITY;
  LANE_TINTS.forEach((tint, index) => {
    const distance = channels(tint.pastel).reduce((sum, value, channel) => sum + (value - (target[channel] ?? 0)) ** 2, 0);
    if (distance < bestDistance) {
      best = index;
      bestDistance = distance;
    }
  });
  return best + 1;
}

/** A detailed step: its sub-map holds steps (1.x `estMacro`, host `IsMacro`). */
export const isMacro = (step: Pick<Schema<'StepView'>, 'sub_map_id' | 'sub_map_empty'>): boolean => step.sub_map_id !== null && !step.sub_map_empty;

/** The types without content, events and gateways that join: no sheet (B-2.3-14), no branch, no detail, no RACI (1.x `sansContenu`). */
const WITHOUT_CONTENT: ReadonlySet<Schema<'StepType'>> = new Set<Schema<'StepType'>>(['start', 'end', 'parallel', 'inclusive']);

export const hasContent = (type: Schema<'StepType'>): boolean => !WITHOUT_CONTENT.has(type);

/** The warnings of the layout by step, in the order the map gives them (B-2.10-16). */
export function warningsByStep(warnings: readonly Schema<'WarningView'>[]): Map<string, string[]> {
  const byStep = new Map<string, string[]>();
  for (const warning of warnings) byStep.set(warning.step_id, [...(byStep.get(warning.step_id) ?? []), warning.code]);
  return byStep;
}
