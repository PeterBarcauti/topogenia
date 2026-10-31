// translate.test.tsx
// A translation of the whole process by the assistant (ADR 083): asked from the dialog of the translations into a language of the list, on the root map; its preview shows its texts in place on the map, in its language, sums them up and lists them beside their originals, nothing written; « Écarter » gives the map back; its application takes only the translations into that language; once applied the process is shown in it.

import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { processLanguage } from '../../../shared/state/processLanguage.ts';
import { memoryStorage, renderMapScreen, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, sampleMap } from '../../../test/maps.ts';
import { createMapEditor } from '../../map/state/editor.ts';

const PMAP = '{"version":"1.1","translations":"pt"}';

const proposal: Schema<'AssistantReplyView'> = {
  form: 'proposal', message: 'Tudo traduzido.', questions: [], findings: [], objects: [], raw: '{"form":"proposal"}', cost: null, duration_milliseconds: 0,
  proposal: { pmap: PMAP, project_code: 'VTE', project_name: 'Ventes', maps: 1, lanes: 2, steps: 4, flows: 3, sheets: 0, uncertainties: [], faults: [] },
};

const preview: Schema<'ProposalPreviewView'> = {
  structure_version: 41, empty: false, warnings: ['translation_kept:VTE-E001'], mode: 'in_place', reasons: [], missing: [],
  changes: [{ kind: 'step', code: 'VTE-E002', id: 's2', text: 'Vérifier la demande', action: 'changed', fields: ['translations'] }],
  translations: [
    { kind: 'lane', id: 'l1', code: 'VTE-C01', original: 'Commercial', text: 'Comercial', kept: false },
    { kind: 'step', id: 's1', code: 'VTE-E001', original: 'Demande reçue', text: 'Pedido recebido à mão', kept: true },
    { kind: 'step', id: 's2', code: 'VTE-E002', original: 'Vérifier la demande', text: 'Verificar o pedido', kept: false },
    { kind: 'flow', id: 'f3', code: 'VTE-E003 > VTE-E004', original: 'oui', text: 'sim', kept: false },
  ],
};

/** The project of the sample map, for a facilitator, and an assistant that answers the translation with a proposal. */
function translationApi(more: Parameters<typeof fakeApi>[0] = {}) {
  return fakeApi({
    [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VTE', name: 'Ventes', source_language: 'fr', name_translations: {}, description: null, shared_folder: null,
      classification: 'internal', sharing_group_label: null, translation_glossary: null, root_map_id: MAP_ID, role: 'facilitator', members: [], version: 4 } },
    [`GET /api/maps/${MAP_ID}/assistant`]: { json: {
      mode: 'model', allowed: true, can_propose: true, empty_map: false, shows_cost: false, project_name: 'Ventes', project_code: 'VTE',
      destination: { connector: 'anthropic', model: 'claude-opus-5-5', region: null, classification: 'internal', scope: 'project', map_name: 'Ventes' },
    } },
    [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: { text: [{ type: 'started', destination: null }, { type: 'reply', reply: proposal }].map((line) => JSON.stringify(line)).join('\n') },
    [`POST /api/maps/${MAP_ID}/assistant/preview`]: { json: preview },
    ...more,
  });
}

/** The editor of the sample map in French, a translation into Portuguese asked from the dialog and its proposal received. */
async function askedIntoPortuguese() {
  const editor = createMapEditor({ source: sourceOf(sampleMap()), storage: memoryStorage(), languages: ['fr', 'en', 'pt'], admin: true });
  renderMapScreen({ mapId: MAP_ID, editor });
  await screen.findByTestId('map-canvas');
  editor.floating.getState().open({ kind: 'translations' });
  await userEvent.click(await screen.findByTestId('translations-assistant-pt'));
  await userEvent.click(screen.getByRole('button', { name: 'Traduire en Português' }));
  await screen.findByTestId('assistant-proposal');
  return editor;
}

const labels = () => screen.getAllByTestId('step-label').map((label) => label.textContent);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  processLanguage.setState({ chosen: {} });
});

afterEach(() => {
  vi.unstubAllGlobals();
  processLanguage.setState({ chosen: {} });
});

