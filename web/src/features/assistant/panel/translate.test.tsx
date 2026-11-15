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
  proposal: { pmap: PMAP, project_code: 'VIS', project_name: 'Visites', maps: 1, lanes: 2, steps: 4, flows: 3, sheets: 0, uncertainties: [], faults: [] },
};

const preview: Schema<'ProposalPreviewView'> = {
  structure_version: 41, empty: false, warnings: ['translation_kept:VIS-E001'], mode: 'in_place', reasons: [], missing: [],
  changes: [{ kind: 'step', code: 'VIS-E002', id: 's2', text: 'Vérifier la demande', action: 'changed', fields: ['translations'] }],
  translations: [
    { kind: 'lane', id: 'l1', code: 'VIS-C01', original: 'Commercial', text: 'Comercial', kept: false },
    { kind: 'step', id: 's1', code: 'VIS-E001', original: 'Demande reçue', text: 'Pedido recebido à mão', kept: true },
    { kind: 'step', id: 's2', code: 'VIS-E002', original: 'Vérifier la demande', text: 'Verificar o pedido', kept: false },
    { kind: 'flow', id: 'f3', code: 'VIS-E003 > VIS-E004', original: 'oui', text: 'sim', kept: false },
  ],
};

/** The project of the sample map, for a facilitator, and an assistant that answers the translation with a proposal. */
function translationApi(more: Parameters<typeof fakeApi>[0] = {}) {
  return fakeApi({
    [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', name: 'Visites', source_language: 'fr', name_translations: {}, description: null, shared_folder: null,
      classification: 'internal', sharing_group_label: null, translation_glossary: null, root_map_id: MAP_ID, role: 'facilitator', members: [], version: 4 } },
    [`GET /api/maps/${MAP_ID}/assistant`]: { json: {
      mode: 'model', allowed: true, can_propose: true, empty_map: false, shows_cost: false, project_name: 'Visites', project_code: 'VIS',
      destination: { connector: 'anthropic', model: 'claude-opus-5-5', region: null, classification: 'internal', scope: 'project', map_name: 'Visites' },
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

describe('a translation that failed (audit 5.2)', () => {
  it('« Réessayer » asks the same translation again: its language and the root map, never a change of the structure', async () => {
    const failed = [{ type: 'started', destination: null }, { type: 'failure', failure: 'timeout' }].map((line) => JSON.stringify(line)).join('\n');
    const sent = translationApi({ [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: { text: failed } });
    const editor = createMapEditor({ source: sourceOf(sampleMap()), storage: memoryStorage(), languages: ['fr', 'en', 'pt'], admin: true });
    renderMapScreen({ mapId: MAP_ID, editor });
    await screen.findByTestId('map-canvas');
    editor.floating.getState().open({ kind: 'translations' });
    await userEvent.click(await screen.findByTestId('translations-assistant-pt'));
    await userEvent.click(screen.getByRole('button', { name: 'Traduire en Português' }));
    await userEvent.click(await screen.findByTestId('assistant-retry'));
    await waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/exchanges`)).toHaveLength(2);
    });
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/exchanges`)[1]?.body)
      .toMatchObject({ intent: 'translate', request: 'Traduire tout le processus en Português.', selection: [], language: 'pt' });
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
      ['VIS-C01 Commercial', 'Comercial'],
      ['VIS-E001 Demande reçue', 'Pedido recebido à mãosaisie à la main, gardée'],
      ['VIS-E002 Vérifier la demande', 'Verificar o pedido'],
      ['VIS-E003 > VIS-E004 oui', 'sim'],
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

describe('the comments of presentation a translation brings (ADR 099 revised)', () => {
  const comment = { step_id: 's2', code: 'VIS-E002', original: 'Le commercial vérifie la demande.', text: 'O comercial verifica o pedido.' };
  const kept = { step_id: 's1', code: 'VIS-E001', original: 'La demande arrive.', text: 'O pedido chega.' };
  const sentBack = [comment, kept].map(({ step_id, original, text }) => ({ step_id, original, text }));
  const withComments: Schema<'ProposalPreviewView'> = {
    ...preview,
    translations: [
      ...(preview.translations ?? []),
      { kind: 'presentation_comment', id: 's2', code: 'VIS-E002', original: comment.original, text: comment.text, kept: false },
      { kind: 'presentation_comment', id: 's1', code: 'VIS-E001', original: kept.original, text: 'Escrito à mão.', kept: true },
    ],
  };

  function commentedApi() {
    const reply = { ...proposal, comments: [comment, kept] };
    return translationApi({
      [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: { text: [{ type: 'started', destination: null }, { type: 'reply', reply }].map((line) => JSON.stringify(line)).join('\n') },
      [`POST /api/maps/${MAP_ID}/assistant/preview`]: { json: withComments },
      [`POST /api/maps/${MAP_ID}/assistant/apply`]: {
        json: { id: null, code: null, ids: [], operation_id: null, warnings: [], structure_version: 42, map: sampleMap(), history: { undo: [], redo: [] } },
      },
    });
  }

  it('go with the preview and the application, are counted apart and marked in the review, and are never shown as the label of their step', async () => {
    const sent = commentedApi();
    await askedIntoPortuguese();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    const card = await screen.findByTestId('assistant-translation');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/preview`)[0]?.body).toEqual({ pmap: PMAP, language: 'pt', comments: sentBack });
    // The steps keep the translation of their own label: a comment is said in a presentation, not written on the map.
    expect(labels()).toEqual(['Pedido recebido à mão', 'Verificar o pedido', 'Conforme ?', 'Facture émise']);
    expect(within(card).getByText('4 textes traduits en Português, affichés sur la carte.')).toBeTruthy();
    expect(screen.getByTestId('assistant-translation-comments').textContent).toBe('Commentaires de présentation : 2');
    expect(screen.getByTestId('assistant-translation-kept').textContent).toBe('2 traductions saisies à la main sont gardées : la proposition ne les remplace pas.');
    await userEvent.click(screen.getByTestId('assistant-translation-review'));
    const rows = within(card).getAllByRole('row').slice(1).map((row) => [...row.querySelectorAll('td')].map((cell) => cell.textContent));
    expect(rows.slice(-2)).toEqual([
      ['VIS-E002commentaire Le commercial vérifie la demande.', 'O comercial verifica o pedido.'],
      ['VIS-E001commentaire La demande arrive.', 'Escrito à mão.saisie à la main, gardée'],
    ]);
    await userEvent.click(screen.getByTestId('assistant-apply'));
    await screen.findByTestId('assistant-applied');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/apply`)[0]?.body).toEqual({ pmap: PMAP, language: 'pt', comments: sentBack });
  }, 15_000);

  it('alone, without a text of the map to translate, are still offered to apply', async () => {
    translationApi({
      [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: {
        text: [{ type: 'started', destination: null }, { type: 'reply', reply: { ...proposal, comments: [comment] } }].map((line) => JSON.stringify(line)).join('\n'),
      },
      [`POST /api/maps/${MAP_ID}/assistant/preview`]: {
        json: { ...preview, changes: [], translations: [{ kind: 'presentation_comment', id: 's2', code: 'VIS-E002', original: comment.original, text: comment.text, kept: false }] },
      },
    });
    await askedIntoPortuguese();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    const card = await screen.findByTestId('assistant-translation');
    // Nothing is said to be shown on the map, in the singular or in the plural.
    expect(within(card).queryByText('sur la carte', { exact: false })).toBeNull();
    expect(screen.getByTestId('assistant-translation-comments').textContent).toBe('Commentaires de présentation : 1');
    expect(labels()).toEqual(['Demande reçue', 'Vérifier la demande', 'Conforme ?', 'Facture émise']);
    expect(screen.getByTestId('assistant-apply')).toBeInTheDocument();
  }, 15_000);
});

describe('the comments of presentation of the flows a translation brings (ADR 099, lot J10)', () => {
  const comment = { step_id: 's2', code: 'VIS-E002', original: 'Le commercial vérifie la demande.', text: 'O comercial verifica o pedido.' };
  // The flow whose label the preview translates too (`oui`, `sim`): its comment has the same identifier, another kind.
  const onFlow = { flow_id: 'f3', source_code: 'VIS-E003', target_code: 'VIS-E004', original: 'Le dossier conforme part à la facturation.', text: 'O processo conforme segue para a faturação.' };
  const flowsBack = [{ flow_id: onFlow.flow_id, original: onFlow.original, text: onFlow.text }];
  const flowText = { kind: 'flow_presentation_comment', id: 'f3', code: 'VIS-E003 > VIS-E004', original: onFlow.original, text: onFlow.text, kept: false };
  const applied = { json: { id: null, code: null, ids: [], operation_id: null, warnings: [], structure_version: 42, map: sampleMap(), history: { undo: [], redo: [] } } };
  const exchange = (reply: Schema<'AssistantReplyView'>) =>
    ({ text: [{ type: 'started', destination: null }, { type: 'reply', reply }].map((line) => JSON.stringify(line)).join('\n') });

  it('go with those of the steps to the preview and the application, are counted with them, marked as their own in the review, and never shown as the label of their flow', async () => {
    const sent = translationApi({
      [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: exchange({ ...proposal, comments: [comment], flow_comments: [onFlow] }),
      [`POST /api/maps/${MAP_ID}/assistant/preview`]: { json: { ...preview, translations: [
        ...(preview.translations ?? []),
        { kind: 'presentation_comment', id: 's2', code: 'VIS-E002', original: comment.original, text: comment.text, kept: false },
        flowText,
      ] } },
      [`POST /api/maps/${MAP_ID}/assistant/apply`]: applied,
    });
    const editor = await askedIntoPortuguese();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    const card = await screen.findByTestId('assistant-translation');
    const body = { pmap: PMAP, language: 'pt', comments: [{ step_id: comment.step_id, original: comment.original, text: comment.text }], flow_comments: flowsBack };
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/preview`)[0]?.body).toEqual(body);
    // The flow keeps the translation of its own label: its comment is said in a presentation, not written on the map.
    expect(editor.store.getState().previewed?.texts.f3).toBe('sim');
    expect(labels()).toEqual(['Pedido recebido à mão', 'Verificar o pedido', 'Conforme ?', 'Facture émise']);
    expect(within(card).getByText('4 textes traduits en Português, affichés sur la carte.')).toBeTruthy();
    expect(screen.getByTestId('assistant-translation-comments').textContent).toBe('Commentaires de présentation : 2');
    await userEvent.click(screen.getByTestId('assistant-translation-review'));
    const rows = within(card).getAllByRole('row').slice(1).map((row) => [...row.querySelectorAll('td')].map((cell) => cell.textContent));
    expect(rows.slice(-3)).toEqual([
      ['VIS-E003 > VIS-E004 oui', 'sim'],
      ['VIS-E002commentaire Le commercial vérifie la demande.', 'O comercial verifica o pedido.'],
      ['VIS-E003 > VIS-E004commentaire de relais Le dossier conforme part à la facturation.', 'O processo conforme segue para a faturação.'],
    ]);
    await userEvent.click(screen.getByTestId('assistant-apply'));
    await screen.findByTestId('assistant-applied');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/apply`)[0]?.body).toEqual(body);
  }, 15_000);

  it('alone, without a comment of a step nor a text of the map to translate, are sent alone and still offered to apply', async () => {
    const sent = translationApi({
      [`POST /api/maps/${MAP_ID}/assistant/exchanges`]: exchange({ ...proposal, flow_comments: [onFlow] }),
      [`POST /api/maps/${MAP_ID}/assistant/preview`]: { json: { ...preview, changes: [], translations: [flowText] } },
      [`POST /api/maps/${MAP_ID}/assistant/apply`]: applied,
    });
    const editor = await askedIntoPortuguese();
    await userEvent.click(screen.getByTestId('assistant-preview'));
    const card = await screen.findByTestId('assistant-translation');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/preview`)[0]?.body).toEqual({ pmap: PMAP, language: 'pt', flow_comments: flowsBack });
    // Nothing is said to be shown on the map, and no text of the map is previewed: the label of the flow stays as it is.
    expect(within(card).queryByText('sur la carte', { exact: false })).toBeNull();
    expect(editor.store.getState().previewed?.texts).toEqual({});
    expect(screen.getByTestId('assistant-translation-comments').textContent).toBe('Commentaires de présentation : 1');
    await userEvent.click(screen.getByTestId('assistant-apply'));
    await screen.findByTestId('assistant-applied');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/apply`)[0]?.body).toEqual({ pmap: PMAP, language: 'pt', flow_comments: flowsBack });
  }, 15_000);
});
