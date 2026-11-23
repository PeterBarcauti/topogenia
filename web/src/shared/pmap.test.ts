// pmap.test.ts
// The type of the .pmap of the interface is the published schema 1.5: a document that uses every property the type allows follows the schema, and the schema names no property the type lacks.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import Ajv2020 from 'ajv/dist/2020.js';
import { PMAP_FORMAT, type PmapDocument, type PmapMap } from './pmap.ts';

const schema = JSON.parse(readFileSync(join(import.meta.dirname, '..', '..', '..', 'schemas', 'pmap-1.5.schema.json'), 'utf8')) as Record<string, unknown>;

const detail: PmapMap = {
  lanes: [{ id: 's1', name: 'Gestion' }],
  steps: [{ id: 'e9', lane: 's1', label: 'Contrôler', column: 0 }],
  summary: { text: 'Le gestionnaire contrôle.', translations: { en: 'The clerk checks.' } },
};

/** A fictional document that writes every property of the format once at least. */
const complete: PmapDocument = {
  $schema: 'pmap-1.5.schema.json',
  format: PMAP_FORMAT,
  version: '1.5',
  generated_by: 'Essai',
  generated_at: '2026-11-16T20:00:00Z',
  project: {
    code: 'ESS', name: 'Essai', language: 'fr', description: 'Un essai.', translations: { en: 'Trial' },
    workday_hours: 7, fte_hours_per_month: 151.67, glossary: 'demande = request',
  },
  map: {
    lanes: [
      { id: 'c1', name: 'Demandeur', type: 'person', color: '#3366CC', headcount: 4, translations: { en: 'Requester' } },
      { id: 'c2', name: 'Logiciel', type: 'system' },
    ],
    steps: [
      { id: 'e1', lane: 'c1', label: 'Début', type: 'start', column: 0, volume: { quantity: 10, period: 'month' } },
      {
        id: 'e2', lane: 'c1', label: 'Saisir', type: 'task', column: 1, row: 0, automation: 'manual', value_class: 'waste',
        raci: { c2: 'I' }, translations: { en: 'Enter' }, comment: { text: 'On saisit.', translations: { en: 'It is entered.' } }, sub_map: detail,
        sheet: {
          actor: 'Le demandeur', purpose: 'Demander', tools: 'Tableur', frequency: 'weekly', duration_minutes: 10, duration_min_minutes: 5,
          duration_max_minutes: 20, lead_time: { value: 2, unit: 'hour' }, procedure: [{ text: 'Remplir', tool: 'Tableur', nature: 're_enter' }],
          free_text: 'Rien.', outputs: [{ id: 'd1', name: 'Demande', location: 'P:\\Achats\\demande.docx', share_confirmed: true, medium: 'email' }],
          inputs: [{ name: 'Besoin', wait_hours: 4, medium: 'paper' }],
        },
      },
      {
        id: 'e3', lane: 'c2', label: 'Traiter', type: 'task', column: 2, automation: 'automated',
        sheet: {
          uses: [{
            document: 'd1', wait_hours: 1, percent_complete_accurate: 80, pain_point: 'Incomplète', pain_point_code: 'ESS-I001',
            pain_point_status: 'reported', lost_hours_per_month: 2, action: 'Un modèle', owner: 'Le service', due_date: '2026-12-01',
          }],
        },
      },
      { id: 'e4', lane: 'c2', label: 'Fin', type: 'end', column: 3 },
    ],
    flows: [
      { source: 'e1', target: 'e2', label: 'oui', share: 50, translations: { en: 'yes' }, comment: { text: 'Ensuite.' }, route: { from: 'right', to: 'left', via: { aisle: 0 } } },
      { source: 'e2', target: 'e3', route: { via: { column: 1 } } },
      { source: 'e3', target: 'e4', route: { via: { lane: 'c2', row: 0, edge: 'top' } } },
    ],
  },
  uncertainties: ['À vérifier.'],
};

/** Every property name the schema declares, in its root and in its definitions. */
function schemaProperties(node: unknown, names = new Set<string>()): Set<string> {
  if (Array.isArray(node)) node.forEach((item) => schemaProperties(item, names));
  else if (typeof node === 'object' && node !== null) {
    for (const [key, value] of Object.entries(node as Record<string, unknown>)) {
      if (key === 'properties' && typeof value === 'object' && value !== null) Object.keys(value).forEach((name) => names.add(name));
      schemaProperties(value, names);
    }
  }
  return names;
}

/** Every key a document writes, at any depth. */
function documentKeys(node: unknown, keys = new Set<string>()): Set<string> {
  if (Array.isArray(node)) node.forEach((item) => documentKeys(item, keys));
  else if (typeof node === 'object' && node !== null)
    for (const [key, value] of Object.entries(node)) {
      keys.add(key);
      documentKeys(value, keys);
    }
  return keys;
}

describe('the type of the .pmap', () => {
  it('writes a document that follows the published schema 1.5', () => {
    const validate = new Ajv2020({ allErrors: true, strictTypes: false }).compile(schema);
    expect(validate(complete), JSON.stringify(validate.errors)).toBe(true);
  });

  it('knows every property the schema declares', () => {
    const written = documentKeys(complete);
    expect([...schemaProperties(schema)].filter((name) => !written.has(name))).toEqual([]);
  });
});
