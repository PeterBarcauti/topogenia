// history.test.ts
// Undo and redo from the editor: the history of the host, after the writes before them, the message of the operation, the refusals and the failures (annex B 2.6, AR-05, AR-108, AR-121).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import en from '../../../../shared/i18n/locales/en.json';
import fr from '../../../../shared/i18n/locales/fr.json';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { replyOf, sampleMap } from '../../../../test/maps.ts';
import type { MapEditor } from '../editor.ts';
import { moveByKeys } from './layout.ts';
import { canReplay, replay } from './history.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const item = (kind: string) => ({ operation_id: `op-${kind}`, kind, at: '2026-10-28T08:00:00Z' });
const historyOf = (undo: string[], redo: string[] = []) => ({ undo: undo.map(item), redo: redo.map(item) });
const withHistory = (editor: MapEditor, undo: string[], redo: string[] = []) => {
  editor.store.setState({ history: historyOf(undo, redo) });
};

describe('the buttons Undo and Redo (B-2.1-24, B-2.6-06)', () => {
  it('follow the history of the host, and wait while an undo or a redo runs', async () => {
    const { editor } = await editorOf(sampleMap());
    const state = () => editor.store.getState();
    expect([canReplay(state(), 'undo'), canReplay(state(), 'redo')]).toEqual([false, false]);
    withHistory(editor, ['deplacer'], ['aligner']);
    expect([canReplay(state(), 'undo'), canReplay(state(), 'redo')]).toEqual([true, true]);
    editor.store.setState({ replaying: true });
    expect([canReplay(state(), 'undo'), canReplay(state(), 'redo')]).toEqual([false, false]);
  });

  it('an undo running sets the editor waiting until the host answers, a failure too', async () => {
    let answer: (response: Response) => void = () => undefined;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { answer = resolve; })));
    const { editor } = await editorOf(sampleMap());
    withHistory(editor, ['deplacer']);
    const undoing = replay(editor, 'undo');
    expect(editor.store.getState().replaying).toBe(true);
    await vi.waitFor(() => {
      expect(fetch).toHaveBeenCalled();
    });
    answer(new Response(JSON.stringify(replyOf(sampleMap(), { code: 'deplacer' })), { status: 200 }));
    await undoing;
    expect(editor.store.getState().replaying).toBe(false);
    fakeApi({ 'POST /api/maps/0190a1b2-0000-7000-8000-00000000a001/undo': { refused: 500, code: 'ERREUR_INTERNE', message: 'Erreur interne.' } });
    withHistory(editor, ['deplacer']);
    await replay(editor, 'undo');
    expect(editor.store.getState().replaying).toBe(false);
  });
});

describe('undo and redo (B-2.6-15, B-2.6-09)', () => {
  const UNDO = 'POST /api/maps/0190a1b2-0000-7000-8000-00000000a001/undo';
  const REDO = 'POST /api/maps/0190a1b2-0000-7000-8000-00000000a001/redo';

  it('undo takes the map and the history of the reply, and says « Annulé : déplacement »', async () => {
    const moved = { ...sampleMap(), name: 'Après annulation' };
    const sent = fakeApi({ [UNDO]: { json: replyOf(moved, { code: 'deplacer', history: historyOf([], ['deplacer']) }) } });
    const { editor, notes } = await editorOf(sampleMap());
    withHistory(editor, ['deplacer']);
    await replay(editor, 'undo');
    expect(callsTo(sent, 'POST', '/api/maps/0190a1b2-0000-7000-8000-00000000a001/undo')).toHaveLength(1);
    expect(editor.store.getState().map?.name).toBe('Après annulation');
    expect(editor.store.getState().history).toEqual(historyOf([], ['deplacer']));
    expect(notes).toEqual([['Annulé : déplacement', undefined]]);
  });

  it('redo says « Rétabli : alignement », in the language of the interface', async () => {
    fakeApi({ [REDO]: { json: replyOf(sampleMap(), { code: 'aligner' }) } });
    const { editor, notes } = await editorOf(sampleMap());
    withHistory(editor, [], ['aligner']);
    await replay(editor, 'redo');
    await i18next.changeLanguage('en');
    withHistory(editor, [], ['aligner']);
    await replay(editor, 'redo');
    expect(notes.map(([text]) => text)).toEqual(['Rétabli : alignement', 'Redone: alignment']);
  });

  it('waits for the writes asked before it: a Ctrl+Z pressed during a move undoes that move (B-2.6-09)', async () => {
    let answerMove: (response: Response) => void = () => undefined;
    const order: string[] = [];
    vi.stubGlobal('fetch', vi.fn((input: string) => {
      order.push(new URL(input, 'http://localhost').pathname);
      if (!input.endsWith('/undo')) return new Promise<Response>((resolve) => { answerMove = resolve; });
      return Promise.resolve(new Response(JSON.stringify(replyOf(sampleMap(), { code: 'deplacer' })), { status: 200 }));
    }));
    const { editor, notes } = await editorOf(sampleMap());
    editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    const moving = moveByKeys(editor, ['s2'], 0, 1);
    const undoing = replay(editor, 'undo');
    await vi.waitFor(() => {
      expect(order).toHaveLength(1);
    });
    answerMove(new Response(JSON.stringify(replyOf(sampleMap(), { history: historyOf(['deplacer']) })), { status: 200 }));
    await Promise.all([moving, undoing]);
    expect(order).toEqual(['/api/maps/0190a1b2-0000-7000-8000-00000000a001/move', '/api/maps/0190a1b2-0000-7000-8000-00000000a001/undo']);
    expect(notes).toEqual([['Annulé : déplacement', undefined]]);
  });

  it('sends nothing when the history is empty at its turn', async () => {
    const sent = fakeApi({});
    const { editor, notes } = await editorOf(sampleMap());
    await replay(editor, 'undo');
    withHistory(editor, ['deplacer']);
    await replay(editor, 'redo');
    expect([sent, notes]).toEqual([[], []]);
  });
});

