// summary.test.tsx
// The panel of the summary of the map shown (plan 3.1, lot R): its sentence, who wrote it, a map changed since; written by hand with the version read, erased, asked again of the AI; the bubble at the opening turned off; the File menu entry for the facilitator only.

import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, replyOf, sampleMap } from '../../../test/maps.ts';

const SENTENCE = 'Le commercial reçoit la demande, la vérifie et envoie le devis au client.';

const summary = (overrides: Partial<Schema<'MapSummaryView'>> = {}): Schema<'MapSummaryView'> => ({
  text: SENTENCE, source_language: 'fr', translations: {}, origin: 'ai', model: 'claude-opus-5-5', written_at: '2026-11-06T20:00:00Z', stale: false, version: 4,
  ...overrides,
});

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

async function openPanel(map: Schema<'MapView'>) {
  const shown = await showEditor(map);
  act(() => {
    shown.editor.floating.getState().open({ kind: 'summary' });
  });
  return { ...shown, dialog: await screen.findByRole('dialog', { name: i18next.t('summary.title') }) };
}

describe('the panel of the summary (plan 3.1, lot R)', () => {
  it('shows the sentence, who wrote it and when, and says when the map changed since', async () => {
    await openPanel(sampleMap({ summary: summary({ stale: true }) }));
    expect(screen.getByTestId('summary-text')).toHaveTextContent(SENTENCE);
    expect(screen.getByTestId('summary-written')).toHaveTextContent(i18next.t('summary.by_ai', { date: '06/11/2026' }));
    expect(screen.getByTestId('summary-written')).toHaveTextContent('claude-opus-5-5');
    expect(screen.getByTestId('summary-stale')).toHaveTextContent(i18next.t('summary.stale_help'));
  });

  it('writes the sentence corrected by hand with the version it replaces, and erases it', async () => {
    const written = sampleMap({ summary: summary({ text: 'Le commercial envoie le devis.', origin: 'manual', model: null, version: 5 }) });
    const sent = fakeApi({ [`PUT /api/maps/${MAP_ID}/summaries`]: [{ json: replyOf(written) }, { json: replyOf(sampleMap({ summary: null })) }] });
    await openPanel(sampleMap({ summary: summary() }));
    await userEvent.click(screen.getByTestId('summary-edit'));
    await userEvent.clear(screen.getByTestId('summary-draft'));
    await userEvent.type(screen.getByTestId('summary-draft'), ' Le commercial envoie le devis. ');
    await userEvent.click(screen.getByTestId('summary-save'));
    expect(await screen.findByTestId('summary-text')).toHaveTextContent('Le commercial envoie le devis.');
    expect(callsTo(sent, 'PUT', `/api/maps/${MAP_ID}/summaries`)[0]?.body).toEqual({
      summaries: [{ map_id: MAP_ID, text: 'Le commercial envoie le devis.', origin: 'manual', model: null, version: 4 }],
    });
    expect(screen.getByTestId('summary-written')).toHaveTextContent(i18next.t('summary.by_hand', { date: '06/11/2026' }));
    await userEvent.click(screen.getByTestId('summary-erase'));
    expect(await screen.findByTestId('summary-draft')).toHaveValue('');
    expect(callsTo(sent, 'PUT', `/api/maps/${MAP_ID}/summaries`)[1]?.body).toEqual({
      summaries: [{ map_id: MAP_ID, text: '', origin: 'manual', model: null, version: 5 }],
    });
  });

  it('opens a map without summary on the field to write it, sent with the version 0 of a summary never read', async () => {
    const sent = fakeApi({ [`PUT /api/maps/${MAP_ID}/summaries`]: { json: replyOf(sampleMap({ summary: summary({ origin: 'manual', model: null, version: 1 }) })) } });
    await openPanel(sampleMap({ summary: null }));
    expect(screen.getByTestId('summary-save')).toBeDisabled();
    await userEvent.type(screen.getByTestId('summary-draft'), SENTENCE);
    expect(screen.getByText(i18next.t('summary.count', { n: SENTENCE.length, max: 200 }))).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('summary-save'));
    expect(await screen.findByTestId('summary-text')).toHaveTextContent(SENTENCE);
    expect(callsTo(sent, 'PUT', `/api/maps/${MAP_ID}/summaries`)[0]?.body).toEqual({ summaries: [{ map_id: MAP_ID, text: SENTENCE, origin: 'manual', model: null, version: 0 }] });
  });

  it('turns the bubble at the opening off, kept in the preferences of the browser', async () => {
    const { editor } = await openPanel(sampleMap({ summary: summary() }));
    expect(screen.getByTestId('summary-on-open')).toBeChecked();
    await userEvent.click(screen.getByTestId('summary-on-open'));
    expect(editor.preferences.getState().showSummaryOnOpen).toBe(false);
  });

  it('offers a reader the sentence only, nothing to write nor to ask', async () => {
    await openPanel(sampleMap({ summary: summary(), rights: { structure: false, role: 'reader' } }));
    expect(screen.getByTestId('summary-text')).toHaveTextContent(SENTENCE);
    for (const action of ['summary-edit', 'summary-erase', 'summary-ask', 'summary-save']) expect(screen.queryByTestId(action)).toBeNull();
  });
});
