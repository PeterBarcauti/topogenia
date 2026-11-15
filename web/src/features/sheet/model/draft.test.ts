// draft.test.ts
// The sheet as typed: the draft read from the content, what is sent of it, the mode at the loading, the summary of the map, the tools offered (annex B 4).

import { contentOf, sheetViewOf } from '../../../test/sheets.ts';
import { bodyOf, boundOutOfOrder, citedTools, draftOf, EMPTY_DRAFT, offeredTools, onlyFreeText, summaryOf } from './draft.ts';

describe('what is sent (B-4-47 to B-4-49)', () => {
  it('trims the texts, leaves out the empty fields and the operations without text, sends an empty free text as null', () => {
    const body = bodyOf({
      ...EMPTY_DRAFT, actor: '  ', purpose: '  Facturer le client ', tools: 'Tableur', frequency: 'weekly', duration: '20',
      procedure: [{ text: ' Extraire ', tool: '  ', nature: 'extract' }, { text: '   ', tool: 'Messagerie', nature: 'send' }, { text: 'Contrôler', tool: ' ERP ', nature: null }],
      freeText: '   ',
    });
    expect(body).toEqual({
      fields: {
        purpose: { value: 'Facturer le client', number: null }, tools: { value: 'Tableur', number: null }, frequency: { value: 'weekly', number: null },
        duration_minutes: { value: null, number: 20 },
      },
      procedure: [{ text: 'Extraire', tool: null, nature: 'extract' }, { text: 'Contrôler', tool: 'ERP', nature: null }],
      free_text: null,
    });
  });

  it('sends the free text trimmed, a duration of zero, and no duration when the field is empty or not a number', () => {
    expect([bodyOf({ ...EMPTY_DRAFT, duration: '0', freeText: ' Libre ' }), bodyOf({ ...EMPTY_DRAFT, duration: 'abc' }), bodyOf({ ...EMPTY_DRAFT, duration: ' ' })]).toEqual([
      { fields: { duration_minutes: { value: null, number: 0 } }, procedure: [], free_text: 'Libre' },
      { fields: {}, procedure: [], free_text: null },
      { fields: {}, procedure: [], free_text: null },
    ]);
  });
});

describe('the draft read and the mode chosen (B-4-19, B-4-20)', () => {
  it('reads each field, the duration as typed, the procedure with an empty tool for none, the free text', () => {
    const content = contentOf({
      actor: 'Assistante', purpose: 'Facturer', tools: 'ERP', frequency: 'monthly', duration_minutes: 12.5,
      procedure: [{ text: 'Saisir', tool: null, nature: 're_enter' }], free_text: 'Notes',
    });
    expect(draftOf(content)).toEqual({
      actor: 'Assistante', purpose: 'Facturer', tools: 'ERP', frequency: 'monthly', duration: '12.5', durationMin: '', durationMax: '', leadTime: '', leadTimeUnit: 'hour',
      procedure: [{ text: 'Saisir', tool: '', nature: 're_enter' }], freeText: 'Notes',
    });
    expect(draftOf(contentOf())).toEqual(EMPTY_DRAFT);
  });

  it('reads the lead time and its unit, and sends them together, or none when the field is empty (spec 14.2, S3)', () => {
    const draft = draftOf(contentOf({ lead_time: { value: 1.5, unit: 'day' } }));
    expect([draft.leadTime, draft.leadTimeUnit]).toEqual(['1.5', 'day']);
    expect(bodyOf(draft).fields?.lead_time).toEqual({ value: 'day', number: 1.5 });
    expect(bodyOf({ ...draft, leadTime: ' ' }).fields?.lead_time).toBeUndefined();
  });

  it('reads the shortest and the longest duration, and sends each one typed (Makigami M5)', () => {
    const draft = draftOf(contentOf({ duration_minutes: 15, duration_min_minutes: 10, duration_max_minutes: 25.5 }));
    expect([draft.durationMin, draft.duration, draft.durationMax]).toEqual(['10', '15', '25.5']);
    expect(bodyOf(draft).fields).toEqual({
      duration_minutes: { value: null, number: 15 }, duration_min_minutes: { value: null, number: 10 }, duration_max_minutes: { value: null, number: 25.5 },
    });
    expect(bodyOf({ ...draft, durationMin: ' ', duration: '' }).fields).toEqual({ duration_max_minutes: { value: null, number: 25.5 } });
    expect(onlyFreeText(contentOf({ free_text: 'Notes', duration_max_minutes: 30 }))).toBe(false);
  });

  it('says the bound out of order as the host refuses it: the shortest past the duration, the longest under it or under the shortest', () => {
    const bounds = (durationMin: string, duration: string, durationMax: string) => boundOutOfOrder({ ...EMPTY_DRAFT, durationMin, duration, durationMax });
    expect([bounds('10', '15', '25'), bounds('15', '15', '15'), bounds('10', '', ''), bounds('', '', '')]).toEqual([null, null, null, null]);
    expect([bounds('20', '15', ''), bounds('20', '15', '10'), bounds('', '15', '10'), bounds('30', '', '20')]).toEqual(['min', 'min', 'max', 'max']);
  });

  it('opens on the free text when the sheet holds only a free text', () => {
    expect([
      onlyFreeText(contentOf({ free_text: 'Notes' })), onlyFreeText(contentOf()), onlyFreeText(contentOf({ free_text: 'Notes', tools: 'ERP' })),
      onlyFreeText(contentOf({ free_text: 'Notes', procedure: [{ text: 'Saisir', tool: null, nature: null }] })), onlyFreeText(contentOf({ free_text: 'Notes', duration_minutes: 0 })),
    ]).toEqual([true, false, false, false, false]);
  });
});

describe('the summary of the map (B-4-50)', () => {
  it('takes the state, the completeness, the right to fill, the frequency and the duration of the sheet read', () => {
    const view = sheetViewOf({
      status: 'draft', completeness: 60, content: contentOf({ frequency: 'daily', duration_minutes: 15 }),
      permissions: { fill: false, validate: false, request_clarification: false, assign: false },
    });
    expect(summaryOf(view)).toEqual({ status: 'draft', completeness: 60, can_fill: false, frequency: 'daily', duration_minutes: 15, lead_time: null });
  });
});

describe('the tools offered (B-4-24)', () => {
  it('reads the tools cited, split on commas, semicolons and line breaks', () => {
    expect(citedTools(' ERP, Tableur ;Messagerie\n\n Portail ,')).toEqual(['ERP', 'Tableur', 'Messagerie', 'Portail']);
  });

  it('offers the suggestions not cited yet, whatever their case', () => {
    expect(offeredTools(['ERP', 'Tableur', 'Portail'], 'erp; tableur')).toEqual(['Portail']);
  });
});
