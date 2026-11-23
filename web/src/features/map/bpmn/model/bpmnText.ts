// bpmnText.ts
// The texts of a BPMN diagram: a text of the .pmap in the language shown (its original otherwise, said untranslated), the name of a flow with its share, and a duration of a sheet in ISO 8601 for a timer.

import type { PmapSheet, PmapTranslations } from '../../../../shared/pmap.ts';

/** A text in the language shown, and whether it is written in it (the original of another language otherwise). */
export interface ShownText {
  readonly text: string;
  readonly translated: boolean;
}

/**
 * A text of the .pmap in a language: the original when the language is its own, its translation when there is one, the
 * original otherwise, said untranslated so that the view writes it in italics (ADR 083).
 */
export function shown(original: string, translations: PmapTranslations | undefined, origin: string, language: string): ShownText {
  if (language === origin) return { text: original, translated: true };
  const translation = translations?.[language];
  return translation === undefined || translation.trim() === '' ? { text: original, translated: false } : { text: translation, translated: true };
}

/** A number written without trailing zeros and with a point, whatever the culture (15, 1.5, 151.67). */
export const plainNumber = (value: number): string => String(Number(value.toFixed(6)));

/** The share of a flow after its label, as the map writes it (« non · 15 % », `labelWithShare`). */
export function flowName(label: string, share: number | undefined): string | null {
  const percent = share === undefined ? '' : `${plainNumber(share)} %`;
  if (label === '' && percent === '') return null;
  return label === '' ? percent : percent === '' ? label : `${label} · ${percent}`;
}

/**
 * The duration of a wait in ISO 8601 (`PT2H`, `PT45M`, `P3D`), its lead time first (the time a case waits there), its
 * duration otherwise; null when the sheet gives neither. A day is a working day in the map, a day in the file: the
 * documentation of the step says it.
 */
export function isoDuration(sheet: PmapSheet | undefined): string | null {
  const lead = sheet?.lead_time;
  if (lead !== undefined) {
    const value = plainNumber(lead.value);
    return lead.unit === 'day' ? `P${value}D` : lead.unit === 'hour' ? `PT${value}H` : `PT${value}M`;
  }
  const minutes = sheet?.duration_minutes;
  return minutes === undefined ? null : `PT${plainNumber(minutes)}M`;
}
