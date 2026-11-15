// tableImport.test.ts
// A table opened as a process (plan 3.1, L5): a CSV read whatever its separator, its quotes and its encoding; the columns of Visio Data Visualizer, those written in French with the roles and the delays, and the workbook the application exports read again; what is guessed said; every file written valid for the published schema of the .pmap 1.2.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import Ajv2020 from 'ajv/dist/2020.js';
import { workbookSheets } from '../../map/export/workbook.ts';
import { i18next, loadLanguage, type Translate } from '../../../shared/i18n/i18n.ts';
import { sampleMap } from '../../../test/maps.ts';
import { leadTimeOf, plainText, TableError, tableToPmap } from './tableImport.ts';
import { cellText, csvRows, decodeCsv, separatorOf } from './tableRead.ts';

const t: Translate = (key, options) => i18next.t(key, options);
const schema = JSON.parse(readFileSync(join(import.meta.dirname, '..', '..', '..', '..', '..', 'schemas', 'pmap-1.2.schema.json'), 'utf8')) as object;
const validate = new Ajv2020({ allErrors: true, strictTypes: false }).compile(schema);

interface Pmap {
  readonly project: { readonly name: string };
  readonly map: {
    readonly lanes: readonly { id: string; name: string }[];
    readonly steps: readonly { id: string; lane: string; label: string; type?: string; raci?: Record<string, string>; sheet?: { lead_time: { value: number; unit: string } } }[];
    readonly flows: readonly { source: string; target: string; label?: string }[];
  };
  readonly uncertainties?: readonly string[];
}

/** The table read as a .pmap, checked against the published schema first. */
function open(rows: readonly (readonly string[])[], name = 'Achats', letters = 'ASCI'): Pmap {
  const text = tableToPmap([{ name: 'Feuil1', rows }], name, t, letters);
  const file = JSON.parse(text) as Pmap;
  expect(validate(file), JSON.stringify(validate.errors)).toBe(true);
  return file;
}

const label = (file: Pmap, id: string) => file.map.steps.find((step) => step.id === id)?.label;
const flowsOf = (file: Pmap) => file.map.flows.map((flow) => `${label(file, flow.source) ?? ''}>${label(file, flow.target) ?? ''}${flow.label === undefined ? '' : ` (${flow.label})`}`);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('a CSV read (RFC 4180)', () => {
  it('takes the separator its first line uses most, its quoted fields, their doubled quotes and their line ends', () => {
    expect([separatorOf('a;b;c'), separatorOf('a,b,c'), separatorOf('a\tb'), separatorOf('"a;b",c,d')]).toEqual([';', ',', '\t', ',']);
    expect(csvRows('ID;Étape\r\n1;"Saisir ; vérifier"\n2;"Dire ""oui""\nensuite"\n\n')).toEqual([['ID', 'Étape'], ['1', 'Saisir ; vérifier'], ['2', 'Dire "oui"\nensuite']]);
  });

  it('reads UTF-8, its mark of order left out, or else Windows-1252, as Excel writes it', () => {
    expect(decodeCsv(new TextEncoder().encode('﻿Étape;Couloir').buffer)).toBe('Étape;Couloir');
    expect(decodeCsv(new Uint8Array([0xc9, 0x74, 0x61, 0x70, 0x65]).buffer)).toBe('Étape');
    expect([cellText({ result: 4 }), cellText({ richText: [{ text: 'a' }, { text: 'b' }] }), cellText({ text: 'lien', hyperlink: 'x' }), cellText(null)]).toEqual(['4', 'ab', 'lien', '']);
  });
});

describe('a table of Visio Data Visualizer (plan 3.1, L5)', () => {
  it('opens with its lanes in their order, its shapes as types, its flows and their labels, its phase set aside', () => {
    const file = open([
      ['Process Step ID', 'Process Step Description', 'Next Step ID', 'Connector Label', 'Shape Type', 'Function', 'Phase'],
      ['P1', 'Request received', 'P2', '', 'Start', 'Requester', 'One'],
      ['P2', 'Check the request', 'P3', '', 'Process', 'Manager', 'One'],
      ['P3', 'Complete?', 'P4,P2', 'Yes,No', 'Decision', 'Manager', 'Two'],
      ['P4', 'Request filed', '', '', 'End', 'Clerk', 'Two'],
    ]);
    expect(file.map.lanes.map((lane) => lane.name)).toEqual(['Requester', 'Manager', 'Clerk']);
    expect(file.map.steps.map((step) => step.type ?? 'task')).toEqual(['start', 'task', 'decision', 'end']);
    expect(flowsOf(file)).toEqual(['Request received>Check the request', 'Check the request>Complete?', 'Complete?>Request filed (Yes)', 'Complete?>Check the request (No)']);
    expect(file.uncertainties).toEqual(['La colonne Phase n’a pas d’équivalent sur la carte : elle est laissée de côté.']);
  });

  it('a shape « Start/End » is a start without arrow in, an end otherwise', () => {
    const file = open([['ID', 'Étape', 'Suivant', 'Type'], ['1', 'Début', '2', 'Start/End'], ['2', 'Faire', '3', ''], ['3', 'Fin', '', 'Début/Fin']]);
    expect(file.map.steps.map((step) => step.type ?? 'task')).toEqual(['start', 'task', 'end']);
  });
});

