// analysis.ts
// What the layer « Analysis » of the Makigami shows (plan 3.1): the flows where the case changes hands, numbered in the order of the core, the count each detailed step carries, the class of each step and its breaks of medium, the tiles of its panel. Nothing is computed here: the core gives every figure.

import type { TFunction } from 'i18next';
import type { Schema } from '../../../api/client.ts';
import type { Point } from './geometry/grid.ts';

type StepClass = NonNullable<Schema<'ValueClass'>>;

/**
 * The marks of the layer: each flow of handoff with its number from 1, each detailed step that carries handoffs with its
 * count, the class of each step classified (M1), the steps where the medium breaks and the detailed steps whose detail has
 * some (M3).
 */
export interface AnalysisMarks {
  readonly handoffs: ReadonlyMap<string, number>;
  readonly detailed: Readonly<Record<string, number>>;
  readonly classes: Readonly<Record<string, StepClass>>;
  readonly breaks: ReadonlySet<string>;
}

/** The marks of a map as the core gives its value stream; a detailed step without handoff carries no badge. */
export function analysisMarksOf(stream: Schema<'MapStream'>, steps: readonly Schema<'StepView'>[]): AnalysisMarks {
  const { flows, detailed_steps: detailed } = stream.handoffs;
  const breaks = stream.media_breaks;
  return {
    handoffs: new Map(flows.map((id, index) => [id, index + 1])),
    detailed: Object.fromEntries(Object.entries(detailed).filter(([, count]) => count > 0)),
    classes: Object.fromEntries(steps.flatMap((step) => (step.value_class == null ? [] : [[step.id, step.value_class]]))),
    breaks: new Set([...breaks.steps.map((found) => found.step_id), ...Object.entries(breaks.detailed_steps).filter(([, count]) => count > 0).map(([id]) => id)]),
  };
}

/** The tiles of the panel, in their order. */
export type AnalysisTileKey = 'handoffs' | 'value' | 'breaks';

/** A tile: its figure, the precision under it. */
export interface AnalysisTile {
  readonly key: AnalysisTileKey;
  readonly value: string;
  readonly note: string;
}

/**
 * The tiles of the panel « Analysis », their numbers in the language of the interface: the handoffs and their count by case;
 * the share of the value added in the work classified with its coverage, or the steps classified when the work is unknown;
 * the breaks of medium and the documents whose medium is given.
 */
export function analysisTilesOf(stream: Schema<'MapStream'>, t: TFunction, language: string): AnalysisTile[] {
  const { count, per_case: perCase } = stream.handoffs;
  const values = stream.value_classes;
  const breaks = stream.media_breaks;
  const number = new Intl.NumberFormat(language, { maximumFractionDigits: 1 });
  const percent = new Intl.NumberFormat(language, { style: 'percent', maximumFractionDigits: 0 });
  const classified = values.value_added + values.necessary + values.waste;
  const share = values.value_added_share ?? null;
  const coverage = values.coverage ?? null;
  return [
    {
      key: 'handoffs', value: number.format(count),
      note: perCase === null ? t('analysis.handoffs.per_case_unknown') : t('analysis.handoffs.per_case', { value: number.format(perCase) }),
    },
    share === null || coverage === null
      ? { key: 'value', value: `${String(values.value_added)} / ${String(classified + values.unclassified)}`, note: t('analysis.value.steps', { count: values.unclassified }) }
      : { key: 'value', value: percent.format(share), note: t('analysis.value.coverage', { value: percent.format(coverage) }) },
    { key: 'breaks', value: number.format(breaks.count), note: t('analysis.breaks.documents', { given: breaks.documents_with_medium, count: breaks.documents }) },
  ];
}

/** The point halfway along a routed path, by its length, where the number of a handoff stands; null for a path of less than two points. */
export function middleOf(points: readonly Point[]): Point | null {
  if (points.length < 2) return null;
  const lengths = points.slice(1).map((point, index) => Math.hypot(point.x - (points[index]?.x ?? 0), point.y - (points[index]?.y ?? 0)));
  let left = lengths.reduce((sum, length) => sum + length, 0) / 2;
  for (const [index, length] of lengths.entries()) {
    const [from, to] = [points[index], points[index + 1]];
    if (from === undefined || to === undefined) break;
    if (left <= length && length > 0) return { x: from.x + ((to.x - from.x) * left) / length, y: from.y + ((to.y - from.y) * left) / length };
    left -= length;
  }
  return points[points.length - 1] ?? null;
}
