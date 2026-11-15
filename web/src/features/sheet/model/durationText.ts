// durationText.ts
// The duration of a sheet as it is read, in the sheet and in the remainder of a dissolved step: the duration with its shortest and longest after it, the lead time with its unit, their numbers as the language of the interface writes them (Makigami M5, spec 14.2).

import type { TFunction } from 'i18next';
import type { Schema } from '../../../api/client.ts';

type SheetContent = Schema<'SheetContent'>;

/** A number as the language writes it, with the two decimals at most that the host keeps: « 12,5 » in French, « 12.5 » in English (audit 3.1 V3, U7). */
const numberText = (value: number, language: string): string => new Intl.NumberFormat(language, { maximumFractionDigits: 2 }).format(value);

/** The bounds of the duration as read (Makigami M5): « 10 à 25 minutes », « au moins 10 minutes », « au plus 25 minutes », or nothing. */
function boundsText(content: SheetContent, t: TFunction, language: string): string | null {
  const [min, max] = [content.duration_min_minutes ?? null, content.duration_max_minutes ?? null];
  if (min !== null && max !== null) return t('sheet.duration_between', { min: numberText(min, language), max: numberText(max, language) });
  if (min !== null) return t('sheet.duration_at_least', { min: numberText(min, language) });
  return max === null ? null : t('sheet.duration_at_most', { max: numberText(max, language) });
}

/** The duration with its bounds after it, « 15 minutes (10 à 25 minutes) », or the bounds alone; null when none is given. */
export function durationText(content: SheetContent, t: TFunction, language: string): string | null {
  const bounds = boundsText(content, t, language);
  if (content.duration_minutes === null) return bounds;
  // The count chooses the plural form, the text written in the language takes its place in the sentence.
  const duration = t('sheet.duration_value', { count: content.duration_minutes, replace: { count: numberText(content.duration_minutes, language) } });
  return bounds === null ? duration : `${duration} (${bounds})`;
}

/**
 * The key facts of a sheet in a line, for the head of their section (essai J10): its frequency, its duration and its
 * lead time, those given, a middle dot between two; null without one.
 */
export function factsText(content: Pick<SheetContent, 'frequency' | 'duration_minutes' | 'lead_time'>, t: TFunction, language: string): string | null {
  const frequency = content.frequency ?? null;
  const lead = content.lead_time ?? null;
  const parts = [
    frequency === null ? null : t(`map.frequencies.${frequency}`),
    content.duration_minutes === null ? null : t('sheet.duration_value', { count: content.duration_minutes, replace: { count: numberText(content.duration_minutes, language) } }),
    lead === null ? null : `${t('sheet.lead_time')} ${leadText(lead, t, language)}`,
  ].filter((part): part is string => part !== null);
  return parts.length === 0 ? null : parts.join(' · ');
}

/** A lead time with its unit agreed with its number, « 1 jour ouvré », « 2 jours ouvrés » (audit 3.2): the count chooses the plural form, as for the duration. */
function leadText(lead: NonNullable<SheetContent['lead_time']>, t: TFunction, language: string): string {
  return t(`sheet.lead_time_value.${lead.unit}`, { count: lead.value, replace: { count: numberText(lead.value, language) } });
}

/** The lead time with its unit, « 1,5 heure », null when none is given (spec 14.2, S3). */
export function leadTimeText(content: SheetContent, t: TFunction, language: string): string | null {
  const lead = content.lead_time ?? null;
  return lead === null ? null : leadText(lead, t, language);
}
