// ProposalPreview.test.tsx
// A proposal of the assistant on a map with content: its preview listed and marked on the steps, its translations and a manual one kept, « Appliquer » sent with the version of the preview and the map taken, « Écarter », nothing for a reader; a proposal for a new project opened with its map of origin (spec 3.0 sections 3.1-5 to 7, ADR 079, ADR 081, ADR 082).

import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { createMapEditor } from '../../map/state/editor.ts';
import { memoryStorage, renderMapScreen, showEditor, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi, type FakeReply } from '../../../test/fakeApi.ts';
import { MAP_ID, sampleMap } from '../../../test/maps.ts';

const STATUS = `GET /api/maps/${MAP_ID}/assistant`;
const PMAP = '{"version":"1.1"}';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const status = (canPropose: boolean): FakeReply => ({
  json: {
    mode: 'model', allowed: true, can_propose: canPropose, empty_map: false, shows_cost: false, project_name: 'Ventes', project_code: 'VTE',
    destination: { connector: 'anthropic', model: 'claude-opus-5-5', region: null, classification: 'internal', scope: 'project', map_name: 'Ventes' },
  },
});

const proposal: Schema<'AssistantReplyView'> = {
  form: 'proposal', message: 'Deux changements.', questions: [], findings: [], objects: [], raw: '{"form":"proposal"}', cost: null, duration_milliseconds: 0,
  proposal: { pmap: PMAP, project_code: 'VTE', project_name: 'Ventes', maps: 1, lanes: 2, steps: 4, flows: 3, sheets: 0, uncertainties: [], faults: [] },
};

const stream: FakeReply = { text: [{ type: 'started', destination: null }, { type: 'reply', reply: proposal }].map((line) => JSON.stringify(line)).join('\n') };

const preview: Schema<'ProposalPreviewView'> = {
  structure_version: 41, empty: false, warnings: ['step_kept:VTE-E009'], mode: 'in_place', reasons: [], missing: [],
  changes: [
    { kind: 'step', code: 'VTE-E002', id: 's2', text: 'Vérifier la demande complète', action: 'changed', fields: ['label'] },
    { kind: 'step', code: 'VTE-E003', id: 's3', text: 'Conforme ?', action: 'removed', fields: [] },
    { kind: 'step', code: null, id: null, text: 'Valider la demande', action: 'added', fields: [] },
  ],
};

