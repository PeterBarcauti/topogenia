// PresenterComment.test.tsx
// The comment of presentation of a step in the stage (ADR 099 revised): opened by T or the pencil for those who may change the map, kept by Entrée and said at once, closed by Échap with its text kept (essai J6 of 3.2), refused and kept to be sent again; Échap never ends the presentation while it is written, whatever the browser does of the full screen.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/errors.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { TOAST_DELAYS } from '../../../shared/ui/toasts.ts';
import { memoryComments, showEditorWith } from '../../../test/editor.tsx';
import { MAP_ID, sampleMap } from '../../../test/maps.ts';
import { afterReading, closed, field, fullScreen, key, lateComments, line, onTheSecondStep, typeIn } from '../../../test/presenterStage.tsx';
import { ESCAPE_GRACE_MS } from './escapeKey.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the comment of presentation of a step (ADR 099)', () => {
  it('opens on T on the step shown, the story paused and the subtitle hidden, and Entrée keeps it and says it at once', async () => {
    const { editor, comments } = await onTheSecondStep();
    const automatic = line();
    key('t');
    expect([document.activeElement, screen.queryByTestId('presenter-caption'), editor.presenter.getState().player.playing]).toEqual([field(), null, false]);
    expect(screen.getByText('Commentaire de présentation : Vérifier la demande')).toBeInTheDocument();
    typeIn('Ici le commercial vérifie la demande.\nIl la renvoie si elle est incomplète.', 'Enter', { shiftKey: true });
    // Maj+Entrée goes to the next line: nothing is sent, even once the replies have come (témoin 445).
    await act(async () => {
      await Promise.resolve();
    });
    expect([screen.queryByTestId('presenter-comment-editor') !== null, comments.writes]).toEqual([true, []]);
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes).toEqual([{ stepId: 's2', text: 'Ici le commercial vérifie la demande.\nIl la renvoie si elle est incomplète.', language: 'fr', version: 0 }]);
    expect([line(), automatic === line()]).toEqual(['Ici le commercial vérifie la demande.', false]);
    expect(editor.presenter.getState().comments[MAP_ID]?.s2).toEqual([
      { step_id: 's2', text: 'Ici le commercial vérifie la demande.\nIl la renvoie si elle est incomplète.', language: 'fr', source: 'manual', version: 1 },
    ]);
  });

  it('counts its characters under the field, out of 500, as the summary of a map does (audit 3.2, UX-MOY-04)', async () => {
    await onTheSecondStep(memoryComments([{ step_id: 's2', text: 'Ici on vérifie.', language: 'fr', source: 'manual', version: 1 }]));
    key('t');
    expect(screen.getByTestId('presenter-comment-count')).toHaveTextContent('15 / 500 caractères');
    act(() => {
      fireEvent.change(field(), { target: { value: 'x'.repeat(500) } });
    });
    expect([screen.getByTestId('presenter-comment-count').textContent, field().maxLength]).toEqual(['500 / 500 caractères', 500]);
  });

});

