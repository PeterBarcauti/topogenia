// CommentsReply.test.tsx
// The comments of presentation written by the model in the panel of the assistant (3.3, lot 3, F-130): those of the map open and of its detailed maps, each map named when there are several; each kept, corrected or dropped before anything is saved; one that would replace a comment typed by hand unticked and said so, sent with its replacement accepted only when the person ticks it; saved, the card shown again says so.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { createAssistantStore, type AssistantReply } from './assistantStore.ts';
import { CommentsReply } from './CommentsReply.tsx';
import type { ThreadContext } from './threadContext.ts';
import { ReplyCard } from './TurnCards.tsx';

const DRAFTS: NonNullable<AssistantReply['comment_drafts']> = [
  { step_id: 's1', flow_id: null, reference: 'VIS-E001', context: 'Demande reçue', map: 'Vente', text: 'Le client envoie sa demande.', current: null, current_source: null, version: 0 },
  { step_id: 's2', flow_id: null, reference: 'VIS-E002', context: 'Vérifier', map: 'Vente', text: 'Le commercial vérifie.', current: 'Tapé à la main.', current_source: 'manual', version: 4 },
  { step_id: null, flow_id: 'f1', reference: 'VIS-E001 > VIS-E002', context: 'Demande reçue > Vérifier', map: 'Vente', text: 'La demande passe au commercial.', current: 'Ancien.', current_source: 'assistant', version: 2 },
];

const reply = (drafts: AssistantReply['comment_drafts'] = DRAFTS): AssistantReply => ({
  form: 'comments', message: 'Trois commentaires.', questions: [], findings: [], proposal: null, objects: [], raw: '{}', duration_milliseconds: 900, comment_drafts: drafts,
  cost: null,
});

