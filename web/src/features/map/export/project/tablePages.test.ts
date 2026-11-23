// tablePages.test.ts
// The tables of the PDF of the project laid on pages: the RACI matrix of a map and the action plan, their header on every page, a long cell wrapped, a row never cut between two pages.

import type { Schema } from '../../../../api/client.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { laneOf, mapOf, stepOf } from '../../../../test/maps.ts';
import { french as t } from '../../../../test/translations.ts';
import { planEntries } from '../../plan/actionPlan.ts';
import { PDF_MARGIN } from '../pdf.ts';
import type { Measure } from './pageFlow.ts';
import { planTable, raciTable, tablePages, type TableData, type TablePage } from './tablePages.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

/** A measure without a font: a character is a fifth of its size in millimetres. */
const measure: Measure = (text, _face, size) => Array.from(text).length * size * 0.2;
const A3 = { width: 420, height: 297 };

/** The header of a page as the columns read it: the bold texts, column by column, top down, the cut of a long header into lines not counted. */
const headerOf = (page: TablePage): string => page.cells.filter((cell) => cell.face === 'bold').sort((a, b) => a.x - b.x || a.y - b.y).map((cell) => cell.text).join('').replace(/\s/g, '');

/** The texts of a row of a page: the cells at the same baseline, from the left. */
const rowsOf = (page: TablePage): string[][] => {
  const ys = [...new Set(page.cells.map((cell) => cell.y))].sort((a, b) => a - b);
  return ys.map((y) => page.cells.filter((cell) => cell.y === y).sort((a, b) => a.x - b.x).map((cell) => cell.text));
};

describe('the RACI matrix as a table', () => {
  const lanes = [laneOf('l3', 2, { name: 'Direction' }), laneOf('l1', 0, { name: 'Commercial' }), laneOf('l2', 1, { name: 'Comptabilité' })];
  const small = mapOf({
    lanes,
    steps: [stepOf('d', 'l1', 2, { type: 'decision', label: 'Conforme ?' }), stepOf('s', 'l1', 0, { type: 'start' }), stepOf('t', 'l2', 1, { label: 'Facturer' })],
    raci: [{ step_id: 't', lane_id: 'l1', role: 'I' }, { step_id: 't', lane_id: 'l3', role: 'C' }, { step_id: 'd', lane_id: 'l3', role: 'A' }],
  });
  const headers = { code: 'Code', step: 'Étape' };
  const table = (map = small) => raciTable(map, (lane) => lane.name, (step) => step.label, headers);

  it('gives a column per lane and a row per step concerned, in reading order', () => {
    const data = table();
    expect(data.headers).toEqual(['Code', 'Étape', 'Commercial', 'Comptabilité', 'Direction']);
    expect(data.rows).toEqual([['VIS-E002', 'Facturer', 'I', 'R', 'C'], ['VIS-E003', 'Conforme ?', 'R', '', 'A']]);
    const [page] = tablePages(data, { page: A3, measure, language: 'fr', top: 30 });
    expect(page && rowsOf(page)).toEqual([data.headers, ...data.rows.map((row) => row.map(String).filter((text) => text !== ''))]);
    // The letters are centred in their column, the header in bold.
    const letter = page?.cells.find((cell) => cell.text === 'R');
    const header = page?.cells.find((cell) => cell.text === 'Commercial');
    expect(letter && header && letter.x).toBeGreaterThan(header?.x ?? Infinity);
    expect(page?.cells.filter((cell) => cell.y === header?.y).every((cell) => cell.face === 'bold')).toBe(true);
  });

  it('repeats the header on every page, a step never cut between two pages', () => {
    const steps = Array.from({ length: 120 }, (_, index) => stepOf(`s${String(index)}`, 'l1', index, { label: `Étape numéro ${String(index)}` }));
    const data = table(mapOf({ lanes, steps, raci: [] }));
    expect(data.rows).toHaveLength(120);
    const pages = tablePages(data, { page: A3, measure, language: 'fr', top: 30 });
    expect(pages.length).toBeGreaterThan(2);
    for (const page of pages) {
      expect(headerOf(page)).toBe(data.headers.join('').replace(/\s/g, ''));
      expect(Math.max(...page.cells.map((cell) => cell.y))).toBeLessThanOrEqual(A3.height - PDF_MARGIN);
    }
    expect(pages.flatMap((page) => rowsOf(page).slice(1).map((row) => row[0]))).toEqual(data.rows.map((row) => row[0]));
  });
});