describe('a translation by the assistant (ADR 083)', () => {
  it('asked into Portuguese from the dialog, previewed and applied with its language on the root map, then the process is shown in Portuguese', async () => {
    const translated = sampleMap({ languages: { origin: 'fr', total: 9, written: { fr: 9, en: 3, pt: 9 } } });
    const sent = translationApi({
      [`POST /api/maps/${MAP_ID}/assistant/apply`]: {
        json: { id: null, code: null, ids: [], operation_id: null, warnings: [], structure_version: 42, map: translated, history: { undo: [], redo: [] } },
      },
    });
    const editor = await askedIntoPortuguese();
    expect(editor.assistant.getState().open).toBe(true);
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/exchanges`)[0]?.body)
      .toMatchObject({ intent: 'translate', request: 'Traduire tout le processus en Português.', selection: [], language: 'pt' });
    await userEvent.click(screen.getByTestId('assistant-preview'));
    await screen.findByTestId('assistant-translation');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/preview`)[0]?.body).toEqual({ pmap: PMAP, language: 'pt' });
    // From the preview to the translation written, the map stays in Portuguese: it never comes back to French between.
    const languages: string[] = [];
    const stop = editor.store.subscribe((state) => { languages.push(state.shown?.language ?? ''); });
    await userEvent.click(screen.getByTestId('assistant-apply'));
    await screen.findByTestId('assistant-applied');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/apply`)[0]?.body).toEqual({ pmap: PMAP, language: 'pt' });
    expect(processLanguage.getState().chosen[PROJECT_ID]).toBe('pt');
    await waitFor(() => {
      expect(editor.store.getState().shown).toEqual({ language: 'pt', origin: 'fr', locked: true });
    });
    expect([editor.store.getState().previewed, screen.queryByTestId('map-translation-preview')]).toEqual([null, null]);
    stop();
    expect(new Set(languages)).toEqual(new Set(['pt']));
  }, 15_000);
});

describe('the preview of a translation by the assistant (ADR 083)', () => {
  it('shows its texts in place in its language without frames, the map locked, sums them up and lists them beside their originals', async () => {
    translationApi();
    const editor = await askedIntoPortuguese();
    expect(screen.getByTestId('assistant-proposal').querySelector('.proposal-counts')).toBeNull();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    const card = await screen.findByTestId('assistant-translation');

    expect(labels()).toEqual(['Pedido recebido à mão', 'Verificar o pedido', 'Conforme ?', 'Facture émise']);
    expect(editor.store.getState().marks).toEqual({});
    expect(document.querySelector('.is-ai-changed')).toBeNull();
    expect(editor.store.getState().shown).toEqual({ language: 'pt', origin: 'fr', locked: true });
    expect([editor.store.getState().map?.rights.structure, editor.store.getState().translating]).toEqual([false, false]);
    expect(screen.getByTestId('map-translation-preview').textContent).toContain('Aperçu de la traduction en Português : rien n’est encore écrit.');
    expect(screen.queryByTestId('map-translation')).toBeNull();

    expect(within(card).getByText('4 textes traduits en Português, affichés sur la carte.')).toBeTruthy();
    expect(screen.getByTestId('assistant-translation-kept').textContent).toBe('1 traduction saisie à la main est gardée : la proposition ne la remplace pas.');
    expect(screen.queryByTestId('assistant-changes')).toBeNull();
    expect(screen.queryByTestId('assistant-refine')).toBeNull();
    expect(screen.queryByTestId('assistant-create-project')).toBeNull();
    expect([screen.getByTestId('assistant-apply'), screen.getByTestId('assistant-dismiss'), screen.getByTestId('assistant-download')].every(Boolean)).toBe(true);
    await userEvent.click(screen.getByTestId('assistant-translation-review'));
    const rows = within(card).getAllByRole('row').slice(1).map((row) => [...row.querySelectorAll('td')].map((cell) => cell.textContent));
    expect(rows).toEqual([
      ['VTE-C01 Commercial', 'Comercial'],
      ['VTE-E001 Demande reçue', 'Pedido recebido à mãosaisie à la main, gardée'],
      ['VTE-E002 Vérifier la demande', 'Verificar o pedido'],
      ['VTE-E003 > VTE-E004 oui', 'sim'],
    ]);
    expect(within(card).getAllByRole('columnheader').map((header) => header.textContent)).toEqual(['Original', 'Português']);
  });

  it('put aside, gives the map back as it is, in the language it was shown in', async () => {
    translationApi();
    const editor = await askedIntoPortuguese();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    await screen.findByTestId('assistant-translation');
    await userEvent.click(screen.getByTestId('assistant-dismiss'));
    expect(editor.store.getState().previewed).toBeNull();
    expect(editor.store.getState().shown).toEqual({ language: 'fr', origin: 'fr', locked: false });
    expect(labels()).toEqual(['Demande reçue', 'Vérifier la demande', 'Conforme ?', 'Facture émise']);
    expect(screen.queryByTestId('map-translation-preview')).toBeNull();
    expect(editor.store.getState().map?.rights.structure).toBe(true);
  });

  it('with the panel closed, gives the map back as it is', async () => {
    translationApi();
    const editor = await askedIntoPortuguese();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    await screen.findByTestId('assistant-translation');
    expect(editor.store.getState().previewed?.language).toBe('pt');
    act(() => {
      editor.assistant.getState().setOpen(false);
    });
    await waitFor(() => {
      expect(editor.store.getState().previewed).toBeNull();
    });
    expect(labels()).toEqual(['Demande reçue', 'Vérifier la demande', 'Conforme ?', 'Facture émise']);
  });

  it('without a text in its language, says so and offers no application', async () => {
    translationApi({ [`POST /api/maps/${MAP_ID}/assistant/preview`]: { json: { ...preview, translations: [] } } });
    await askedIntoPortuguese();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    expect((await screen.findByTestId('assistant-translation-empty')).textContent).toBe('La réponse ne donne aucune traduction en Português : rien à appliquer.');
    expect(screen.queryByTestId('assistant-apply')).toBeNull();
  });
});