async function proposed(canPropose = true, previewed: Schema<'ProposalPreviewView'> = preview) {
  const applied = { ...sampleMap(), steps: sampleMap().steps.filter((step) => step.id !== 's3') };
  const sent = fakeApi({
    [STATUS]: status(canPropose), [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: stream,
    [`POST /api/maps/${MAP_ID}/assistant/preview`]: { json: previewed },
    [`POST /api/maps/${MAP_ID}/assistant/apply`]: {
      json: { id: null, code: null, ids: [], operation_id: null, warnings: [], structure_version: 42, map: applied, history: { undo: [], redo: [] } },
    },
  });
  const shown = await showEditor(sampleMap());
  await userEvent.click(screen.getByTestId('ribbon-assistant'));
  await userEvent.type(await screen.findByTestId('assistant-field'), 'Restructure la carte.{Enter}');
  await screen.findByTestId('assistant-proposal');
  return { sent, ...shown };
}

describe('a proposal on a map with content', () => {
  it('previews its changes, marks the steps it changes or removes, and applies with the version of its preview', async () => {
    const { sent, editor, canvas } = await proposed();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    const list = await screen.findByTestId('assistant-changes');
    expect(list).toHaveTextContent('ModificationétapeVTE-E002Vérifier la demande complètelibellé');
    expect(list).toHaveTextContent('Suppression');
    expect(list).toHaveTextContent('VTE-E009 est détaillée : elle reste, avec sa carte.');
    expect(editor.store.getState().marks).toEqual({ s2: 'changed', s3: 'removed' });
    await waitFor(() => { expect(within(canvas).getByTestId('step-VTE-E002')).toHaveClass('is-ai-changed'); });
    await userEvent.click(screen.getByTestId('assistant-apply'));
    expect(await screen.findByTestId('assistant-applied')).toHaveTextContent('Ctrl+Z la défait d’un coup');
    const apply = callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/apply`)[0];
    expect(apply?.body).toEqual({ pmap: PMAP });
    expect(apply?.headers.get('If-Match')).toBe('"41"');
    expect(editor.store.getState().marks).toEqual({});
    expect(editor.store.getState().map?.steps.map((step) => step.id)).not.toContain('s3');
  }, 15_000);

  it('names the translations a proposal writes and the manual one it keeps, in each language of the interface', async () => {
    const translations: Schema<'ProposalPreviewView'> = {
      structure_version: 41, empty: false, warnings: ['translation_kept:VTE-E003'], mode: 'in_place', reasons: [], missing: [],
      changes: [{ kind: 'step', code: 'VTE-E002', id: 's2', text: 'Vérifier', action: 'changed', fields: ['translations'] }],
    };
    await proposed(true, translations);
    await userEvent.click(screen.getByTestId('assistant-preview'));
    const list = await screen.findByTestId('assistant-changes');
    expect(list).toHaveTextContent('ModificationétapeVTE-E002Vérifiertraductions');
    expect(list).toHaveTextContent('VTE-E003 garde sa traduction saisie à la main : la proposition ne la remplace pas.');
    await i18next.changeLanguage('en');
    await waitFor(() => { expect(screen.getByTestId('assistant-changes')).toHaveTextContent('VTE-E003 keeps the translation a person entered: the proposal does not replace it.'); });
    expect(screen.getByTestId('assistant-changes')).toHaveTextContent('VTE-E002Vérifiertranslations');
  });

  it('puts a proposal aside with its marks', async () => {
    const { editor } = await proposed();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    await screen.findByTestId('assistant-changes');
    await userEvent.click(screen.getByTestId('assistant-dismiss'));
    expect(screen.getByText('Proposition écartée.')).toBeInTheDocument();
    expect(editor.store.getState().marks).toEqual({});
  });

  it('offers to refine the proposal in the field', async () => {
    await proposed();
    await userEvent.click(screen.getByTestId('assistant-refine'));
    expect(screen.getByTestId('assistant-field')).toHaveValue('Garde la proposition, mais ');
    expect(screen.getByTestId('assistant-intent')).toHaveTextContent('Modifier la carte');
  });
});

describe('a proposal that changes the level of steps', () => {
  const flat: Schema<'ProposalPreviewView'> = {
    structure_version: 41, empty: false, warnings: [], changes: [], mode: 'new_project', reasons: ['raised:VTE-E005', 'raised:VTE-E006'],
    missing: [{ code: 'VTE-E004', label: 'Vérifier', detailed: true, has_sheet: true }],
  };

  it('is not applied: it says why, what the new project would not hold, and a facilitator downloads it', async () => {
    await proposed(true, flat);
    await userEvent.click(screen.getByTestId('assistant-preview'));
    const card = await screen.findByTestId('assistant-new-project');
    expect(card).toHaveTextContent('elle ne s’applique pas à cette carte. Elle s’ouvre en nouveau projet ; la carte d’origine reste intacte.');
    expect(card).toHaveTextContent('Remontent d’un niveau : VTE-E005, VTE-E006');
    expect(within(card).getByTestId('assistant-new-project-missing')).toHaveTextContent('VTE-E004Vérifierétape détaillée, sa fiche n’est pas reprise');
    expect(card).toHaveTextContent('1 étape de la carte et de ses niveaux ne sera pas dans le nouveau projet');
    expect(screen.queryByTestId('assistant-apply')).not.toBeInTheDocument();
    expect(card).toHaveTextContent('Seul un administrateur crée un projet');
  });

  it('opens as a new project for an administrator, with its map of origin', async () => {
    const sent = fakeApi({
      [STATUS]: status(true), [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: stream, [`POST /api/maps/${MAP_ID}/assistant/preview`]: { json: flat },
      'POST /api/projects/import-pmap': { status: 201, json: { id: 'p2', code: 'PLAT', root_map_id: 'm2', maps: 1, lanes: 1, steps: 4, flows: 3, sheets: 1, uncertainties: [] } },
    });
    const editor = createMapEditor({ source: sourceOf(sampleMap()), storage: memoryStorage(), admin: true });
    renderMapScreen({ mapId: MAP_ID, editor });
    await userEvent.click(await screen.findByTestId('ribbon-assistant'));
    await userEvent.type(await screen.findByTestId('assistant-field'), 'Ramène le processus sur un seul niveau.{Enter}');
    await userEvent.click(await screen.findByTestId('assistant-preview'));
    await userEvent.click(await screen.findByTestId('assistant-new-project-open'));
    const dialog = await screen.findByTestId('open-pmap');
    await userEvent.type(within(dialog).getByTestId('open-pmap-code'), 'PLAT');
    await userEvent.type(within(dialog).getByTestId('open-pmap-name'), 'Ventes à plat');
    await userEvent.click(screen.getByTestId('open-pmap-confirm'));
    await waitFor(() => { expect(callsTo(sent, 'POST', '/api/projects/import-pmap')).toHaveLength(1); });
    expect(callsTo(sent, 'POST', '/api/projects/import-pmap')[0]?.body).toEqual({ content: PMAP, code: 'PLAT', name: 'Ventes à plat', origin_map_id: MAP_ID });
  });
});

describe('a finding of an analysis', () => {
  it('asks the correction of the finding as a change of the map, for the facilitator', async () => {
    const findings: Schema<'AssistantReplyView'> = {
      form: 'findings', message: 'Un constat.', questions: [], proposal: null, raw: '{"form":"findings"}', cost: null, duration_milliseconds: 0, objects: [],
      findings: [{ kind: 'no_sheet', title: 'Étapes sans fiche', detail: 'Deux étapes.', objects: ['VTE-E002', 'VTE-E003'], suggestion: null }],
    };
    const line = (reply: Schema<'AssistantReplyView'>) => ({ text: JSON.stringify({ type: 'reply', reply }) });
    const sent = fakeApi({ [STATUS]: status(true), [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: [line(findings), line(proposal)] });
    await showEditor(sampleMap());
    await userEvent.click(screen.getByTestId('ribbon-assistant'));
    await userEvent.type(await screen.findByTestId('assistant-field'), 'Analyse.{Enter}');
    await userEvent.click(await screen.findByTestId('assistant-correct-finding'));
    await screen.findByTestId('assistant-proposal');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/exchanges`)[1]?.body).toMatchObject({
      intent: 'modify', request: 'Proposer la correction de ce constat : « Étapes sans fiche » (VTE-E002, VTE-E003), dans l’état voulu de la carte.',
    });
  });
});