describe('a table written in French (plan 3.1, L5)', () => {
  it('takes the roles of the other lanes, by a column per letter or one column RACI, and the delays in hours, minutes or days', () => {
    const file = open([
      ['ID', 'Étape', 'Couloir', 'Suivant', 'Temps', 'A', 'C', 'RACI'],
      ['1', 'Saisir', 'Demandeur', '2', '2,5', 'Manager', '', ''],
      ['2', 'Valider', 'Manager', '3', '30 min', '', 'RH, Juridique', ''],
      ['3', 'Classer', 'RH', '', '1 j', '', '', 'A : Direction ; I : Demandeur'],
    ]);
    expect(file.map.lanes.map((lane) => lane.name)).toEqual(['Demandeur', 'Manager', 'RH', 'Juridique', 'Direction']);
    const id = (name: string) => file.map.lanes.find((lane) => lane.name === name)?.id ?? '';
    expect(file.map.steps.map((step) => step.raci)).toEqual([{ [id('Manager')]: 'A' }, { [id('RH')]: 'C', [id('Juridique')]: 'C' }, { [id('Direction')]: 'A', [id('Demandeur')]: 'I' }]);
    expect(file.map.steps.map((step) => step.sheet?.lead_time)).toEqual([{ value: 2.5, unit: 'hour' }, { value: 30, unit: 'minute' }, { value: 1, unit: 'day' }]);
  });

  it('says what it guessed or set aside: a key twice, a next step unknown, a type unknown, a delay unreadable, a label missing or too long', () => {
    const file = open([
      ['ID', 'Étape', 'Couloir', 'Suivant', 'Type', 'Temps'],
      ['1', 'x'.repeat(160), 'A', '2,9', 'Rectangle', 'bientôt'],
      ['1', 'Encore', 'A', '', '', ''],
      ['2', '', 'B', '', '', ''],
    ]);
    expect(file.map.steps).toHaveLength(2);
    expect(file.map.steps[0]?.label).toHaveLength(150);
    expect(file.uncertainties).toEqual([
      'Ligne 3 : l’identifiant 1 est déjà pris, la ligne est laissée de côté.',
      'Ligne 2 : l’étape suivante 9 n’est pas dans le tableau, la flèche est laissée de côté.',
      'Ligne 2 : le libellé de l’étape est coupé à 150 caractères.',
      'Ligne 2 : type « Rectangle » inconnu, l’étape devient une tâche.',
      'Ligne 2 : délai « bientôt » illisible (par exemple 2 h, 30 min, 1 j), il est laissé de côté.',
      'Ligne 4 : l’étape n’a pas de libellé, elle porte son identifiant.',
    ]);
  });

  it('refuses a table of no known column, or of no step', () => {
    expect(() => tableToPmap([{ name: 'x', rows: [['Nom', 'Prénom'], ['Martin', 'Léa']] }], 'x', t)).toThrow(TableError);
    expect(() => tableToPmap([{ name: 'x', rows: [['ID', 'Étape', 'Couloir']] }], 'x', t)).toThrow(new TableError('files.table.empty'));
    expect([leadTimeOf('3'), leadTimeOf('4 heures'), leadTimeOf('2 days'), leadTimeOf('vite')]).toEqual([{ value: 3, unit: 'hour' }, { value: 4, unit: 'hour' }, { value: 2, unit: 'day' }, null]);
  });
});

describe('the workbook the application exports, read again (plan 3.1, L5)', () => {
  it('gives back its lanes, steps, types, flows, labels and roles', () => {
    const map = sampleMap({ raci: [{ step_id: 's2', lane_id: 'l2', role: 'C' }] });
    const sheets = workbookSheets(map, 'fr', t).map((sheet) => ({ name: sheet.name, rows: [sheet.headers, ...sheet.rows.map((row) => row.map(String))] }));
    const file = JSON.parse(tableToPmap(sheets, 'Visites', t)) as Pmap;
    expect(validate(file), JSON.stringify(validate.errors)).toBe(true);
    expect(file.map.lanes.map((lane) => lane.name)).toEqual(['Commercial', 'Comptabilité']);
    expect(file.map.steps.map((step) => [step.label, step.type ?? 'task'])).toEqual([
      ['Demande reçue', 'start'], ['Vérifier la demande', 'task'], ['Conforme ?', 'decision'], ['Facture émise', 'end'],
    ]);
    expect(flowsOf(file)).toEqual(['Demande reçue>Vérifier la demande', 'Vérifier la demande>Conforme ?', 'Conforme ?>Vérifier la demande (non)', 'Conforme ?>Facture émise (oui)']);
    const checked = file.map.steps.find((step) => step.label === 'Vérifier la demande');
    expect(checked?.raci).toEqual({ [file.map.lanes.find((lane) => lane.name === 'Comptabilité')?.id ?? '']: 'C' });
  });
});

