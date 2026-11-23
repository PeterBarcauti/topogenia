// bpmnDocumentation.test.ts
// The bounds of the duration of a step in its BPMN documentation agree with the number before the word « minutes » (F-205).

import { i18next, loadLanguage, type Translate } from '../../../../shared/i18n/i18n.ts';
import { PMAP_FORMAT, type PmapDocument } from '../../../../shared/pmap.ts';
import { bpmnOf } from './bpmnCompile.ts';

const t: Translate = (key, options) => i18next.t(key, options);

/** The documentation of the one step of a small map once its sheet is the one given, in a language. */
async function documentationOf(language: string, sheet: object): Promise<string> {
  await loadLanguage(language);
  await i18next.changeLanguage(language);
  const document: PmapDocument = {
    format: PMAP_FORMAT, version: '1.5', project: { code: 'CLO', name: 'Clôture', language: 'fr', workday_hours: 7 },
    map: { lanes: [{ id: 'CLO-C01', name: 'Comptable' }], steps: [{ id: 'CLO-E002', lane: 'CLO-C01', label: 'Extraire la balance', column: 1, sheet }], flows: [] },
  };
  const options = { language, withSubProcesses: true, withDetails: true, mapTitle: 'Clôture', classification: 'Interne', summary: null, exporterVersion: '3.3.0', t };
  const node = bpmnOf(document, options).process.nodes.find((candidate) => candidate.stepId === 'CLO-E002');
  return node?.documentation ?? '';
}

describe('the documentation of a step in the BPMN diagram', () => {
  it('writes « au moins 1 minute », « au plus 1 minute », « 0.5 à 1 minute », « 1 à 2 minutes »', async () => {
    expect(await documentationOf('fr', { duration_min_minutes: 1 })).toMatch(/au moins 1 minute$/m);
    expect(await documentationOf('fr', { duration_max_minutes: 1 })).toMatch(/au plus 1 minute$/m);
    expect(await documentationOf('fr', { duration_min_minutes: 0.5, duration_max_minutes: 1 })).toMatch(/0\.5 à 1 minute$/m);
    expect(await documentationOf('fr', { duration_min_minutes: 1, duration_max_minutes: 2 })).toMatch(/1 à 2 minutes$/m);
  });

  it('writes « at least 1 minute » and « 1 to 2 minutes » in English', async () => {
    expect(await documentationOf('en', { duration_min_minutes: 1 })).toMatch(/at least 1 minute$/m);
    expect(await documentationOf('en', { duration_min_minutes: 1, duration_max_minutes: 2 })).toMatch(/1 to 2 minutes$/m);
  });
});
