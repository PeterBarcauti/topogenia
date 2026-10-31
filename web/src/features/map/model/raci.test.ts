// raci.test.ts
// The RACI of a map: steps concerned, roles with the implicit R, missing approver, letters, badges, matrix and its two copies (annex B 2.11).

import { laneOf, mapOf, stepOf } from '../../../test/maps.ts';
import { concernedByRaci, matrixHtml, matrixText, missingApprover, raciBadges, raciLetters, raciMatrix, rolesOf } from './raci.ts';

/** Three lanes listed out of their order, a task of the lane l2 approved by l3 and informed by l1, a start, a decision. */
const map = mapOf({
  lanes: [laneOf('l3', 2, { name: 'Direction' }), laneOf('l1', 0, { name: 'Commercial' }), laneOf('l2', 1, { name: 'Comptabilité' })],
  steps: [stepOf('d', 'l1', 2, { type: 'decision', label: 'Conforme ?' }), stepOf('s', 'l1', 0, { type: 'start' }), stepOf('t', 'l2', 1, { label: 'Facturer' })],
  raci: [
    { step_id: 't', lane_id: 'l1', role: 'I' }, { step_id: 't', lane_id: 'l3', role: 'C' }, { step_id: 't', lane_id: 'l2', role: 'A' },
    { step_id: 'd', lane_id: 'l3', role: 'A' }, { step_id: 't', lane_id: 'gone', role: 'S' },
  ],
});
const step = (id: string) => map.steps.find((candidate) => candidate.id === id) ?? stepOf(id, 'l1', 0);
const name = (lane: { name: string }) => lane.name;

describe('the steps and their roles (B-2.11-01 to B-2.11-03, B-2.11-07)', () => {
  it('only the steps with content are concerned: not the start, the end, the parallel nor the inclusive gateway', () => {
    const types = ['task', 'wait', 'decision', 'start', 'end', 'parallel', 'inclusive'] as const;
    expect(types.map((type) => concernedByRaci({ type }))).toEqual([true, true, true, false, false, false, false]);
  });

  it('the R is the lane of the step; a role left on that lane gives way to it', () => {
    expect([...rolesOf(map, step('t'))]).toEqual([['l2', 'R'], ['l1', 'I'], ['l3', 'C'], ['gone', 'S']]);
  });

  it('a step concerned without an A on another lane misses its approver; never a start', () => {
    expect([missingApprover(map, step('t')), missingApprover(map, step('d')), missingApprover(map, step('s'))]).toEqual([true, false, false]);
  });

  it('the letters of the map besides the R, in the order of its setting, each once', () => {
    expect([raciLetters({ raci_letters: 'ACI' }), raciLetters({ raci_letters: 'ASCI' }), raciLetters({ raci_letters: 'aRsxAi' })]).toEqual([['A', 'C', 'I'], ['A', 'S', 'C', 'I'], ['A', 'S', 'I']]);
  });
});

describe('the badges under a step (B-2.11-05, B-2.11-06)', () => {
  it('in the order A, S, C, I then the order of the lanes, without the lane of the step nor a lane gone', () => {
    const lanes = [laneOf('l4', 3, { name: 'Achats' }), ...map.lanes];
    const raci = [
      { step_id: 't', lane_id: 'l4', role: 'C' }, { step_id: 't', lane_id: 'l1', role: 'C' }, { step_id: 't', lane_id: 'l3', role: 'A' },
      { step_id: 't', lane_id: 'l2', role: 'S' }, { step_id: 't', lane_id: 'gone', role: 'I' }, { step_id: 'd', lane_id: 'l4', role: 'S' },
    ] as const;
    const badges = raciBadges({ lanes, raci: [...raci] }, step('t'), name);
    expect(badges.map((badge) => `${badge.role} ${badge.laneId} ${badge.name}`)).toEqual(['A l3 Direction', 'C l1 Commercial', 'C l4 Achats']);
  });
});