describe('the action plan as a table', () => {
  const named = (text: string): Schema<'NamedText'> => ({ text, source_language: 'fr', translations: {} });
  const line = (code: string, overrides: Partial<Schema<'ActionPlanLine'>> = {}): Schema<'ActionPlanLine'> => ({
    pain_point_id: code, code, description: 'Saisie en double', status: 'reported', lost_hours_per_month: 6, action: 'Lire l’extraction', owner: 'Service paie',
    due_date: '2026-12-15', map_id: 'm1', map: named('Demande de congés'), step_id: 's1', step_code: 'PRJ-E001', step: named('Extraire'), lane: named('Paie'), open: true,
    receiving_step_id: 's1', receiving_map_id: 'm1', document_id: null, can_fill: false, status_step_id: 's1', version: 1, status_changed_at: null, ...overrides,
  });
  const headers = ['Carte', 'Code', 'Étape', 'Couloir', 'Irritant', 'Description', 'Action', 'Responsable', 'Échéance', 'État', 'Heures perdues par mois'];
  const tableOf = (lines: Schema<'ActionPlanLine'>[]): TableData => planTable(planEntries({ lines, open_lost_hours_per_month: 0 }, 'fr', t), headers);

  it('gives the eleven cells of the entries in the order of the host, a long cell wrapped, the header on every page', () => {
    const long = 'Reprendre la requête de l’extraction pour tenir compte des nouveaux types de visites '.repeat(8).trim();
    const lines = [line('B', { action: long }), line('A', { lost_hours_per_month: null, owner: null }), ...Array.from({ length: 80 }, (_, index) => line(`C${String(index)}`))];
    const data = tableOf(lines);
    expect(data.headers).toEqual(headers);
    expect(data.rows.map((row) => row[4])).toEqual(['B', 'A', ...Array.from({ length: 80 }, (_, index) => `C${String(index)}`)]);
    expect(data.rows.every((row) => row.length === 11)).toBe(true);
    expect(data.rows[0]).toEqual(['Demande de congés', 'PRJ-E001', 'Extraire', 'Paie', 'B', 'Saisie en double', long, 'Service paie', '2026-12-15', 'signalé', 6]);
    const pages = tablePages(data, { page: A3, measure, language: 'fr', top: 30 });
    expect(pages.length).toBeGreaterThan(1);
    for (const page of pages) expect(headerOf(page)).toBe(headers.join('').replace(/\s/g, ''));
    // The long action runs over several lines in its column, within the page.
    const action = pages[0]?.cells.filter((cell) => cell.text.length > 10 && long.includes(cell.text) && cell.text !== long);
    expect(action?.length).toBeGreaterThan(2);
    expect(pages.flatMap((page) => page.cells).every((cell) => cell.x >= PDF_MARGIN && cell.x + measure(cell.text, cell.face, cell.size) <= A3.width - PDF_MARGIN + 1e-9)).toBe(true);
    // A line without hours or owner leaves those cells empty; the numbers are written.
    expect(pages[0]?.cells.some((cell) => cell.text === '6')).toBe(true);
  });

  it('a cell longer than a page is cut with an ellipsis so that its row is not lost', () => {
    const data = tableOf([line('X', { action: 'mot '.repeat(6000) }), line('Y')]);
    const pages = tablePages(data, { page: A3, measure, language: 'fr', top: 30 });
    expect(pages.flatMap((page) => page.cells).some((cell) => cell.text === 'Y')).toBe(true);
    expect(pages.flatMap((page) => page.cells).some((cell) => cell.text.endsWith('…'))).toBe(true);
    expect(pages.flatMap((page) => page.cells).every((cell) => cell.y <= A3.height - PDF_MARGIN)).toBe(true);
  });
});
