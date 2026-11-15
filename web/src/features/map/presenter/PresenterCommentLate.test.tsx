// PresenterCommentLate.test.tsx
// What a critical reading of the comment of presentation found (13/11/2026): after a conflict a gesture that leaves the field says it again before it gives the text up; an answer that comes late, or to another field, takes nothing.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/errors.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments } from '../../../test/editor.tsx';
import { MAP_ID } from '../../../test/maps.ts';
import { afterReading, closed, field, key, onTheSecondStep, typeIn } from '../../../test/presenterStage.tsx';
import { COMMENT_SEND_MS, type PresentationComment } from './comments.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

const CONFLICT = 'Ce commentaire a été modifié entre-temps.';
const alert = () => within(screen.getByTestId('presenter')).queryByRole('alert')?.textContent ?? null;
const escape = () => {
  act(() => {
    fireEvent.keyDown(field(), { key: 'Escape' });
  });
};
const SENT: PresentationComment[] = [{ step_id: 's2', text: 'Demande de congés.', language: 'fr', source: 'manual', version: 1 }];

/** Comments whose host answers a write when the test says so: each write kept with its signal. */
interface Late {
  readonly signal: AbortSignal | undefined;
  readonly resolve: (lines: readonly PresentationComment[]) => void;
  readonly reject: (failure: Error) => void;
}
function deferredHost() {
  const comments = memoryComments();
  const calls: Late[] = [];
  const write: typeof comments.source.write = (_stepId, _text, _language, _version, signal) =>
    new Promise((resolve, reject) => {
      calls.push({ signal, resolve, reject });
    });
  return { ...comments, source: { ...comments.source, write }, calls };
}

/** The field written over a line another person changed meanwhile: the conflict said, the field free again, the text typed in it. */
async function inConflict() {
  const comments = memoryComments([{ step_id: 's2', text: 'Texte d’origine.', language: 'fr', source: 'manual', version: 1 }]);
  const stage = await onTheSecondStep(comments);
  key('t');
  await waitFor(() => {
    expect(comments.reads.length).toBe(2);
  });
  await comments.source.write('s2', 'Texte d’un autre.', 'fr', 1, new AbortController().signal);
  typeIn('Ma réponse.');
  await waitFor(() => {
    expect(alert()).toBe(CONFLICT);
  });
  await waitFor(() => {
    expect(field()).not.toHaveAttribute('readonly');
  });
  return { ...stage, comments, before: comments.writes.length };
}
const hosted = async (comments: ReturnType<typeof memoryComments>) =>
  (await comments.source.read(MAP_ID, new AbortController().signal)).find((kept) => kept.step_id === 's2')?.text;

describe('a gesture that leaves the field after a conflict (critical reading of 13/11/2026)', () => {
  it('says the conflict again instead of overwriting the text of another once the message has gone, and gives the text up the second time', async () => {
    const { editor, comments, before } = await inConflict();
    act(() => {
      editor.presenter.getState().say(null);
    });
    escape();
    expect(comments.writes.length).toBe(before);
    await waitFor(() => {
      expect(alert()).toBe(CONFLICT);
    });
    expect([field().value, editor.presenter.getState().writing]).toEqual(['Ma réponse.', 's2']);
    // The refusal read: it has been shown long enough for a gesture to give the text up.
    afterReading();
    escape();
    await closed();
    expect([comments.writes.length, await hosted(comments)]).toEqual([before, 'Texte d’un autre.']);
  });

  it('does the same when the text was changed after the conflict, the first message still shown', async () => {
    const { editor, comments, before } = await inConflict();
    const first = editor.presenter.getState().alert;
    act(() => {
      fireEvent.change(field(), { target: { value: 'Ma réponse retouchée.' } });
    });
    escape();
    expect(comments.writes.length).toBe(before);
    await waitFor(() => {
      expect(alert()).toBe(CONFLICT);
    });
    // Said again: another message than the first, which was still shown.
    expect(editor.presenter.getState().alert).not.toBe(first);
    expect(field().value).toBe('Ma réponse retouchée.');
    // The refusal read: it has been shown long enough for a gesture to give the text up.
    afterReading();
    escape();
    await closed();
    expect([comments.writes.length, await hosted(comments)]).toEqual([before, 'Texte d’un autre.']);
  });

  it('closes the field, nothing written, when the text is not changed and the message, read, is still shown (no regression)', async () => {
    const { comments, before } = await inConflict();
    // The refusal read: it has been shown long enough for a gesture to give the text up.
    afterReading();
    escape();
    await closed();
    expect([comments.writes.length, await hosted(comments)]).toEqual([before, 'Texte d’un autre.']);
  });
});

describe('an answer that comes after the time limit takes nothing (critical reading of 13/11/2026)', () => {
  /** A text sent to a host that answers late: the time limit passed, the lack of answer said, the text given up with Échap. */
  async function abandoned() {
    const host = deferredHost();
    const stage = await onTheSecondStep(host);
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    key('t');
    typeIn('Demande de congés.');
    await act(async () => {
      vi.advanceTimersByTime(COMMENT_SEND_MS);
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(alert()).toContain('ne répond pas');
    vi.useRealTimers();
    // The refusal read: it has been shown long enough for a gesture to give the text up.
    afterReading();
    escape();
    await closed();
    return { ...stage, host };
  }
  const flush = () => act(async () => {
    for (let turn = 0; turn < 5; turn++) await Promise.resolve();
  });

  it('does not stop the story told again meanwhile', async () => {
    const { editor, host } = await abandoned();
    key(' ');
    const { player } = editor.presenter.getState();
    expect(player.playing).toBe(true);
    host.calls[0]?.resolve(SENT);
    await flush();
    const now = editor.presenter.getState().player;
    expect([now.playing, now.elapsed >= player.elapsed, alert()]).toEqual([true, true, null]);
  });

  it('says no refusal of a late answer once the lack of answer has gone', async () => {
    const { editor, host } = await abandoned();
    act(() => {
      editor.presenter.getState().say(null);
    });
    host.calls[0]?.reject(new ApiError(500, 'ERREUR_INTERNE', 'Le service est momentanément indisponible.'));
    await flush();
    expect(alert()).toBeNull();
  });

  it('gives the host a signal that is aborted once the time limit has passed, not before', async () => {
    const host = deferredHost();
    await onTheSecondStep(host);
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    key('t');
    typeIn('Demande de congés.');
    expect(host.calls[0]?.signal?.aborted).toBe(false);
    await act(async () => {
      vi.advanceTimersByTime(COMMENT_SEND_MS);
      await Promise.resolve();
    });
    expect(host.calls[0]?.signal?.aborted).toBe(true);
  });
});

describe('the answer of a text goes to the field that sent it (critical reading of 13/11/2026)', () => {
  it('leaves a field opened since on the same step open, with its own text', async () => {
    const host = deferredHost();
    const { editor } = await onTheSecondStep(host);
    key('t');
    typeIn('Demande de congés.');
    act(() => {
      editor.presenter.getState().write(null);
      editor.presenter.getState().write('s2');
    });
    act(() => {
      fireEvent.change(field(), { target: { value: 'Nouveau texte.' } });
    });
    await act(async () => {
      host.calls[0]?.resolve(SENT);
      await Promise.resolve();
      await Promise.resolve();
    });
    const now = screen.queryByTestId<HTMLTextAreaElement>('presenter-comment-field');
    expect([now?.value ?? null, now?.hasAttribute('readonly') ?? null]).toEqual(['Nouveau texte.', false]);
  });
});