describe('what the host would refuse, set aside and said (plan 3.1, E11)', () => {
  it('keeps one approver and the letters of the installation only, and reads the parts of RACI on several lines', () => {
    const rows = [['ID', 'Étape', 'Couloir', 'A', 'S', 'RACI'], ['1', 'Saisir', 'Visites', 'Direction, Finance', 'RH', 'C : Juridique\r\nI : Visites\r\nqui ?']];
    const file = open(rows, 'Achats', 'ACI');
    const id = (name: string) => file.map.lanes.find((lane) => lane.name === name)?.id ?? '';
    expect(file.map.steps[0]?.raci).toEqual({ [id('Direction')]: 'A', [id('Juridique')]: 'C' });
    expect(file.uncertainties).toEqual([
      'Ligne 2 : le rôle S n’est pas utilisé dans cette installation, il est laissé de côté.',
      'Ligne 2 : plus d’un approbateur (A) ; une étape n’en prend qu’un.',
      'Ligne 2 : « qui ? » ne se lit pas comme un rôle RACI (écrire par exemple « A : Direction »), il est laissé de côté.',
    ]);
    expect(open(rows).map.steps[0]?.raci).toEqual(expect.objectContaining({ [id('Direction')]: 'A' }));
    expect(Object.values(open(rows).map.steps[0]?.raci ?? {})).toContain('S');
  });

  it('cuts what it quotes in an uncertainty, a delay out of the format, a key made label, and spaces a tag made by a control character', () => {
    const file = open([['ID', 'Étape', 'Couloir', 'Type', 'Temps'], ['k'.repeat(200), '', 'A', 'x'.repeat(600), '200000'], ['2', 'Fin', 'A', 'constructor', '']]);
    expect(file.uncertainties?.every((text) => text.length <= 500)).toBe(true);
    expect([file.map.steps[0]?.label.length, file.map.steps[0]?.sheet, file.map.steps[1]?.type]).toEqual([150, undefined, undefined]);
    expect(file.uncertainties?.filter((text) => text.includes('inconnu'))).toHaveLength(2);
    expect([plainText('x<\u0007b'), plainText('<b>Gras</b>'), leadTimeOf('1'.repeat(400)), leadTimeOf('100000 min')]).toEqual(['x< b', 'Gras', null, { value: 100000, unit: 'minute' }]);
  });

  it('names the line of the file after empty rows, and reads a duration and a date of Excel', () => {
    const rows = csvRows('ID;Étape;Couloir;Type\n\n1;Saisir;A;Rectangle\n\n\n');
    expect(rows).toEqual([['ID', 'Étape', 'Couloir', 'Type'], [], ['1', 'Saisir', 'A', 'Rectangle']]);
    expect(open(rows).uncertainties).toEqual(['Ligne 3 : type « Rectangle » inconnu, l’étape devient une tâche.']);
    expect([cellText(new Date(Date.UTC(1899, 11, 30, 2, 30))), cellText(new Date(Number.NaN)), cellText(new Date(Date.UTC(2026, 10, 6)))]).toEqual(['2.5 h', '', '2026-11-06']);
  });

  it('reads the workbook it exports in an interface of another script, and never takes an empty header for a column', async () => {
    await loadLanguage('ru');
    await i18next.changeLanguage('ru');
    expect(t('raci.column_code')).toBe('Код');
    const sheets = workbookSheets(sampleMap(), 'fr', t).map((sheet) => ({ name: sheet.name, rows: [sheet.headers, ...sheet.rows.map((row) => row.map(String))] }));
    const file = JSON.parse(tableToPmap(sheets, 'Visites', t)) as Pmap;
    expect([file.map.steps.length, file.map.flows.length]).toEqual([4, 4]);
    const csv = JSON.parse(tableToPmap([{ name: 'x', rows: [['ID', 'Etape', 'Couloir', 'Next', ''], ['1', 'A', 'L', '2', ''], ['2', 'B', 'L', '', '']] }], 'x', t)) as Pmap;
    expect(csv.map.flows).toHaveLength(1);
  });
});