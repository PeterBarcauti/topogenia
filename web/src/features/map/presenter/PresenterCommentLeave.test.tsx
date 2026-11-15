// PresenterCommentLeave.test.tsx
// What a critical reading of the comment of presentation found (13/11/2026): a host that never answers holds nobody in the field; the way out acts no more than the other gestures while a text is on its way; a refusal no longer shown gives no text up unseen; « Étape précédente » leaves the step at once; Entrée held acts once, in the field too.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/errors.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments } from '../../../test/editor.tsx';
import { MAP_ID } from '../../../test/maps.ts';
import { afterReading, closed, field, key, line, onTheSecondStep, typeIn } from '../../../test/presenterStage.tsx';
import { COMMENT_SEND_MS, type PresentationComment } from './comments.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

const click = (testId: string) => {
  act(() => {
    screen.getByTestId(testId).click();
  });
};
// A second click on a button the first one made vanish does nothing, as for a person.
const clickIfShown = (testId: string) => {
  act(() => {
    screen.queryByTestId(testId)?.click();
  });
};
/** Types in the field of the comment and sends nothing: the gesture that follows does. */
const typeOnly = (text: string) => {
  act(() => {
    fireEvent.change(field(), { target: { value: text } });
  });
};
const shown = () => screen.queryByTestId('presenter-canvas')?.querySelector('.react-flow__node.is-presented')?.getAttribute('data-id') ?? null;
const alert = () => within(screen.getByTestId('presenter')).queryByRole('alert')?.textContent ?? null;

/** Comments whose host never answers a write: the request left, and nothing comes back. */
function silentHost() {
  const comments = memoryComments();
  const write: typeof comments.source.write = () => new Promise<never>(() => undefined);
  return { ...comments, source: { ...comments.source, write } };
}

/** Comments whose host answers when the test says so: each write kept with its signal. */
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
const SENT: PresentationComment[] = [{ step_id: 's2', text: 'Demande de congés.', language: 'fr', source: 'manual', version: 1 }];
const UNAVAILABLE = new ApiError(500, 'ERREUR_INTERNE', 'Le service est momentanément indisponible.');