describe('a refused or failed undo (B-2.6-07, B-2.6-08, B-2.6-48, B-2.6-49, AR-05, AR-108, AR-121)', () => {
  const UNDO = 'POST /api/maps/0190a1b2-0000-7000-8000-00000000a001/undo';
  const REDO = 'POST /api/maps/0190a1b2-0000-7000-8000-00000000a001/redo';
  const reread = { ...sampleMap(), name: 'Relue' };

  it('a refusal of the host shows its message and reads the map again, with the stacks the host cleared', async () => {
    fakeApi({
      [UNDO]: { refused: 409, code: 'OPERATION_DEPASSEE', message: 'Camille a modifié la carte à 10:12 : l’historique de la carte est vidé.' },
      [REDO]: { refused: 409, code: 'CARTE_MODIFIEE', message: 'La carte a été modifiée.' },
    });
    const { editor, notes } = await editorOf(sampleMap(), reread);
    withHistory(editor, ['deplacer'], ['aligner']);
    await replay(editor, 'undo');
    expect([editor.store.getState().map?.name, editor.store.getState().history]).toEqual(['Relue', historyOf([])]);
    withHistory(editor, [], ['aligner']);
    await replay(editor, 'redo');
    expect(notes).toEqual([['Camille a modifié la carte à 10:12 : l’historique de la carte est vidé.', 'error'], ['La carte a été modifiée.', 'error']]);
  });

  it('an empty stack on the host (INTROUVABLE) is silent: the map and its history are read again (AR-108)', async () => {
    fakeApi({ [UNDO]: { refused: 404, code: 'INTROUVABLE', message: 'Élément introuvable.' } });
    const { editor, notes } = await editorOf(sampleMap(), reread);
    withHistory(editor, ['deplacer']);
    await replay(editor, 'undo');
    expect(notes).toEqual([]);
    expect([editor.store.getState().map?.name, editor.store.getState().history]).toEqual(['Relue', historyOf([])]);
  });

  it('another failure says the operation could not be replayed, with its reason; the map and the history of the host are read again', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))));
    const { editor, notes } = await editorOf(sampleMap(), reread);
    withHistory(editor, ['deplacer']);
    await replay(editor, 'undo');
    withHistory(editor, [], ['deplacer']);
    await replay(editor, 'redo');
    const reason = i18next.t('errors.RESEAU');
    expect(notes).toEqual([[`Impossible d’annuler. ${reason}`, 'error'], [`Impossible de rétablir. ${reason}`, 'error']]);
    expect(editor.store.getState().map?.name).toBe('Relue');
  });
});

describe('the names of the operations (B-2.6-15)', () => {
  it('every kind of the operation log of the host has its name in each language', () => {
    const source = readFileSync(join(import.meta.dirname, '../../../../../../src/Topogenia.Application/OperationLog/OperationKinds.cs'), 'utf8');
    const kinds = [...source.matchAll(/public const string \w+ = "([a-z_]+)";/g)].map((match) => match[1]);
    // The 21 kinds of the 1.x, the reversal of a flow, new in 2.0 (AR-155), and the proposal of the assistant, new in 3.0 (ADR 079).
    expect(kinds).toHaveLength(23);
    expect(Object.keys(fr.history.actions).sort()).toEqual([...kinds].sort());
    expect(Object.keys(en.history.actions).sort()).toEqual([...kinds].sort());
  });
});
