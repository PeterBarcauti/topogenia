// searchResults.test.ts
// The results of a search as data (ADR 110 point 5): one list across the maps, next and previous going round it, where each result leads, each word marked in an excerpt that keeps the first in view (R9b).

import type { Schema } from '../../../api/client.ts';
import { excerptOf, flatHits, marksOf, namedIn, nextIndex, subjectOf, type ExcerptPart } from './searchResults.ts';

type Hit = Schema<'SearchHitView'>;

const hit = (kind: Hit['kind'], text: string, start: number, length: number, extra: Partial<Hit> = {}): Hit => ({
  kind, step_id: null, lane_id: null, text, language: 'fr', start, length, marks: [{ start, length }], ...extra,
});

const named = (text: string, translations: Record<string, string> = {}): Schema<'NamedText'> => ({ text, source_language: 'fr', translations });

const reply: Schema<'ProjectSearchView'> = {
  total: 3,
  maps: [
    { map_id: 'root', depth: 1, name: named('Congés'), hits: [hit('step', 'Saisir la demande', 7, 7, { step_id: 's1' }), hit('lane', 'Demandeur', 0, 7, { lane_id: 'l1' })] },
    { map_id: 'detail', depth: 2, name: named('Saisir la demande'), hits: [hit('document', 'Formulaire de demande', 14, 7, { step_id: 's9', step_code: 'CONGES-E002', step: named('Remplir') })] },
  ],
};

describe('the list of the results', () => {
  it('next and previous go round the list', () => {
    expect(flatHits(reply).map((found) => [found.mapId, found.hit.text])).toEqual([
      ['root', 'Saisir la demande'], ['root', 'Demandeur'], ['detail', 'Formulaire de demande'],
    ]);
    expect(flatHits(null)).toEqual([]);
    // Nothing current yet: the first forward, the last backward; past either end, the other end.
    expect([nextIndex(-1, 3, 1), nextIndex(0, 3, 1), nextIndex(1, 3, 1), nextIndex(2, 3, 1)]).toEqual([0, 1, 2, 0]);
    expect([nextIndex(-1, 3, -1), nextIndex(0, 3, -1), nextIndex(2, 3, -1)]).toEqual([2, 2, 1]);
    expect([nextIndex(-1, 0, 1), nextIndex(4, 0, -1)]).toEqual([-1, -1]);
  });

  it('a document, a tool, a pain point lead to their step, a lane to its steps', () => {
    const lead = (kind: Hit['kind'], extra: Partial<Hit>) => subjectOf({ mapId: 'detail', hit: hit(kind, 'x', 0, 1, extra) });
    for (const kind of ['step', 'document', 'tool', 'pain_point', 'action'] as const) {
      expect(lead(kind, { step_id: 's9', step_code: 'CONGES-E010' })).toEqual({ mapId: 'detail', stepId: 's9', laneId: null });
    }
    expect(lead('lane', { lane_id: 'l1' })).toEqual({ mapId: 'detail', stepId: null, laneId: 'l1' });
  });

  it('names a map or a step in the language shown, its own name without a translation', () => {
    expect([namedIn(named('Congés', { en: 'Leave' }), 'en'), namedIn(named('Congés', { en: 'Leave' }), 'fr'), namedIn(named('Congés'), 'de')]).toEqual(['Leave', 'Congés', 'Congés']);
  });
});

/** An excerpt written out, each mark between brackets. */
const written = (parts: readonly ExcerptPart[]): string => parts.map((part) => (part.marked ? `[${part.text}]` : part.text)).join('');

describe('the words marked', () => {
  it('the excerpt keeps the word in view in a long text', () => {
    const long = `${'a'.repeat(200)} Planning d’équipe ${'b'.repeat(200)}`;
    const shown = excerptOf(long, [{ start: 201, length: 8 }]);
    const [before, word, after] = shown;
    expect(shown).toHaveLength(3);
    expect(word).toEqual({ text: 'Planning', marked: true });
    expect(before?.text.startsWith('…')).toBe(true);
    expect(after?.text.endsWith('…')).toBe(true);
    expect(written(shown).length).toBeLessThan(120);
    expect(after?.text.startsWith(' d’équipe')).toBe(true);
    // A short text is shown whole, without an ellipsis.
    expect(excerptOf('Bon de commande', [{ start: 7, length: 8 }])).toEqual([{ text: 'Bon de ', marked: false }, { text: 'commande', marked: true }]);
    // Never a half of a character at a cut: the letters out of the plane of the others stay whole.
    const [far] = excerptOf(`${'𝒜'.repeat(60)}mot`, [{ start: 120, length: 3 }]);
    expect(Array.from(far?.text.slice(1) ?? '').every((character) => character === '𝒜')).toBe(true);
  });

  it('marks each word at the places the host gives, in the order of the text (R9b)', () => {
    const sales = hit('step', 'Site web Livres à jour', 5, 3, { marks: [{ start: 5, length: 3 }, { start: 9, length: 5 }] });
    expect(marksOf(sales)).toEqual([{ start: 5, length: 3 }, { start: 9, length: 5 }]);
    expect(written(excerptOf(sales.text, marksOf(sales)))).toBe('Site [web] [Livre]s à jour');
    expect(written(excerptOf('Prévenir l’équipe', [{ start: 0, length: 8 }, { start: 11, length: 6 }]))).toBe('[Prévenir] l’[équipe]');
  });

  it('the excerpt holds the first mark and as many of the next ones as its room allows, none of them cut', () => {
    const long = `${'a'.repeat(100)} Planning d’équipe ${'c'.repeat(50)} équipe ${'b'.repeat(100)}`;
    const marks = [{ start: 101, length: 8 }, { start: 112, length: 6 }, { start: 170, length: 6 }];
    // The second word is in the room after the first, the third beyond it: not shown.
    expect(written(excerptOf(long, marks))).toBe(`…${'a'.repeat(35)} [Planning] d’[équipe] ${'c'.repeat(26)}…`);
    // A mark the cut would halve is left out whole: the excerpt stops before it.
    const across = `Planning ${'x'.repeat(30)} équipe fin`;
    expect(written(excerptOf(across, [{ start: 0, length: 8 }, { start: 40, length: 6 }]))).toBe(`[Planning] ${'x'.repeat(30)} …`);
    // The room is counted on each side of the first mark, which stays in view.
    expect(written(excerptOf('Bon de commande', [{ start: 7, length: 8 }], 3))).toBe('…de [commande]');
  });

  it('draws only the marks inside the text, by place, never overlapping; none left, the text whole and unmarked', () => {
    const marked = (marks: readonly { start: number; length: number }[]) => marksOf(hit('step', 'Vérifier la demande', 0, 0, { marks: [...marks] }));
    expect(marked([{ start: 0, length: 8 }, { start: 12, length: 7 }])).toEqual([{ start: 0, length: 8 }, { start: 12, length: 7 }]);
    // Out of the text, empty, before or over the one kept: left out.
    expect(marked([{ start: 12, length: 7 }, { start: 0, length: 8 }, { start: 14, length: 2 }, { start: 15, length: 30 }, { start: -1, length: 2 }, { start: 3, length: 0 }]))
      .toEqual([{ start: 12, length: 7 }]);
    expect(marked([{ start: 40, length: 6 }])).toEqual([]);
    expect(excerptOf('Prévenir', [])).toEqual([{ text: 'Prévenir', marked: false }]);
  });
});