describe('the comment of presentation closed with its text kept, or given up when the host refuses it (essai J6)', () => {
  it('is closed by Échap with its text kept, as Entrée keeps it, the presentation still on; a text left as it was writes nothing (essai J6)', async () => {
    const { editor, comments } = await onTheSecondStep();
    key('t');
    expect(screen.getByTestId('presenter-comment-hint')).toHaveTextContent('Entrée : enregistrer · Maj+Entrée : nouvelle ligne · clic sur une étape ou une flèche : passer à elle · Échap : fermer');
    typeIn('Gardé par Échap.', 'Escape');
    await closed();
    expect([comments.writes, editor.presenter.getState().active, line()]).toEqual([[{ stepId: 's2', text: 'Gardé par Échap.', language: 'fr', version: 0 }], true, 'Gardé par Échap.']);
    key('t');
    act(() => {
      fireEvent.keyDown(field(), { key: 'Escape' });
    });
    expect([screen.queryByTestId('presenter-comment-editor'), comments.writes.length, editor.presenter.getState().active]).toEqual([null, 1, true]);
    expect(screen.getByTestId('presenter-caption')).toBeInTheDocument();
  });

  it('gives up on Échap a text the host has just refused, and sends it again on Entrée: no field holds anybody in it (essai J6)', async () => {
    const comments = memoryComments();
    comments.state.failure = new ApiError(400, 'TEXTE_HTML_INTERDIT', 'Le texte ne doit pas contenir de balise.');
    const { editor } = await onTheSecondStep(comments);
    const editable = () => waitFor(() => {
      expect(field()).not.toHaveAttribute('readonly');
    });
    key('t');
    typeIn('<b>Refusé</b>', 'Escape');
    expect(await within(screen.getByTestId('presenter')).findByRole('alert')).toHaveTextContent('Le texte ne doit pas contenir de balise.');
    await editable();
    expect([field().value, document.activeElement, comments.writes.length]).toEqual(['<b>Refusé</b>', field(), 1]);
    // The key still down repeats, the refusal shown long enough by now: a repeat is no new gesture, and gives nothing up.
    afterReading();
    for (let repeats = 0; repeats < 2; repeats++) {
      act(() => {
        fireEvent.keyDown(field(), { key: 'Escape', repeat: true });
      });
    }
    expect([field().value, comments.writes.length]).toEqual(['<b>Refusé</b>', 1]);
    // Entrée tries again; Échap, on the text just refused, leaves the field and gives the text up.
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    await waitFor(() => {
      expect(comments.writes.length).toBe(2);
    });
    await editable();
    // The refusal read: it has been shown long enough for a gesture to give the text up.
    afterReading();
    act(() => {
      fireEvent.keyDown(field(), { key: 'Escape' });
    });
    expect([screen.queryByTestId('presenter-comment-editor'), comments.writes.length, editor.presenter.getState().active]).toEqual([null, 2, true]);
  });

  it('opens by the pencil of the bar, pressed while it is open, and closes by it', async () => {
    await onTheSecondStep();
    const pencil = screen.getByTestId('presenter-comment');
    expect([pencil.getAttribute('aria-label'), pencil.getAttribute('title'), pencil.getAttribute('aria-pressed')])
      .toEqual(['Commentaire de l’étape', 'Commentaire de l’étape (T)', 'false']);
    act(() => {
      pencil.click();
    });
    expect([screen.queryByTestId('presenter-comment-editor') !== null, pencil.getAttribute('aria-pressed')]).toEqual([true, 'true']);
    act(() => {
      pencil.click();
    });
    expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
  });

  it.each(['reader', 'contributor'] as const)('is not offered to a %s: no pencil, T does nothing', async (role) => {
    await onTheSecondStep(memoryComments(), sampleMap({ rights: { structure: role === 'contributor', role } }));
    expect(screen.queryByTestId('presenter-comment')).toBeNull();
    key('t');
    expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
  });

});

describe('the comment of presentation kept, refused or in another language (ADR 099)', () => {
  it('starts with the comment kept, and an empty text gives the step its own sentences back', async () => {
    const { comments } = await onTheSecondStep(memoryComments([{ step_id: 's2', text: 'Dit par le présentateur.', language: 'fr', source: 'manual', version: 3 }]));
    expect(line()).toBe('Dit par le présentateur.');
    key('t');
    expect(field().value).toBe('Dit par le présentateur.');
    // Writing the original, nothing is recalled above the field: the field holds it.
    expect(screen.queryByTestId('presenter-comment-original')).toBeNull();
    typeIn('   ');
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes).toEqual([{ stepId: 's2', text: '', language: 'fr', version: 3 }]);
    expect(line()).not.toBe('Dit par le présentateur.');
  });

  it('says a refusal in the stage, where the full screen shows it, and keeps the field open with its text to be sent again (R2-02, R6-02)', async () => {
    const comments = memoryComments();
    comments.state.failure = new ApiError(400, 'TEXTE_HTML_INTERDIT', 'Le texte ne doit pas contenir de balise.');
    await onTheSecondStep(comments);
    key('t');
    typeIn('À garder.');
    const alert = await within(screen.getByTestId('presenter')).findByRole('alert');
    expect(alert).toHaveTextContent('Le texte ne doit pas contenir de balise.');
    expect([screen.queryByTestId('presenter-comment-editor') !== null, field().value, document.activeElement]).toEqual([true, 'À garder.', field()]);
  });

  it('keeps the presentation when the full screen is left while a comment is written, and when the browser leaves it on the Échap that closes the field; ends it on a full screen left later (essai J6)', async () => {
    const { editor, comments } = await onTheSecondStep();
    const full = fullScreen();
    try {
      key('t');
      // The browser takes Échap and leaves the full screen, no key given to the page: the presentation stays, the field too (témoin 450).
      full.leave();
      expect([editor.presenter.getState().active, screen.queryByTestId('presenter-comment-editor') !== null]).toEqual([true, true]);
      // Back in full screen, Échap closes the field and the browser leaves the full screen on that same key: the key first, the event after.
      full.enter();
      typeIn('Écrit en plein écran.', 'Escape');
      full.leave();
      await closed();
      expect([editor.presenter.getState().active, comments.writes.map((write) => write.text)]).toEqual([true, ['Écrit en plein écran.']]);
      // Nothing open any more, and that key gone: the full screen left by the browser ends the presentation, as in PowerPoint.
      full.enter();
      full.leave();
      expect(editor.presenter.getState().active).toBe(false);
    } finally {
      full.restore();
    }
  });

});

