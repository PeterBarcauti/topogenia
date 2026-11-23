// searchResults.ts
// The results of a search as data (ADR 110 point 5): one list across the maps in the order of the reply, the next and the previous going round it, the object each result leads to, each word marked in an excerpt that keeps the first in view (R9b).

import type { Schema } from '../../../api/client.ts';
import { localizedName } from '../../../shared/i18n/localizedName.ts';
import type { TextPlace } from '../../../shared/text/fold.ts';
import { graphemes } from '../export/arialWidth.ts';
import type { ArrivalSubject } from '../state/arrival.ts';

type Hit = Schema<'SearchHitView'>;

/** A result and the map it was found on. */
export interface FoundHit {
  readonly mapId: string;
  readonly hit: Hit;
}

/** Every result of a reply, map after map, in the order of the host (the map open first). */
export function flatHits(reply: Schema<'ProjectSearchView'> | null): readonly FoundHit[] {
  return reply === null ? [] : reply.maps.flatMap((map) => map.hits.map((hit) => ({ mapId: map.map_id, hit })));
}

/** The rank after (1) or before (-1) the one given, round the list: from none, the first or the last; -1 for an empty list. */
export function nextIndex(index: number, count: number, direction: 1 | -1): number {
  if (count === 0) return -1;
  if (index < 0 || index >= count) return direction === 1 ? 0 : count - 1;
  return (index + direction + count) % count;
}

/** Where a result leads: its step (a document, a tool, a pain point and an action are those of their step), or a lane by its steps. */
export function subjectOf(found: FoundHit): ArrivalSubject {
  const { hit } = found;
  return hit.kind === 'lane' ? { mapId: found.mapId, stepId: null, laneId: hit.lane_id } : { mapId: found.mapId, stepId: hit.step_id, laneId: null };
}

/** A name of the reply (a map, a step) in the language shown: the rule of the names of the interface. */
export const namedIn = (named: Schema<'NamedText'>, language: string): string =>
  localizedName({ name: named.text, source_language: named.source_language, name_translations: named.translations }, language);

type Mark = Schema<'TextMatch'>;

/**
 * The marks of the words in the text of a result, those the host gives (R9b: the host is the only source of the marks),
 * kept when they are inside the text, not empty, and after the one kept before: never one out of the text nor one over
 * another. None left, the text is shown unmarked.
 */
export function marksOf(hit: Hit): readonly Mark[] {
  const kept: Mark[] = [];
  let end = 0;
  for (const mark of hit.marks) {
    if (mark.start < end || mark.length <= 0 || mark.start + mark.length > hit.text.length) continue;
    kept.push(mark);
    end = mark.start + mark.length;
  }
  return kept;
}

/** A piece of an excerpt: a run of text, marked or not. */
export interface ExcerptPart {
  readonly text: string;
  readonly marked: boolean;
}

/** The characters kept on each side of the first mark in a long text. */
const EXCERPT_ROOM = 36;

/** The place in the text after `count` characters as a person reads them, counted from `from` (the end of the text at most). */
function placeAfter(text: string, from: number, count: number): { readonly place: number; readonly cut: boolean } {
  const tail = graphemes(text.slice(from));
  return tail.length > count ? { place: from + tail.slice(0, count).join('').length, cut: true } : { place: text.length, cut: false };
}

/**
 * The text round its first mark, as pieces marked or not: before the first mark and after it, each side cut to its room
 * with an ellipsis, never in the middle of a character as a person reads it (a letter and its accent, a letter out of
 * the plane of the others); the next marks shown as long as they fit whole in the room after the first, the excerpt
 * stopping before a mark the cut would halve. A short text whole; without a mark, the text whole and unmarked.
 */
export function excerptOf(text: string, marks: readonly TextPlace[], room = EXCERPT_ROOM): readonly ExcerptPart[] {
  const [first] = marks;
  if (first === undefined) return [{ text, marked: false }];
  const before = graphemes(text.slice(0, first.start));
  const from = before.length > room ? first.start - before.slice(-room).join('').length : 0;
  let { place: to, cut } = placeAfter(text, first.start + first.length, room);
  const shown = [first];
  for (const mark of marks.slice(1)) {
    if (mark.start + mark.length > to) {
      if (mark.start < to) [to, cut] = [mark.start, true];
      break;
    }
    shown.push(mark);
  }
  const parts: ExcerptPart[] = [];
  const plain = (piece: string) => {
    if (piece !== '') parts.push({ text: piece, marked: false });
  };
  plain(from > 0 ? `…${text.slice(from, first.start)}` : text.slice(0, first.start));
  shown.forEach((mark, at) => {
    parts.push({ text: text.slice(mark.start, mark.start + mark.length), marked: true });
    const last = at === shown.length - 1;
    plain(`${text.slice(mark.start + mark.length, last ? to : shown[at + 1]?.start)}${last && cut ? '…' : ''}`);
  });
  return parts;
}
