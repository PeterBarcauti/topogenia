// PresenterCommentKeyed.test.tsx
// What the fourth critical reading of the comment of presentation found (13/11/2026): a button of the bar pressed by the keyboard keeps the focus after a refusal or a conflict said again, so that the same key pressed again gives the text up and never writes over the text of another; and the second click of a double click on a step of the map moves nothing.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/errors.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments } from '../../../test/editor.tsx';
import { MAP_ID } from '../../../test/maps.ts';
import { afterReading, closed, field, key, onTheSecondStep } from '../../../test/presenterStage.tsx';
import type { PresentationComment } from './comments.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
});

const CONFLICT = 'Ce commentaire a été modifié entre-temps.';
const UNAVAILABLE = 'Le service est momentanément indisponible.';
const ORIGINAL: PresentationComment[] = [{ step_id: 's2', text: 'Texte d’origine.', language: 'fr', source: 'manual', version: 1 }];

const alert = () => within(screen.getByTestId('presenter')).queryByRole('alert')?.textContent ?? null;
const shown = () => screen.queryByTestId('presenter-canvas')?.querySelector('.react-flow__node.is-presented')?.getAttribute('data-id') ?? null;
/** What the field holds, null once it is closed. */
const held = () => screen.queryByTestId<HTMLTextAreaElement>('presenter-comment-field')?.value ?? null;
const free = () => !field().hasAttribute('readonly');
const typeOnly = (text: string) => {
  act(() => {
    fireEvent.change(field(), { target: { value: text } });
  });
};
const clickStep = (stepId: string) => {
  fireEvent.click(screen.getByTestId('presenter-canvas').querySelector(`.react-flow__node[data-id="${stepId}"]`) as HTMLElement);
};
const hosted = async (comments: ReturnType<typeof memoryComments>) =>
  (await comments.source.read(MAP_ID, new AbortController().signal)).find((kept) => kept.step_id === 's2')?.text;

/** Walks the controls of the stage with Tab until the pencil has the focus, as a person without a pointer does. */
const toThePencil = () => {
  const pencil = screen.getByTestId('presenter-comment');
  for (let walked = 0; walked < 40 && document.activeElement !== pencil; walked++) key('Tab');
  expect(document.activeElement).toBe(pencil);
  return pencil;
};

/** The field open on a line another person changes meanwhile, a text typed in it and not sent yet. */
async function overAnother() {
  const comments = memoryComments(ORIGINAL);
  const stage = await onTheSecondStep(comments);
  key('t');
  await waitFor(() => {
    expect(comments.reads.length).toBe(2);
  });
  await comments.source.write('s2', 'Texte d’un autre.', 'fr', 1, new AbortController().signal);
  typeOnly('Ma réponse.');
  return { ...stage, comments };
}

describe('a button of the bar pressed by the keyboard keeps the focus (fourth critical reading of 13/11/2026)', () => {
  it('stays on the pencil pressed by Entrée when the host answers by a conflict: Entrée again, the conflict read, gives the text up', async () => {
    const { comments } = await overAnother();
    const pencil = toThePencil();
    key('Enter');
    await waitFor(() => {
      expect([alert(), free()]).toEqual([CONFLICT, true]);
    });
    const before = comments.writes.length;
    expect([held(), document.activeElement]).toEqual(['Ma réponse.', pencil]);
    afterReading();
    key('Enter');
    await closed();
    expect([comments.writes.length, await hosted(comments)]).toEqual([before, 'Texte d’un autre.']);
  });

  it('stays on the pencil when Entrée on it says again a conflict whose message had gone: Entrée again gives the text up', async () => {
    const { editor, comments } = await overAnother();
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    await waitFor(() => {
      expect([alert(), free()]).toEqual([CONFLICT, true]);
    });
    const before = comments.writes.length;
    act(() => {
      editor.presenter.getState().say(null);
    });
    const pencil = toThePencil();
    key('Enter');
    await waitFor(() => {
      expect(alert()).toBe(CONFLICT);
    });
    expect([comments.writes.length, held(), document.activeElement]).toEqual([before, 'Ma réponse.', pencil]);
    afterReading();
    key('Enter');
    await closed();
    expect([comments.writes.length, await hosted(comments)]).toEqual([before, 'Texte d’un autre.']);
  });
});

describe('the second click of a double click on a step of the map (fourth critical reading of 13/11/2026)', () => {
  it('moves nothing right after the refusal of the text the first click sent, and goes once the refusal has been read', async () => {
    const comments = memoryComments();
    comments.state.failure = new ApiError(500, 'ERREUR_INTERNE', UNAVAILABLE);
    await onTheSecondStep(comments);
    key('t');
    typeOnly('Demande de congés.');
    clickStep('s3');
    await waitFor(() => {
      expect([alert(), free()]).toEqual([UNAVAILABLE, true]);
    });
    clickStep('s3');
    expect([shown(), held(), comments.writes.length]).toEqual(['s2', 'Demande de congés.', 1]);
    afterReading();
    clickStep('s3');
    await waitFor(() => {
      expect([shown(), held()]).toEqual(['s3', '']);
    });
    expect(comments.writes.length).toBe(1);
  });
});