describe('the full screen left while a comment is written, and the Échap held down (essai J6)', () => {
  it('keeps the presentation when the full screen is left on the release of the Échap that closed the field, a text left as it was; ends it well after that key (essai J6)', async () => {
    const { editor } = await onTheSecondStep();
    const full = fullScreen();
    try {
      key('t');
      act(() => {
        fireEvent.keyDown(field(), { key: 'Escape' });
      });
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
      act(() => {
        fireEvent.keyUp(document.activeElement ?? document.body, { key: 'Escape' });
      });
      full.leave();
      expect(editor.presenter.getState().active).toBe(true);
      // Another field closed by Échap, then a long while: the full screen left is no longer the doing of that key.
      full.enter();
      key('t');
      vi.useFakeTimers({ toFake: ['performance'] });
      act(() => {
        fireEvent.keyDown(field(), { key: 'Escape' });
      });
      vi.advanceTimersByTime(ESCAPE_GRACE_MS + 1);
      full.leave();
      expect(editor.presenter.getState().active).toBe(false);
    } finally {
      full.restore();
    }
  });

  it('does nothing more on an Échap held down: its repeats, the field once closed, never end the presentation; a new press does (essai J6)', async () => {
    const { editor } = await onTheSecondStep();
    key('t');
    act(() => {
      fireEvent.keyDown(field(), { key: 'Escape' });
    });
    expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    // The key still down repeats where the focus now is: on the pencil.
    for (let repeats = 0; repeats < 3; repeats++) {
      act(() => {
        fireEvent.keyDown(document.activeElement ?? document.body, { key: 'Escape', repeat: true });
      });
    }
    expect(editor.presenter.getState().active).toBe(true);
    key('Escape');
    expect(editor.presenter.getState().active).toBe(false);
  });

  it('in the language of origin, writes the original, says its language and shows no original above', async () => {
    await onTheSecondStep(memoryComments([{ step_id: 's2', text: 'Said by the presenter.', language: 'en', source: 'assistant', version: 1 }]));
    key('t');
    // The translation kept in another language is not the text of this field.
    expect(field().value).toBe('');
    expect([screen.queryByTestId('presenter-comment-original'), screen.getByTestId('presenter-comment-language').textContent]).toEqual([null, 'Langue : Français']);
  });

  it('on a passage, opens on the flow the passage lights, the story staying on the passage (lot J10: a flow takes a comment as a step does)', async () => {
    const { editor } = await onTheSecondStep();
    const step = editor.presenter.getState().player.scene;
    for (let presses = 0; presses < 12 && editor.presenter.getState().player.scene === step; presses++) key('PageDown');
    expect(editor.presenter.getState().player.scene).toBe(step + 1);
    key('t');
    expect([editor.presenter.getState().writing, editor.presenter.getState().player.scene]).toEqual(['f2', step + 1]);
  });
});


describe('a comment of presentation left as it was, or read after its field opened (relecture 2: R2-01, R2-08)', () => {
  it('writes nothing on Entrée when the text was left as it was: the field closes, the line of another language stays (R2-01)', async () => {
    const { editor, comments } = await onTheSecondStep(memoryComments([{ step_id: 's2', text: 'Said by the presenter.', language: 'en', source: 'assistant', version: 1 }]));
    key('t');
    expect(field().value).toBe('');
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes).toEqual([]);
    expect(editor.presenter.getState().comments[MAP_ID]?.s2?.map((kept) => [kept.language, kept.text])).toEqual([['en', 'Said by the presenter.']]);
  });

  it('leaves an automatic translation automatic when its field is closed by Entrée without a change (R2-01)', async () => {
    const lines = [
      { step_id: 's2', text: 'Dit par le présentateur.', language: 'fr', source: 'manual', version: 3 },
      { step_id: 's2', text: 'Said by the presenter.', language: 'en', source: 'assistant', version: 2 },
    ];
    const { editor, comments } = await onTheSecondStep(memoryComments(lines), sampleMap(), 'en');
    key('t');
    typeIn('Said by the presenter.  ');
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes).toEqual([]);
    expect(editor.presenter.getState().comments[MAP_ID]?.s2?.map((kept) => [kept.language, kept.source, kept.version])).toEqual([['fr', 'manual', 3], ['en', 'assistant', 2]]);
  });

  it('takes the text read when the comments come after its field opened and nothing was typed, and sends the version it showed (R2-08)', async () => {
    const comments = lateComments([{ step_id: 's2', text: 'Dit par le présentateur.', language: 'fr', source: 'manual', version: 3 }]);
    await showEditorWith({ comments: comments.source }, sampleMap());
    key('F5');
    key('ArrowRight');
    key('ArrowRight');
    key('t');
    expect([field().value, comments.reads]).toEqual(['', []]);
    await act(async () => {
      comments.release();
      await Promise.resolve();
    });
    await waitFor(() => {
      expect(field().value).toBe('Dit par le présentateur.');
    });
    typeIn('Dit autrement.');
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes).toEqual([{ stepId: 's2', text: 'Dit autrement.', language: 'fr', version: 3 }]);
  });
});

