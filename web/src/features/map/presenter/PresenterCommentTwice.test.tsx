// PresenterCommentTwice.test.tsx
// What the third critical reading of the comment of presentation found (13/11/2026): a refusal must have been read before a gesture gives its text up; the pointer gestures that do nothing bring the cursor back to the field; a conflict is said again when the field is free.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/errors.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments } from '../../../test/editor.tsx';
import { MAP_ID } from '../../../test/maps.ts';
import { afterReading, closed, field, key, lateComments, onTheSecondStep, typeIn } from '../../../test/presenterStage.tsx';
import type { MapEditor } from '../state/editor.ts';
import type { PresentationComment } from './comments.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

const CONFLICT = 'Ce commentaire a été modifié entre-temps.';
const UNAVAILABLE = 'Le service est momentanément indisponible.';
const alert = () => {
  const stage = screen.queryByTestId('presenter');
  return stage === null ? null : within(stage).queryByRole('alert')?.textContent ?? null;
};
const shown = () => screen.queryByTestId('presenter-canvas')?.querySelector('.react-flow__node.is-presented')?.getAttribute('data-id') ?? null;
/** What the field holds, null once it is closed. */
const held = () => screen.queryByTestId<HTMLTextAreaElement>('presenter-comment-field')?.value ?? null;
const free = () => !field().hasAttribute('readonly');
const unavailable = () => new ApiError(500, 'ERREUR_INTERNE', UNAVAILABLE);

/** A click as a browser makes it: the button takes the focus, then acts. */
const press = (testId: string) => {
  act(() => {
    const button = screen.getByTestId(testId);
    button.focus();
    button.click();
  });
};
const escape = () => {
  act(() => {
    fireEvent.keyDown(field(), { key: 'Escape' });
  });
};
const typeOnly = (text: string) => {
  act(() => {
    fireEvent.change(field(), { target: { value: text } });
  });
};
const forget = (editor: MapEditor) => {
  act(() => {
    editor.presenter.getState().say(null);
  });
};
const hosted = async (comments: ReturnType<typeof memoryComments>) =>
  (await comments.source.read(MAP_ID, new AbortController().signal)).find((kept) => kept.step_id === 's2')?.text;

const ORIGINAL: PresentationComment[] = [{ step_id: 's2', text: 'Texte d’origine.', language: 'fr', source: 'manual', version: 1 }];

/** The field written over a line another person changed meanwhile: the conflict said, the field free again, the text typed in it. */
async function inConflict() {
  const comments = memoryComments(ORIGINAL);
  const stage = await onTheSecondStep(comments);
  key('t');
  await waitFor(() => {
    expect(comments.reads.length).toBe(2);
  });
  await comments.source.write('s2', 'Texte d’un autre.', 'fr', 1, new AbortController().signal);
  typeIn('Ma réponse.');
  await waitFor(() => {
    expect([alert(), free()]).toEqual([CONFLICT, true]);
  });
  return { ...stage, comments, before: comments.writes.length };
}

/** The field written to a host that refuses with a 500: the refusal said, the field free again. */
async function refused(send: () => void) {
  const comments = memoryComments();
  comments.state.failure = unavailable();
  const stage = await onTheSecondStep(comments);
  key('t');
  send();
  await waitFor(() => {
    expect([alert(), free()]).toEqual([UNAVAILABLE, true]);
  });
  return { ...stage, comments };
}

describe('a refusal must have been read before a gesture gives the text up', () => {
  it('keeps the presentation on a second click on the way out right after a refusal, and leaves once it has been read', async () => {
    const { editor, comments } = await refused(() => {
      typeOnly('Demande de congés.');
      press('presenter-exit');
    });
    press('presenter-exit');
    expect([editor.presenter.getState().active, held(), alert(), comments.writes.length]).toEqual([true, 'Demande de congés.', UNAVAILABLE, 1]);
    afterReading();
    press('presenter-exit');
    expect([editor.presenter.getState().active, comments.writes.length]).toEqual([false, 1]);
  });

  it('keeps the field on a second Échap right after a conflict said again, and closes it once it has been read', async () => {
    const { editor, comments, before } = await inConflict();
    forget(editor);
    escape();
    await waitFor(() => {
      expect(alert()).toBe(CONFLICT);
    });
    escape();
    expect([held(), comments.writes.length]).toEqual(['Ma réponse.', before]);
    afterReading();
    escape();
    await closed();
    expect([comments.writes.length, await hosted(comments)]).toEqual([before, 'Texte d’un autre.']);
  });

  it('keeps the field on a second Échap right after a refusal, and closes it once it has been read', async () => {
    const { comments } = await refused(() => {
      typeIn('Demande de congés.');
    });
    escape();
    expect(held()).toBe('Demande de congés.');
    afterReading();
    escape();
    await closed();
    expect(comments.writes.length).toBe(1);
  });
});

describe('a pointer gesture that does nothing brings the cursor back to the field', () => {
  it('says the conflict again on the pencil, the cursor in the field', async () => {
    const { editor, comments, before } = await inConflict();
    forget(editor);
    press('presenter-comment');
    await waitFor(() => {
      expect(alert()).toBe(CONFLICT);
    });
    expect([comments.writes.length, document.activeElement === field()]).toEqual([before, true]);
  });

  it('moves nothing on a second click on « Étape suivante » right after a refusal, the cursor in the field', async () => {
    const { editor } = await refused(() => {
      typeOnly('Demande de congés.');
      press('presenter-next');
    });
    await waitFor(() => {
      expect(document.activeElement === field()).toBe(true);
    });
    press('presenter-next');
    expect([shown(), editor.presenter.getState().writing, field().value, document.activeElement === field()]).toEqual(['s2', 's2', 'Demande de congés.', true]);
  });
});

describe('the conflict is said when the field is free again', () => {
  it('says it again when the message has gone while the comments were read again', async () => {
    const comments = lateComments(ORIGINAL, 2);
    const { editor } = await onTheSecondStep(comments);
    key('t');
    await waitFor(() => {
      expect(comments.reads.length).toBe(2);
    });
    await comments.source.write('s2', 'Texte d’un autre.', 'fr', 1, new AbortController().signal);
    typeIn('Ma réponse.');
    await waitFor(() => {
      expect(alert()).toBe(CONFLICT);
    });
    forget(editor);
    await act(async () => {
      comments.release();
      await Promise.resolve();
    });
    await waitFor(() => {
      expect([alert(), free()]).toEqual([CONFLICT, true]);
    });
  });

  it('keeps the conflict noted when the next Entrée is refused for another reason (witness: already in the code)', async () => {
    const { editor, comments, before } = await inConflict();
    comments.state.failure = unavailable();
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    await waitFor(() => {
      expect([alert(), free()]).toEqual([UNAVAILABLE, true]);
    });
    comments.state.failure = null;
    forget(editor);
    escape();
    expect(comments.writes.length).toBe(before + 1);
    await waitFor(() => {
      expect(alert()).toBe(CONFLICT);
    });
    expect(field().value).toBe('Ma réponse.');
  });
});

describe('the pointer gestures after a conflict write nothing', () => {
  it.each(['presenter-comment', 'presenter-exit', 'presenter-next'])('says the conflict again on %s and keeps the field on its step', async (testId) => {
    const { editor, comments, before } = await inConflict();
    forget(editor);
    press(testId);
    await waitFor(() => {
      expect(alert()).toBe(CONFLICT);
    });
    expect([comments.writes.length, field().value, editor.presenter.getState().writing, editor.presenter.getState().active, shown()]).toEqual([before, 'Ma réponse.', 's2', true, 's2']);
  });
});