const context = (saveComments?: ThreadContext['saveComments'], store = createAssistantStore({ mapId: () => 'map', selection: () => [] })): ThreadContext => ({
  store, home: false, status: null, ...(saveComments === undefined ? {} : { saveComments }),
});

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the comments of presentation to review (F-130)', () => {
  it('lists each step and flow with its comment, the one that would replace a typed comment unticked and said so', () => {
    render(<CommentsReply reply={reply()} context={context(vi.fn())} turnId={1} />);
    const items = screen.getAllByTestId('assistant-comment');
    expect(items).toHaveLength(3);
    expect(items[2]).toHaveTextContent(i18next.t('assistant.comments.flow', { labels: 'Demande reçue > Vérifier' }));
    expect(items[2]).toHaveTextContent(i18next.t('assistant.comments.current', { text: 'Ancien.' }));
    expect(screen.getByTestId('assistant-comment-typed')).toHaveTextContent(i18next.t('assistant.comments.typed', { text: 'Tapé à la main.' }));
    expect(screen.getAllByTestId('assistant-comment-keep').map((box) => (box as HTMLInputElement).checked)).toEqual([true, false, true]);
    expect(screen.getByTestId('assistant-comments-save')).toHaveTextContent(i18next.t('assistant.comments.save', { n: 2 }));
    // One map: no map named.
    expect(screen.queryByTestId('assistant-comments-map')).toBeNull();
  });

  it('names each map before its first comment when the reply covers the map open and its detailed maps (essay of the 18/11/2026)', async () => {
    const saveComments = vi.fn(() => Promise.resolve(true));
    const inner = [
      { step_id: 's3', flow_id: null, reference: 'VIS-E003', context: 'Contrôler', map: 'Vérifier', text: 'Le responsable contrôle.', current: null, current_source: null, version: 0 },
      { step_id: null, flow_id: 'f2', reference: 'VIS-E003 > VIS-E004', context: 'Contrôler > Signer', map: 'Vérifier', text: 'Puis il signe.', current: null, current_source: null, version: 0 },
    ];
    render(<CommentsReply reply={reply([...DRAFTS, ...inner])} context={context(saveComments)} turnId={1} />);
    expect(screen.getAllByTestId('assistant-comments-map').map((name) => name.textContent)).toEqual([
      i18next.t('assistant.comments.map', { name: 'Vente' }), i18next.t('assistant.comments.map', { name: 'Vérifier' }),
    ]);
    expect(screen.getAllByTestId('assistant-comment')[3]).toHaveTextContent(i18next.t('assistant.comments.map', { name: 'Vérifier' }));
    await userEvent.click(screen.getByTestId('assistant-comments-save'));
    expect(saveComments).toHaveBeenCalledWith([
      { stepId: 's1', flowId: null, text: 'Le client envoie sa demande.', version: 0, replace: false },
      { stepId: null, flowId: 'f1', text: 'La demande passe au commercial.', version: 2, replace: false },
      { stepId: 's3', flowId: null, text: 'Le responsable contrôle.', version: 0, replace: false },
      { stepId: null, flowId: 'f2', text: 'Puis il signe.', version: 0, replace: false },
    ]);
  });

  it('saves the comments kept as corrected, the typed one replaced only once ticked, then says how many', async () => {
    const saveComments = vi.fn(() => Promise.resolve(true));
    render(<CommentsReply reply={reply()} context={context(saveComments)} turnId={1} />);
    const texts = screen.getAllByTestId('assistant-comment-text');
    await userEvent.clear(texts[0] as HTMLElement);
    await userEvent.type(texts[0] as HTMLElement, '  Le client écrit.  ');
    await userEvent.click(screen.getAllByTestId('assistant-comment-keep')[1] as HTMLElement);
    await userEvent.click(screen.getByTestId('assistant-comments-save'));
    expect(saveComments).toHaveBeenCalledWith([
      { stepId: 's1', flowId: null, text: 'Le client écrit.', version: 0, replace: false },
      { stepId: 's2', flowId: null, text: 'Le commercial vérifie.', version: 4, replace: true },
      { stepId: null, flowId: 'f1', text: 'La demande passe au commercial.', version: 2, replace: false },
    ]);
    expect(await screen.findByTestId('assistant-comments-saved')).toHaveTextContent(i18next.t('assistant.comments.saved', { n: 3 }));
    expect(screen.queryByTestId('assistant-comments-save')).toBeNull();
  });

  it('in its card, the reply says it brings comments of presentation, never the key of its title', () => {
    // The card of a reply is what the person sees (TurnCards): its head names the form (audit of the copy 64, P1-01).
    render(<ReplyCard turn={{ kind: 'reply', id: 1, reply: reply(), cost: null, elapsed: 0, pasted: false }} context={context(vi.fn())} />);
    expect(screen.getByTestId('assistant-reply')).toHaveTextContent('Commentaires de présentation');
    expect(screen.getByTestId('assistant-reply')).not.toHaveTextContent('assistant.panel');
    expect(screen.getAllByTestId('assistant-comment')).toHaveLength(3);
  });

  it('saved, the card shown again says so; nothing to save when nothing is kept; no saving without a map; nothing without comments', async () => {
    const saveComments = vi.fn(() => Promise.resolve(true));
    const store = createAssistantStore({ mapId: () => 'map', selection: () => [] });
    const { unmount } = render(<CommentsReply reply={reply()} context={context(saveComments, store)} turnId={5} />);
    await userEvent.click(screen.getByTestId('assistant-comments-save'));
    expect(await screen.findByTestId('assistant-comments-saved')).toHaveTextContent(i18next.t('assistant.comments.saved', { n: 2 }));
    unmount();
    const { rerender } = render(<CommentsReply reply={reply()} context={context(saveComments, store)} turnId={5} />);
    expect(screen.getByTestId('assistant-comments-saved')).toBeInTheDocument();
    expect(saveComments).toHaveBeenCalledTimes(1);
    rerender(<CommentsReply key={6} reply={reply(DRAFTS.slice(1, 2))} context={context(saveComments)} turnId={6} />);
    expect(screen.getByTestId('assistant-comments-save')).toBeDisabled();
    rerender(<CommentsReply key={7} reply={reply(DRAFTS.slice(0, 1))} context={context()} turnId={7} />);
    expect(screen.queryByTestId('assistant-comments-save')).toBeNull();
    rerender(<CommentsReply key={8} reply={reply([])} context={context(saveComments)} turnId={8} />);
    expect(screen.queryByTestId('assistant-comments')).toBeNull();
    store.getState().restart();
    expect(store.getState().commentsSaved).toEqual({});
  });
});
