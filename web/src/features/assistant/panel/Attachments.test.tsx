// Attachments.test.tsx
// The files joined to the request of the assistant: the checks against the bounds of the connector, the paperclip, the pills with their size and their removal, a file dropped or pasted, a text file dropped read into the field, the files sent with their request and again with their turn, the hint of the copy and paste (spec 3.0 section 3.4, lot 4).

import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { toasts } from '../../../shared/ui/toasts.ts';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { base64Of, choose, kindOf, sizeOf } from './attachments.ts';
import { createAssistantStore } from './assistantStore.ts';
import { Composer } from './Composer.tsx';
import { RequestCard } from './TurnCards.tsx';

const MEGABYTE = 1024 * 1024;

const BOUNDS: Schema<'AssistantFileBoundsView'> = {
  files: 5, file_bytes: 10 * MEGABYTE, image_bytes: 7 * MEGABYTE, total_bytes: 20 * MEGABYTE,
  extensions: ['.pdf', '.png', '.jpg', '.jpeg', '.gif', '.webp', '.docx', '.xlsx', '.vsdx'], outdated: { '.doc': 'docx', '.xls': 'xlsx', '.vsd': 'vsdx' },
};

/** A file of the size given, its content repeated. */
const sized = (name: string, size: number, type = '') => new File([new Uint8Array(size)], name, { type });
const pdf = () => new File(['%PDF-1.7'], 'plan.pdf', { type: 'application/pdf' });
const png = () => new File(['PNGDATA'], 'capture.png', { type: 'image/png' });

const notes = () => toasts.getState().toasts.map((toast) => toast.text);

const stream = { text: [{ type: 'started', destination: null }, { type: 'reply', reply: { form: 'answer', message: 'Lu.', questions: [], findings: [], proposal: null, objects: [], raw: '{"form":"answer"}', cost: null, duration_milliseconds: 0 } }].map((line) => JSON.stringify(line)).join('\n') };

function field(bounds: Schema<'AssistantFileBoundsView'> | null = BOUNDS) {
  const store = createAssistantStore({ mapId: () => 'map', selection: () => [] });
  act(() => {
    store.getState().setBounds(bounds);
  });
  render(<Composer store={store} placeholder="Votre demande" />);
  return { store, input: screen.getByTestId('assistant-field'), composer: screen.getByTestId('assistant-composer') };
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    toasts.setState({ toasts: [] });
  });
  vi.unstubAllGlobals();
});

describe('the checks of the files joined', () => {
  it('tells the kind by the extension, and refuses a former format, another kind, an empty file, a file too large, too many or too heavy files', () => {
    expect([kindOf('A.PDF'), kindOf('b.jpeg'), kindOf('c.docx'), kindOf('d.xlsx'), kindOf('e.vsdx'), kindOf('f.doc'), kindOf('sans')]).toEqual(['pdf', 'image', 'word', 'excel', 'visio', null, null]);
    const { accepted, refusals } = choose([pdf(), sized('ancien.doc', 10), sized('notes.txt', 10), sized('vide.pdf', 0), sized('grande.png', 7 * MEGABYTE + 1), png()], [], BOUNDS);
    expect(accepted.map((file) => file.name)).toEqual(['plan.pdf', 'capture.png']);
    expect(refusals).toEqual([
      { reason: 'outdated', name: 'ancien.doc', format: 'docx' }, { reason: 'format', name: 'notes.txt' }, { reason: 'unreadable', name: 'vide.pdf' },
      { reason: 'too_large', name: 'grande.png', max: 7 },
    ]);
    expect(choose([pdf()], Array.from({ length: 5 }, () => ({ size: 1 })), BOUNDS).refusals).toEqual([{ reason: 'too_many', name: 'plan.pdf', max: 5 }]);
    expect(choose([sized('gros.pdf', 6 * MEGABYTE), sized('gros2.pdf', 6 * MEGABYTE)], [{ size: 9 * MEGABYTE }], BOUNDS).refusals)
      .toEqual([{ reason: 'too_heavy', name: 'gros2.pdf', max: 20 }]);
    expect(choose([sized('limite.pdf', 10 * MEGABYTE)], [], BOUNDS).accepted).toHaveLength(1);
  });

  it('reads a content in base64 and writes a size as a person reads it', async () => {
    expect(await base64Of(new Blob(['abc']))).toBe('YWJj');
    expect(sizeOf(10, 'fr')).toEqual({ unit: 'kb', size: '1' });
    expect(sizeOf(2048, 'fr')).toEqual({ unit: 'kb', size: '2' });
    expect(sizeOf(1.5 * MEGABYTE, 'fr')).toEqual({ unit: 'mb', size: '1,5' });
    expect(sizeOf(1.5 * MEGABYTE, 'en')).toEqual({ unit: 'mb', size: '1.5' });
  });
});

