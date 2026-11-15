// PresenterCommentRead.test.tsx
// The comments of presentation read again, late or not at all, when the field of a comment opens; the step told again from the comment kept; the keys of the stage while a comment is open and the focus out of its field.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/errors.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments } from '../../../test/editor.tsx';
import { MAP_ID } from '../../../test/maps.ts';
import { closed, field, key, lateComments, line, onTheSecondStep, typeIn } from '../../../test/presenterStage.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the comments read again when the field of a comment opens (R1-02, interface)', () => {
  const kept = (version: number) => [{ step_id: 's2', text: 'Dit par le présentateur.', language: 'fr', source: 'manual', version }];
  const typed = (text: string) => {
    act(() => {
      fireEvent.change(field(), { target: { value: text } });
    });
  };
  const released = async (comments: ReturnType<typeof lateComments>) => {
    await act(async () => {
      comments.release();
      await Promise.resolve();
    });
    await waitFor(() => {
      expect(comments.reads).toEqual([MAP_ID, MAP_ID]);
    });
  };

  it('shows the line of now, not the one read at the start: removed then written again by another, it has the same version and another text', async () => {
    const { comments } = await onTheSecondStep(memoryComments(kept(1)));
    // Another facilitator removes the comment, then writes it again: its line is in version 1 again.
    await comments.source.write('s2', '', 'fr', 1, new AbortController().signal);
    await comments.source.write('s2', 'Récrit par un autre animateur.', 'fr', 0, new AbortController().signal);
    key('t');
    await waitFor(() => {
      expect(field().value).toBe('Récrit par un autre animateur.');
    });
    expect(comments.reads).toEqual([MAP_ID, MAP_ID]);
    typeIn('Récrit par un autre animateur, puis complété.');
    await closed();
    expect(comments.writes.at(-1)).toEqual({ stepId: 's2', text: 'Récrit par un autre animateur, puis complété.', language: 'fr', version: 1 });
  });

  it('keeps the text typed before the comments read again come; the line having another text, it sends the version it showed and the host refuses it', async () => {
    const comments = lateComments(kept(3), 1);
    await onTheSecondStep(comments);
    await comments.source.write('s2', 'Écrit par un autre animateur.', 'fr', 3, new AbortController().signal);
    key('t');
    expect(field().value).toBe('Dit par le présentateur.');
    typed('Tapé avant la relecture.');
    await released(comments);
    expect(field().value).toBe('Tapé avant la relecture.');
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    expect(await within(screen.getByTestId('presenter')).findByRole('alert')).toHaveTextContent('Ce commentaire a été modifié entre-temps.');
    expect([field().value, comments.writes.at(-1)]).toEqual(['Tapé avant la relecture.', { stepId: 's2', text: 'Tapé avant la relecture.', language: 'fr', version: 3 }]);
  });

  it('sends the version read again when the text its field showed is still the one of the line: no conflict for a line written again alike', async () => {
    const comments = lateComments(kept(3), 1);
    await onTheSecondStep(comments);
    await comments.source.write('s2', '', 'fr', 3, new AbortController().signal);
    await comments.source.write('s2', 'Dit par le présentateur.', 'fr', 0, new AbortController().signal);
    key('t');
    typed('Dit par le présentateur, puis complété.');
    await released(comments);
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    await closed();
    expect(comments.writes.at(-1)).toEqual({ stepId: 's2', text: 'Dit par le présentateur, puis complété.', language: 'fr', version: 1 });
  });

  it('lets the comment be written when they cannot be read again: the field keeps what it had, the failure said as the one of the start is', async () => {
    const comments = memoryComments(kept(3));
    let asked = 0;
    const read: typeof comments.source.read = (mapId, signal) => (asked++ === 0
      ? comments.source.read(mapId, signal)
      : Promise.reject(new ApiError(500, 'ERREUR_INTERNE', 'Les commentaires sont illisibles.')));
    await onTheSecondStep({ ...comments, source: { ...comments.source, read } });
    key('t');
    expect(await within(screen.getByTestId('presenter')).findByRole('alert')).toHaveTextContent('Les commentaires sont illisibles.');
    expect(field().value).toBe('Dit par le présentateur.');
    typeIn('Dit autrement.');
    await closed();
    expect(comments.writes).toEqual([{ stepId: 's2', text: 'Dit autrement.', language: 'fr', version: 3 }]);
  });
});

