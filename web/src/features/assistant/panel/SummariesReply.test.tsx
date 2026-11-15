// SummariesReply.test.tsx
// The summaries written by the model in the panel of the assistant (plan 3.1, lot R): each sentence kept, corrected or dropped before anything is saved; the ones kept saved at once with the version they replace and the model that wrote them; saved, the card shown again says so (audit 3.1 V2, 5.3).

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { createAssistantStore, type AssistantReply } from './assistantStore.ts';
import { SummariesReply } from './SummariesReply.tsx';
import type { ThreadContext } from './threadContext.ts';

const SUMMARIES = [
  { map_id: 'root', reference: 'VIS', name: 'Visites à l’export', text: 'Le commercial reçoit la demande et envoie le devis.', current: null, version: 0 },
  { map_id: 'detail', reference: 'VIS-E004', name: 'Traiter la commande', text: 'La comptabilité facture la commande.', current: 'Ancienne phrase.', version: 3 },
  { map_id: 'other', reference: 'VIS-E007', name: 'Livrer', text: 'Le magasin livre.', current: null, version: 0 },
];

const reply = (summaries: AssistantReply['summaries'] = SUMMARIES): AssistantReply => ({
  form: 'summaries', message: 'Trois cartes résumées.', questions: [], findings: [], proposal: null, objects: [], raw: '{}', duration_milliseconds: 900, summaries,
  cost: { model: 'claude-opus-5-5', input_tokens: 1, cache_read_tokens: 0, cache_write_tokens: 0, output_tokens: 1, estimated_usd: null },
});

const context = (saveSummaries?: ThreadContext['saveSummaries'], store = createAssistantStore({ mapId: () => 'map', selection: () => [] })): ThreadContext => ({
  store, home: false, status: null, ...(saveSummaries === undefined ? {} : { saveSummaries }),
});

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the summaries to review (plan 3.1, lot R)', () => {
  it('lists each map with its sentence and its summary of today, all kept at first', () => {
    render(<SummariesReply reply={reply()} context={context(vi.fn())} turnId={1} />);
    const items = screen.getAllByTestId('assistant-summary');
    expect(items).toHaveLength(3);
    expect(items[1]).toHaveTextContent('Traiter la commande');
    expect(items[1]).toHaveTextContent('VIS-E004');
    expect(items[1]).toHaveTextContent(i18next.t('assistant.summaries.current', { text: 'Ancienne phrase.' }));
    expect(screen.getAllByTestId('assistant-summary-keep').map((box) => (box as HTMLInputElement).checked)).toEqual([true, true, true]);
    expect(screen.getByTestId('assistant-summaries-save')).toHaveTextContent(i18next.t('assistant.summaries.save', { n: 3 }));
  });

  it('saves only the sentences kept, as corrected, with the version they replace and the model, then says how many', async () => {
    const saveSummaries = vi.fn(() => Promise.resolve(true));
    render(<SummariesReply reply={reply()} context={context(saveSummaries)} turnId={1} />);
    const texts = screen.getAllByTestId('assistant-summary-text');
    await userEvent.clear(texts[1] as HTMLElement);
    await userEvent.type(texts[1] as HTMLElement, '  La comptabilité facture puis relance.  ');
    await userEvent.click(screen.getAllByTestId('assistant-summary-keep')[2] as HTMLElement);
    await userEvent.click(screen.getByTestId('assistant-summaries-save'));
    expect(saveSummaries).toHaveBeenCalledWith([
      { mapId: 'root', text: 'Le commercial reçoit la demande et envoie le devis.', version: 0 },
      { mapId: 'detail', text: 'La comptabilité facture puis relance.', version: 3 },
    ], 'claude-opus-5-5');
    expect(await screen.findByTestId('assistant-summaries-saved')).toHaveTextContent(i18next.t('assistant.summaries.saved', { n: 2 }));
    expect(screen.queryByTestId('assistant-summaries-save')).toBeNull();
  });

  it('saved, the card shown again (the panel closed then opened) says so and offers nothing more to save (audit 3.1 V2, 5.3)', async () => {
    const saveSummaries = vi.fn(() => Promise.resolve(true));
    const store = createAssistantStore({ mapId: () => 'map', selection: () => [] });
    const { unmount } = render(<SummariesReply reply={reply()} context={context(saveSummaries, store)} turnId={4} />);
    await userEvent.click(screen.getByTestId('assistant-summaries-save'));
    expect(await screen.findByTestId('assistant-summaries-saved')).toHaveTextContent(i18next.t('assistant.summaries.saved', { n: 3 }));
    unmount();
    render(<SummariesReply reply={reply()} context={context(saveSummaries, store)} turnId={4} />);
    expect(screen.getByTestId('assistant-summaries-saved')).toHaveTextContent(i18next.t('assistant.summaries.saved', { n: 3 }));
    expect(screen.queryByTestId('assistant-summaries-save')).toBeNull();
    expect(saveSummaries).toHaveBeenCalledTimes(1);
    // A new thread forgets it, as it forgets the turns.
    store.getState().restart();
    expect(store.getState().summariesSaved).toEqual({});
  });

  it('cannot save when nothing is kept, keeps the list when the saving fails, offers no saving without a map, shows nothing without sentences', async () => {
    const saveSummaries = vi.fn(() => Promise.resolve(false));
    const { rerender } = render(<SummariesReply reply={reply(SUMMARIES.slice(0, 1))} context={context(saveSummaries)} turnId={1} />);
    await userEvent.click(screen.getByTestId('assistant-summary-keep'));
    expect(screen.getByTestId('assistant-summaries-save')).toBeDisabled();
    await userEvent.click(screen.getByTestId('assistant-summary-keep'));
    await userEvent.click(screen.getByTestId('assistant-summaries-save'));
    expect(screen.getByTestId('assistant-summaries-save')).toBeEnabled();
    rerender(<SummariesReply reply={reply(SUMMARIES.slice(0, 1))} context={context()} turnId={1} />);
    expect(screen.queryByTestId('assistant-summaries-save')).toBeNull();
    rerender(<SummariesReply reply={reply([])} context={context(saveSummaries)} turnId={1} />);
    expect(screen.queryByTestId('assistant-summaries')).toBeNull();
  });
});