describe('a text on its way to a host that does not answer (critical reading of 13/11/2026)', () => {
  /** A text typed, the way out clicked: the text leaves for a host that has not answered yet; then a second click on the way out. */
  const leftTwice = async () => {
    const host = deferredHost();
    const { editor } = await onTheSecondStep(host);
    key('t');
    typeOnly('Demande de congés.');
    click('presenter-exit');
    expect([host.calls.length, field().hasAttribute('readonly')]).toEqual([1, true]);
    clickIfShown('presenter-exit');
    expect([editor.presenter.getState().active, host.calls.length]).toEqual([true, 1]);
    return { editor, host };
  };

  it('keeps the presentation, says the refusal and keeps the text when the host refuses after two clicks on the way out', async () => {
    const { editor, host } = await leftTwice();
    await act(async () => {
      host.calls[0]?.reject(UNAVAILABLE);
      await Promise.resolve();
    });
    await waitFor(() => {
      expect(alert()).toBe('Le service est momentanément indisponible.');
    });
    expect([editor.presenter.getState().active, field().value]).toEqual([true, 'Demande de congés.']);
  });

  it('ends the presentation, asked by the first click, when the host keeps the text after two clicks on the way out', async () => {
    const { editor, host } = await leftTwice();
    await act(async () => {
      host.calls[0]?.resolve(SENT);
      await Promise.resolve();
    });
    await waitFor(() => {
      expect(editor.presenter.getState().active).toBe(false);
    });
  });

  it('keeps the presentation when the host stays silent after a click on the way out, and a new click on the message leaves', async () => {
    const { editor } = await onTheSecondStep(silentHost());
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    key('t');
    typeOnly('Demande de congés.');
    click('presenter-exit');
    expect([editor.presenter.getState().active, field().hasAttribute('readonly')]).toEqual([true, true]);
    await act(async () => {
      vi.advanceTimersByTime(COMMENT_SEND_MS);
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(editor.presenter.getState().active).toBe(true);
    expect([field().hasAttribute('readonly'), alert()?.includes('ne répond pas')]).toEqual([false, true]);
    // The refusal read: it has been shown long enough for a gesture to give the text up.
    afterReading();
    click('presenter-exit');
    expect(editor.presenter.getState().active).toBe(false);
  });

  it('gives the field back after the time limit: the lack of answer said, the text kept, the field free to send again or to be left', async () => {
    const { editor } = await onTheSecondStep(silentHost());
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    key('t');
    typeIn('Parti, jamais revenu.');
    expect([field().hasAttribute('readonly'), alert()]).toEqual([true, null]);
    await act(async () => {
      vi.advanceTimersByTime(COMMENT_SEND_MS);
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(alert()).toContain('ne répond pas');
    expect([field().hasAttribute('readonly'), field().value, editor.presenter.getState().writing]).toEqual([false, 'Parti, jamais revenu.', 's2']);
    // The lack of answer said and read, Échap gives the text up, as after any refusal: the field closes, the presentation stays.
    afterReading();
    act(() => {
      fireEvent.keyDown(field(), { key: 'Escape' });
    });
    expect([screen.queryByTestId('presenter-comment-editor'), editor.presenter.getState().active]).toEqual([null, true]);
  });
});

describe('a refusal no longer shown (critical reading of 13/11/2026)', () => {
  it('sends the text again on a gesture that leaves, instead of giving it up unseen', async () => {
    const comments = memoryComments();
    comments.state.failure = new ApiError(500, 'ERREUR_INTERNE', 'Le service est momentanément indisponible.');
    const { editor } = await onTheSecondStep(comments);
    key('t');
    typeIn('À garder.');
    await waitFor(() => {
      expect(alert()).toBe('Le service est momentanément indisponible.');
    });
    await waitFor(() => {
      expect(field()).not.toHaveAttribute('readonly');
    });
    // The host answers again, and what the stage said has gone, as after its seven seconds.
    comments.state.failure = null;
    act(() => {
      editor.presenter.getState().say(null);
    });
    act(() => {
      fireEvent.keyDown(field(), { key: 'Escape' });
    });
    await closed();
    expect([comments.writes.length, line()]).toEqual([2, 'À garder.']);
  });
});

describe('the moves by the keys and the bar around the field (critical reading of 13/11/2026)', () => {
  it('leaves its step at once on « Étape précédente », its text left as it was, whatever the step had played', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    const long = [
      'Le commercial vérifie chaque pièce du dossier avant de le transmettre.', 'Il renvoie la demande au client dès qu’une pièce manque au dossier.',
      'Il note enfin la date de réception dans le tableau de suivi partagé.',
    ].join('\n');
    const { editor } = await onTheSecondStep(memoryComments([{ step_id: 's2', text: long, language: 'fr', source: 'manual', version: 1 }]));
    key(' ');
    act(() => {
      vi.advanceTimersByTime(3000);
    });
    expect([shown(), editor.presenter.getState().player.elapsed > 2000]).toEqual(['s2', true]);
    key('t');
    click('presenter-previous');
    await waitFor(() => {
      expect([shown(), editor.presenter.getState().writing, document.activeElement]).toEqual(['s1', 's1', field()]);
    });
  });

  it('does nothing more on an Entrée held down: its repeats, the field once closed, do not tell the story further; a new press does', async () => {
    const { editor } = await onTheSecondStep();
    key('t');
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    const before = editor.presenter.getState().player;
    for (let repeats = 0; repeats < 3; repeats++) {
      act(() => {
        fireEvent.keyDown(document.activeElement ?? document.body, { key: 'Enter', repeat: true });
      });
    }
    expect(editor.presenter.getState().player).toEqual(before);
    key('Enter');
    expect(editor.presenter.getState().player).not.toEqual(before);
  });
});

describe('an Entrée held down in the field of a comment acts once (critical reading of 13/11/2026)', () => {
  const enterHeld = (times: number) => {
    for (let repeats = 0; repeats < times; repeats++) {
      act(() => {
        fireEvent.keyDown(field(), { key: 'Enter', repeat: true });
      });
    }
  };

  it('sends a refused text again on a new press only, never on its repeats', async () => {
    const comments = memoryComments();
    comments.state.failure = UNAVAILABLE;
    await onTheSecondStep(comments);
    key('t');
    typeIn('Demande de congés.');
    await waitFor(() => {
      expect([alert(), field().hasAttribute('readonly')]).toEqual(['Le service est momentanément indisponible.', false]);
    });
    enterHeld(3);
    expect(comments.writes.length).toBe(1);
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    expect(comments.writes.length).toBe(2);
  });

  it('does not overwrite the text of another on the repeats after a conflict: only a new press does, knowingly', async () => {
    const comments = memoryComments([{ step_id: 's2', text: 'Texte d’origine.', language: 'fr', source: 'manual', version: 1 }]);
    await onTheSecondStep(comments);
    key('t');
    await waitFor(() => {
      expect(comments.reads.length).toBe(2);
    });
    // Another person writes the line meanwhile: it is now in version 2.
    await comments.source.write('s2', 'Texte d’un autre.', 'fr', 1, new AbortController().signal);
    typeIn('Ma réponse.');
    await waitFor(() => {
      expect([alert(), field().hasAttribute('readonly')]).toEqual(['Ce commentaire a été modifié entre-temps.', false]);
    });
    const before = comments.writes.length;
    enterHeld(3);
    const lines = await comments.source.read(MAP_ID, new AbortController().signal);
    expect([comments.writes.length, lines.find((kept) => kept.step_id === 's2')?.text]).toEqual([before, 'Texte d’un autre.']);
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    await closed();
    expect(line()).toBe('Ma réponse.');
  });
});