describe('the matrix (B-2.11-14 to B-2.11-16)', () => {
  const matrix = raciMatrix(map, name, (candidate) => candidate.label);

  it('the steps concerned in the reading order in rows, the lanes in their order in columns, the R written', () => {
    expect(matrix.columns).toEqual([{ id: 'l1', name: 'Commercial' }, { id: 'l2', name: 'Comptabilité' }, { id: 'l3', name: 'Direction' }]);
    expect(matrix.rows.map((row) => [row.code, row.label, ...row.cells])).toEqual([['VTE-E002', 'Facturer', 'I', 'R', 'C'], ['VTE-E003', 'Conforme ?', 'R', '', 'A']]);
  });

  it('as tabulated text: the headers, a line a step, tabs and line ends of the cells turned into spaces', () => {
    const odd = { columns: [{ id: 'l1', name: 'Com\tmercial' }], rows: [{ id: 'x', code: 'VTE-E001', label: 'Deux\r\nlignes', cells: ['R' as const] }] };
    expect(matrixText(odd, { code: 'Code', step: 'Étape' })).toBe('Code\tÉtape\tCom mercial\r\nVTE-E001\tDeux lignes\tR');
  });

  it('as an HTML table: headers on their ground, bordered cells, letters centred and bold, texts escaped', () => {
    const html = matrixHtml({ columns: [{ id: 'l1', name: 'R&D' }], rows: [{ id: 'x', code: 'VTE-E001', label: '<b>', cells: ['A'] }] }, { code: 'Code', step: 'Étape' }, { border: 'silver', header: 'whitesmoke', font: 'Arial' });
    expect(html).toBe('<table style="border-collapse:collapse;font-family:Arial;font-size:11pt;">'
      + '<tr><th style="border:1px solid silver;padding:4px 8px;background:whitesmoke;font-weight:bold;mso-number-format:\\@;">Code</th><th style="border:1px solid silver;padding:4px 8px;background:whitesmoke;font-weight:bold;mso-number-format:\\@;">Étape</th><th style="border:1px solid silver;padding:4px 8px;background:whitesmoke;font-weight:bold;mso-number-format:\\@;">R&amp;D</th></tr>'
      + '<tr><td style="border:1px solid silver;padding:4px 8px;mso-number-format:\\@;">VTE-E001</td><td style="border:1px solid silver;padding:4px 8px;mso-number-format:\\@;">&lt;b&gt;</td><td style="border:1px solid silver;padding:4px 8px;text-align:center;font-weight:bold;mso-number-format:\\@;">A</td></tr></table>');
  });

  it('a look that the page could not read leaves its properties out', () => {
    const html = matrixHtml({ columns: [], rows: [] }, { code: 'Code', step: 'Étape' }, { border: ' ', header: '', font: '' });
    expect(html).toBe('<table style="border-collapse:collapse;font-size:11pt;"><tr><th style="padding:4px 8px;font-weight:bold;mso-number-format:\\@;">Code</th><th style="padding:4px 8px;font-weight:bold;mso-number-format:\\@;">Étape</th></tr></table>');
  });

  it('a cell that starts like a formula gets an apostrophe in the text, so that Excel keeps it as a text (AR-130)', () => {
    const hostile = { columns: [{ id: 'l1', name: '=WEBSERVICE("x")' }], rows: [
      { id: 'x', code: 'VTE-E001', label: '+cmd', cells: ['R' as const] }, { id: 'y', code: 'VTE-E002', label: '-2+3', cells: ['' as const] },
      { id: 'z', code: 'VTE-E003', label: '@SUM(1)', cells: ['' as const] }, { id: 'w', code: 'VTE-E004', label: 'Valider = signer', cells: ['' as const] }] };
    expect(matrixText(hostile, { code: 'Code', step: 'Étape' }).split('\r\n')).toEqual([
      'Code\tÉtape\t\'=WEBSERVICE("x")', 'VTE-E001\t\'+cmd\tR', 'VTE-E002\t\'-2+3\t', 'VTE-E003\t\'@SUM(1)\t', 'VTE-E004\tValider = signer\t']);
  });

  it('every cell of the HTML table has the text format of Excel, the texts shown as they are (AR-130)', () => {
    const html = matrixHtml({ columns: [{ id: 'l1', name: '=1+1' }], rows: [{ id: 'x', code: 'VTE-E001', label: '=HYPERLINK("x")', cells: ['R'] }] },
      { code: 'Code', step: 'Étape' }, { border: '', header: '', font: '' });
    expect(html.match(/<t[dh] /g)).toHaveLength(6);
    expect(html.match(/mso-number-format:\\@;/g)).toHaveLength(6);
    expect(html).toContain('>=HYPERLINK(&quot;x&quot;)</td>');
  });
});
