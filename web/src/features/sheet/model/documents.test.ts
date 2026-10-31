// documents.test.ts
// The documents of a sheet: the form of a document and what is sent of it, the inputs in their groups, the use of a ticked input, the consumers of an output (annex B 4).

import { documentOf, inputOf, painPointOf, sheetViewOf, documentUseOf } from '../../../test/sheets.ts';
import {
  consumersOf, currentUse, documentBody, entryOf, inputGroups, NEW_DOCUMENT, painPointOf as findPainPoint, showsPainPoint, bodyOfUse, entryOfUse, webAddressOf,
} from './documents.ts';

describe('a document typed (B-4-66 to B-4-69, B-4-78)', () => {
  it('reads a document into its form, its wait as typed', () => {
    const document = documentOf('d1', { name: 'Devis', location: 'https://localhost/devis', wait_hours: 4.5, share_confirmed: true, version: 3 });
    expect([entryOf(document), entryOf(documentOf('d2'))]).toEqual([
      { id: 'd1', version: 3, name: 'Devis', location: 'https://localhost/devis', wait: '4.5', shared: true },
      { id: 'd2', version: 1, name: 'Document d2', location: '', wait: '', shared: false },
    ]);
  });

  it('opens an http(s) address typed as a URL by the host, never a path nor another scheme', () => {
    expect([
      webAddressOf(documentOf('a', { location: 'https://localhost/devis', location_type: 'url' })), webAddressOf(documentOf('b', { location: 'HTTP://localhost', location_type: 'url' })),
      webAddressOf(documentOf('c', { location: '\\\\srv\\devis', location_type: 'path' })), webAddressOf(documentOf('d', { location: 'javascript:alert(1)', location_type: 'url' })),
      webAddressOf(documentOf('e', { location: 'https://localhost', location_type: 'path' })), webAddressOf(documentOf('f')),
      webAddressOf(documentOf('g', { location: 'https:localhost', location_type: 'url' })),
    ]).toEqual(['https://localhost/devis', 'HTTP://localhost', null, null, null, null, null]);
  });

  it('sends the texts trimmed, no address when empty, the wait for an input only', () => {
    const entry = { ...NEW_DOCUMENT, name: '  Devis ', location: '  ', wait: '2', shared: true };
    expect([documentBody('input', entry), documentBody('output', entry), documentBody('input', { ...entry, wait: '', location: ' \\\\srv\\devis ' })]).toEqual([
      { direction: 'input', name: 'Devis', location: null, wait_hours: 2, share_confirmed: true },
      { direction: 'output', name: 'Devis', location: null, wait_hours: null, share_confirmed: true },
      { direction: 'input', name: 'Devis', location: '\\\\srv\\devis', wait_hours: null, share_confirmed: true },
    ]);
  });
});

describe('the inputs in their groups (B-4-62, B-4-64, B-4-77)', () => {
  const near = inputOf(documentOf('n1'));
  const nearUsed = inputOf(documentOf('n2'), { used: true });
  const other = inputOf(documentOf('o1'), { near: false, used: true });
  const external = inputOf(documentOf('e1', { direction: 'input', step_id: 's2' }), { producer: null, near: false, used: true });

  it('gives the near ones, then the ones from elsewhere, then the external ones', () => {
    expect(inputGroups([external, other, near, nearUsed], true).map((input) => input.document.id)).toEqual(['n1', 'n2', 'o1', 'e1']);
  });

  it('in reading, only the inputs used, the external ones always', () => {
    expect(inputGroups([external, other, near, nearUsed], false).map((input) => input.document.id)).toEqual(['n2', 'o1', 'e1']);
  });
});

describe('the use of a ticked input (B-4-71 to B-4-75)', () => {
  const pain = painPointOf('p1', { description: 'Arrive sans numéro', lost_hours_per_month: 3 });
  const input = inputOf(documentOf('n1'), { used: true, use: documentUseOf('n1', 's2', { wait_hours: 4, percent_complete_accurate: 70, pain_point_id: 'p1', version: 2 }) });

  it('finds the pain point of a use among those of the sheet', () => {
    const view = sheetViewOf({ pain_points: [painPointOf('p0'), pain] });
    expect([findPainPoint(view, 'p1'), findPainPoint(view, null), findPainPoint(view, 'gone')]).toEqual([pain, null, null]);
  });

  it('reads the use as typed and as the host holds it', () => {
    expect([entryOfUse(input, pain), currentUse(input, pain)]).toEqual([
      { wait: '4', percent: '70', painPoint: 'Arrive sans numéro', lost: '3' },
      { wait_hours: 4, percent_complete_accurate: 70, pain_point: 'Arrive sans numéro', lost_hours_per_month: 3 },
    ]);
    expect([entryOfUse(inputOf(documentOf('x')), null), currentUse(inputOf(documentOf('x')), null)]).toEqual([
      { wait: '', percent: '', painPoint: '', lost: '' }, { wait_hours: null, percent_complete_accurate: null, pain_point: null, lost_hours_per_month: null },
    ]);
  });

  it('sends the percentage rounded, the pain point trimmed, the time lost only with a pain point', () => {
    expect([
      bodyOfUse({ wait: '1.5', percent: '72.4', painPoint: ' Ressaisie ', lost: '2' }), bodyOfUse({ wait: '', percent: '', painPoint: '  ', lost: '2' }),
    ]).toEqual([
      { wait_hours: 1.5, percent_complete_accurate: 72, pain_point: 'Ressaisie', lost_hours_per_month: 2 },
      { wait_hours: null, percent_complete_accurate: null, pain_point: null, lost_hours_per_month: null },
    ]);
  });

  it('shows the pain point under 100 %, or when one is typed, or when one exists', () => {
    const typed = (percent: string, painPoint = '') => ({ wait: '', percent, painPoint, lost: '' });
    expect([showsPainPoint(typed('95'), null), showsPainPoint(typed('100'), null), showsPainPoint(typed(''), null), showsPainPoint(typed('100', 'x'), null), showsPainPoint(typed(''), pain)])
      .toEqual([true, false, false, true, true]);
  });
});

describe('the consumers of an output (B-4-79, B-4-80)', () => {
  it('names each step that uses it by its code, with its share usable as is and its pain point', () => {
    const pain = painPointOf('p1');
    const view = sheetViewOf({ pain_points: [pain] });
    const output = { document: documentOf('d1'), used_by: [documentUseOf('d1', 's3', { percent_complete_accurate: 90, pain_point_id: 'p1' }), documentUseOf('d1', 'far')] };
    const code = (stepId: string) => (stepId === 's3' ? 'VTE-E003' : null);
    expect(consumersOf(output, view, code)).toEqual([{ code: 'VTE-E003', percent: 90, painPoint: pain }, { code: null, percent: null, painPoint: null }]);
  });
});