describe('the files of the field', () => {
  it('joins the files chosen by the paperclip, shows their pills, says each one refused, and removes a pill', async () => {
    const { store } = field();
    const user = userEvent.setup({ applyAccept: false });
    await user.upload(screen.getByTestId('assistant-attach-input'), [pdf(), sized('ancien.doc', 10), sized('photo.png', 1.5 * MEGABYTE)]);
    await waitFor(() => { expect(screen.getAllByTestId('assistant-file-pill')).toHaveLength(2); });
    const [first, second] = screen.getAllByTestId('assistant-file-pill');
    expect(first).toHaveTextContent('plan.pdf1 Ko');
    expect(second).toHaveTextContent('photo.png1,5 Mo');
    expect(notes()).toContain('« ancien.doc » est dans un ancien format d’Office : enregistrez-le au format docx, puis joignez-le à nouveau.');
    expect(screen.getByTestId('assistant-attach-input')).toHaveAttribute('accept', BOUNDS.extensions.join(','));
    if (first !== undefined) await userEvent.click(within(first).getByTestId('assistant-file-remove'));
    expect(store.getState().files.map((file) => file.name)).toEqual(['photo.png']);
  });

  it('cannot join a file before the bounds of the connector are known', () => {
    field(null);
    expect(screen.getByTestId('assistant-attach')).toBeDisabled();
  });

  it('joins a file dropped on the field, reads a text file dropped into it, and joins a capture pasted', async () => {
    const { store, input, composer } = field();
    const prompt = new File(['Mon récit préparé'], 'prompt.md', { type: 'text/markdown' });
    fireEvent.dragOver(composer, { dataTransfer: { types: ['Files'], files: [], dropEffect: 'none' } });
    expect(screen.getByTestId('assistant-drop')).toHaveTextContent('Déposez vos fichiers ici');
    fireEvent.drop(composer, { dataTransfer: { types: ['Files'], files: [pdf(), prompt] } });
    expect(screen.queryByTestId('assistant-drop')).toBeNull();
    await waitFor(() => { expect(store.getState().draft).toBe('Mon récit préparé'); });
    await waitFor(() => { expect(store.getState().files.map((file) => file.name)).toEqual(['plan.pdf']); });
    fireEvent.paste(input, { clipboardData: { files: [png()] } });
    await waitFor(() => { expect(store.getState().files.map((file) => file.name)).toEqual(['plan.pdf', 'capture.png']); });
    // A text dragged, not a file, is the business of the field.
    fireEvent.dragOver(composer, { dataTransfer: { types: ['text/plain'], files: [] } });
    expect(screen.queryByTestId('assistant-drop')).toBeNull();
  });
});

describe('the files sent', () => {
  it('sends the files with their request, then again with their turn, and the request shows them', async () => {
    const sent = fakeApi({ 'POST /api/maps/map/assistant/exchanges': [stream, stream] });
    const { store, input } = field();
    await userEvent.upload(screen.getByTestId('assistant-attach-input'), pdf());
    await waitFor(() => { expect(store.getState().files).toHaveLength(1); });
    await userEvent.type(input, 'Crée la carte de ce plan.{Enter}');
    await waitFor(() => { expect(store.getState().turns.at(-1)?.kind).toBe('reply'); });
    expect(callsTo(sent, 'POST', '/api/maps/map/assistant/exchanges')[0]?.body).toMatchObject({ files: [{ name: 'plan.pdf', data: 'JVBERi0xLjc=' }] });
    expect(store.getState().files).toEqual([]);
    expect(screen.queryByTestId('assistant-file-pill')).toBeNull();
    const request = store.getState().turns[0];
    expect(request).toMatchObject({ kind: 'request', files: [{ name: 'plan.pdf', size: 8, kind: 'pdf' }] });
    await userEvent.type(input, 'Et ensuite ?{Enter}');
    await waitFor(() => { expect(callsTo(sent, 'POST', '/api/maps/map/assistant/exchanges')).toHaveLength(2); });
    expect(callsTo(sent, 'POST', '/api/maps/map/assistant/exchanges')[1]?.body).toMatchObject({
      files: [], history: [{ request: 'Crée la carte de ce plan.', files: [{ name: 'plan.pdf', data: 'JVBERi0xLjc=' }] }],
    });
    if (request?.kind === 'request') render(<RequestCard turn={request} />);
    const shown = screen.getByTestId('assistant-request');
    expect(within(shown).getByTestId('assistant-file-pill')).toHaveTextContent('plan.pdf');
    expect(within(shown).queryByTestId('assistant-file-remove')).toBeNull();
  });

  it('keeps the files of a request that failed for its retry', async () => {
    const failure = { text: [{ type: 'started', destination: null }, { type: 'failure', failure: 'limited', detail: null }].map((line) => JSON.stringify(line)).join('\n') };
    const sent = fakeApi({ 'POST /api/maps/map/assistant/exchanges': [failure, stream] });
    const { store, input } = field();
    await userEvent.upload(screen.getByTestId('assistant-attach-input'), pdf());
    await waitFor(() => { expect(store.getState().files).toHaveLength(1); });
    await userEvent.type(input, 'Lis ce plan.{Enter}');
    await waitFor(() => { expect(store.getState().turns.at(-1)?.kind).toBe('failure'); });
    const failed = store.getState().turns.at(-1);
    const retry = failed?.kind === 'failure' ? failed.retry : null;
    expect(retry?.files.map((file) => file.name)).toEqual(['plan.pdf']);
    await act(async () => {
      await store.getState().send(retry?.text, retry?.intent, retry?.files);
    });
    expect(callsTo(sent, 'POST', '/api/maps/map/assistant/exchanges')[1]?.body).toMatchObject({ files: [{ name: 'plan.pdf', data: 'JVBERi0xLjc=' }] });
  });

  it('tells, without a model, that the person joins a pdf or an image in their AI', async () => {
    const { store } = field();
    act(() => {
      store.getState().setMode('copy_paste');
    });
    await userEvent.upload(screen.getByTestId('assistant-attach-input'), new File(['PK'], 'procédure.docx'));
    await waitFor(() => { expect(store.getState().files).toHaveLength(1); });
    expect(screen.queryByTestId('assistant-files-hint')).toBeNull();
    await userEvent.upload(screen.getByTestId('assistant-attach-input'), pdf());
    expect(await screen.findByTestId('assistant-files-hint')).toHaveTextContent('joignez vous-même les PDF et les images');
  });
});