describe('the step told again from its comment once kept (relecture 2: R2-04)', () => {
  it('goes back to the first sentence of the comment just kept, the time of its scene at zero, paused: a comment shorter than the time already played is said whole', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    const long = [
      'Le commercial vérifie chaque pièce du dossier avant de le transmettre.', 'Il renvoie la demande au client dès qu’une pièce manque au dossier.',
      'Il note enfin la date de réception dans le tableau de suivi partagé.',
    ].join('\n');
    const { editor } = await onTheSecondStep(memoryComments([{ step_id: 's2', text: long, language: 'fr', source: 'manual', version: 1 }]));
    const { scene } = editor.presenter.getState().player;
    // The story plays six seconds of the step: it is at the second of its three sentences.
    key(' ');
    act(() => {
      vi.advanceTimersByTime(6000);
    });
    expect([editor.presenter.getState().player.scene, line()]).toEqual([scene, 'Il renvoie la demande au client dès qu’une pièce manque au dossier.']);
    key('t');
    typeIn('Première ligne.\nSeconde ligne.');
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    const { player } = editor.presenter.getState();
    expect([line(), player.scene, player.elapsed, player.playing]).toEqual(['Première ligne.', scene, 0, false]);
  });
});

describe('a comment open, the focus out of its field (relecture 2: R2-05, R2-10; relecture 6: R6-11)', () => {
  /** A key pressed where the focus is, as a keyboard sends it. */
  const press = (name: string) => {
    act(() => {
      fireEvent.keyDown(document.activeElement ?? document.body, { key: name });
    });
  };
  const draft = (text: string) => {
    act(() => {
      fireEvent.change(field(), { target: { value: text } });
    });
  };

  it('lets no key drive the story while a comment is open, wherever the focus is, and Échap closes the field, not the presentation', async () => {
    const { editor, comments } = await onTheSecondStep();
    const { scene } = editor.presenter.getState().player;
    key('t');
    draft('Brouillon');
    act(() => {
      screen.getByTestId('presenter-restart').focus();
    });
    for (const name of ['b', 'n', 'Enter', ' ', 'ArrowRight', 't', 'F5', 'End']) press(name);
    const state = editor.presenter.getState();
    expect([state.screen, state.player.scene, state.player.playing, state.writing, field().value, comments.writes]).toEqual(['map', scene, false, 's2', 'Brouillon', []]);
    // Échap, the focus on a button of the bar: the field closes all the same, its text kept (essai J6).
    press('Escape');
    await closed();
    expect([comments.writes.map((write) => write.text), editor.presenter.getState().active]).toEqual([['Brouillon'], true]);
  });

  it('does nothing on Lecture while a comment is written: the story stays paused on its step, the text stays, nothing is written', async () => {
    const { editor, comments } = await onTheSecondStep();
    const { scene } = editor.presenter.getState().player;
    key('t');
    draft('Brouillon');
    act(() => {
      screen.getByTestId('presenter-play').click();
    });
    const state = editor.presenter.getState();
    expect([state.player.scene, state.player.playing, state.writing, field().value, comments.writes]).toEqual([scene, false, 's2', 'Brouillon', []]);
  });

  it('gives the focus back to the pencil when the field closes, left by Échap or kept by Entrée; Espace then plays the story, it does not open the field again (R2-10)', async () => {
    const { editor } = await onTheSecondStep();
    key('t');
    typeIn('', 'Escape');
    expect(document.activeElement).toBe(screen.getByTestId('presenter-comment'));
    key('t');
    typeIn('Dit par le présentateur.');
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(document.activeElement).toBe(screen.getByTestId('presenter-comment'));
    press(' ');
    expect([editor.presenter.getState().player.playing, screen.queryByTestId('presenter-comment-editor')]).toEqual([true, null]);
  });
});