describe('a refusal or a failure of the comments said in the stage (relecture 2: R2-02, R2-09; relecture 6: R6-02)', () => {
  const kept = () => [{ step_id: 's2', text: 'Dit par le présentateur.', language: 'fr', source: 'manual', version: 3 }];

  it('says a conflict of version in the stage, reads the comments again, and the second Entrée sends the version read again (R2-09)', async () => {
    const { editor, comments } = await onTheSecondStep(memoryComments(kept()));
    key('t');
    // Another facilitator writes the comment of the step meanwhile: its line is now in version 4.
    await comments.source.write('s2', 'Écrit par un autre animateur.', 'fr', 3, new AbortController().signal);
    typeIn('Dit par moi.');
    const alert = await within(screen.getByTestId('presenter')).findByRole('alert');
    expect(alert).toHaveTextContent('Ce commentaire a été modifié entre-temps.');
    // Read at the start of the presentation, when the field opened, and after the conflict.
    await waitFor(() => {
      expect(comments.reads).toEqual([MAP_ID, MAP_ID, MAP_ID]);
    });
    expect([field().value, editor.presenter.getState().comments[MAP_ID]?.s2?.map((line) => [line.text, line.version])]).toEqual(['Dit par moi.', [['Écrit par un autre animateur.', 4]]]);
    act(() => {
      fireEvent.keyDown(field(), { key: 'Enter' });
    });
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes.slice(1)).toEqual([{ stepId: 's2', text: 'Dit par moi.', language: 'fr', version: 3 }, { stepId: 's2', text: 'Dit par moi.', language: 'fr', version: 4 }]);
    // The second try said nothing more: what the first one said is gone with it.
    expect([line(), within(screen.getByTestId('presenter')).queryByRole('alert')]).toEqual(['Dit par moi.', null]);
  });

  it('sends the version its field showed, not the one read after a text was typed: the comment read late is not replaced unseen (R2-08)', async () => {
    const comments = lateComments(kept());
    await showEditorWith({ comments: comments.source }, sampleMap());
    key('F5');
    key('ArrowRight');
    key('ArrowRight');
    key('t');
    act(() => {
      fireEvent.change(field(), { target: { value: 'Tapé avant la lecture.' } });
    });
    await act(async () => {
      comments.release();
      await Promise.resolve();
    });
    await waitFor(() => {
      expect(comments.reads).toEqual([MAP_ID, MAP_ID]);
    });
    typeIn('Tapé avant la lecture.');
    expect(await within(screen.getByTestId('presenter')).findByRole('alert')).toHaveTextContent('Ce commentaire a été modifié entre-temps.');
    expect([field().value, comments.writes]).toEqual(['Tapé avant la lecture.', [{ stepId: 's2', text: 'Tapé avant la lecture.', language: 'fr', version: 0 }]]);
  });

  it('says in the stage that the comments could not be read, until the next gesture or after the time of an error', async () => {
    const read = () => Promise.reject(new ApiError(500, 'ERREUR_INTERNE', 'Les commentaires sont illisibles.'));
    const flush = () => act(async () => {
      await Promise.resolve();
    });
    const { editor } = await showEditorWith({ comments: { ...memoryComments().source, read } }, sampleMap());
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const alert = () => within(screen.getByTestId('presenter')).queryByRole('alert')?.textContent ?? null;
    key('F5');
    key(' ');
    await flush();
    expect(alert()).toBe('Les commentaires sont illisibles.');
    act(() => {
      vi.advanceTimersByTime(TOAST_DELAYS.error);
    });
    expect([alert(), editor.presenter.getState().active]).toEqual([null, true]);
    key('Escape');
    key('F5');
    key(' ');
    await flush();
    expect(alert()).toBe('Les commentaires sont illisibles.');
    key('ArrowRight');
    expect(alert()).toBeNull();
  });
});

