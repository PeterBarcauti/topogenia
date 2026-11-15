// PresenterCommentFollow.test.tsx
// The field of the comment of presentation that follows the step shown by the pointer (a step, a chapter, the bar), keeps its text before every way out, and the translation of a comment, the map shown in another language (ADR 099 revised).

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/errors.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments, showEditorWith } from '../../../test/editor.tsx';
import { MAP_ID, sampleMap } from '../../../test/maps.ts';
import { afterReading, closed, field, key, line, onTheSecondStep, typeIn } from '../../../test/presenterStage.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

const draft = (text: string) => {
  act(() => {
    fireEvent.change(field(), { target: { value: text } });
  });
};
const click = (testId: string) => {
  act(() => {
    screen.getByTestId(testId).click();
  });
};
const clickStep = (stepId: string) => {
  fireEvent.click(screen.getByTestId('presenter-canvas').querySelector(`.react-flow__node[data-id="${stepId}"]`) as HTMLElement);
};
const shown = () => screen.queryByTestId('presenter-canvas')?.querySelector('.react-flow__node.is-presented')?.getAttribute('data-id') ?? null;
const written = (comments: ReturnType<typeof memoryComments>) => comments.writes.map((write) => [write.stepId, write.text]);
/** Waits until the field is open on a step with the cursor in it, then says what it shows: its label and its text. */
const fieldOn = async (stepId: string) => {
  await waitFor(() => {
    expect([shown(), document.activeElement]).toEqual([stepId, field()]);
  });
  return [screen.getByTestId('presenter-comment-editor').querySelector('label')?.textContent, field().value];
};


describe('the field of a comment follows the step shown by the pointer, as the notes of a slide follow it (ADR 099 revised, essai J6)', () => {
  it('keeps the text typed and opens on the step clicked on the map, its own comment in it, the cursor in it, the story paused there', async () => {
    const { editor, comments } = await onTheSecondStep(memoryComments([{ step_id: 's3', text: 'Déjà dit de la décision.', language: 'fr', source: 'manual', version: 2 }]));
    key('t');
    draft('Dit de la vérification.');
    clickStep('s3');
    expect(await fieldOn('s3')).toEqual(['Commentaire de présentation : Conforme ?', 'Déjà dit de la décision.']);
    expect(comments.writes).toEqual([{ stepId: 's2', text: 'Dit de la vérification.', language: 'fr', version: 0 }]);
    const state = editor.presenter.getState();
    expect([state.writing, state.player.playing, screen.getByTestId('presenter-comment').getAttribute('aria-pressed')]).toEqual(['s3', false, 'true']);
  });

  it('follows a chapter and the steps before and after of the bar; a text left as it was writes nothing and the field follows all the same', async () => {
    const { comments } = await onTheSecondStep();
    key('t');
    draft('Deux.');
    click('presenter-next');
    expect(await fieldOn('s3')).toEqual(['Commentaire de présentation : Conforme ?', '']);
    draft('Trois.');
    click('presenter-chapter-0');
    expect(await fieldOn('s1')).toEqual(['Commentaire de présentation : Demande reçue', '']);
    click('presenter-next');
    expect(await fieldOn('s2')).toEqual(['Commentaire de présentation : Vérifier la demande', 'Deux.']);
    click('presenter-previous');
    expect(await fieldOn('s1')).toEqual(['Commentaire de présentation : Demande reçue', '']);
    expect(written(comments)).toEqual([['s2', 'Deux.'], ['s3', 'Trois.']]);
  });

  it('follows a step clicked off the story told: the story is told again from it, the field on it', async () => {
    const comments = memoryComments();
    const { editor } = await showEditorWith({ comments: comments.source }, sampleMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    key('F5', { shiftKey: true });
    key('ArrowRight');
    key('t');
    draft('Deux.');
    clickStep('s1');
    expect(await fieldOn('s1')).toEqual(['Commentaire de présentation : Demande reçue', '']);
    expect([editor.presenter.getState().from, editor.presenter.getState().writing, written(comments)]).toEqual(['s1', 's1', [['s2', 'Deux.']]]);
  });

  it('closes, the text kept, where the scene reached has no step: back to the start, and past the last step', async () => {
    const { editor, comments } = await onTheSecondStep();
    key('t');
    draft('Deux.');
    click('presenter-restart');
    await closed();
    expect([editor.presenter.getState().player.scene, written(comments)]).toEqual([0, [['s2', 'Deux.']]]);
    // The last step: the mark after the one of the decision, which is shown twice and has one mark (lot J10).
    click('presenter-chapter-4');
    key('t');
    draft('Quatre.');
    click('presenter-next');
    await closed();
    expect([shown(), written(comments)]).toEqual([null, [['s2', 'Deux.'], ['s4', 'Quatre.']]]);
  });

});

describe('the field of a comment followed to the step reached, refused or given up, and the ways out that keep its text (essai J6)', () => {
  it('stays on its step when the host refuses the text: the refusal said, the text still in the field; the same gesture again gives the text up and goes', async () => {
    const comments = memoryComments();
    comments.state.failure = new ApiError(400, 'TEXTE_HTML_INTERDIT', 'Le texte ne doit pas contenir de balise.');
    const { editor } = await onTheSecondStep(comments);
    const { scene } = editor.presenter.getState().player;
    key('t');
    draft('<b>Refusé</b>');
    clickStep('s3');
    expect(await within(screen.getByTestId('presenter')).findByRole('alert')).toHaveTextContent('Le texte ne doit pas contenir de balise.');
    await waitFor(() => {
      expect(field()).not.toHaveAttribute('readonly');
    });
    const state = editor.presenter.getState();
    expect([state.writing, state.player.scene, field().value, document.activeElement]).toEqual(['s2', scene, '<b>Refusé</b>', field()]);
    // The refusal read: it has been shown long enough for a gesture to give the text up.
    afterReading();
    clickStep('s3');
    expect(await fieldOn('s3')).toEqual(['Commentaire de présentation : Conforme ?', '']);
    expect([comments.writes.length, editor.presenter.getState().comments[MAP_ID]?.s2]).toEqual([1, undefined]);
  });

  it('stays open on its step, the text kept, when that very step is clicked: the cursor comes back in its field', async () => {
    const { editor, comments } = await onTheSecondStep();
    key('t');
    draft('Deux.');
    act(() => {
      screen.getByTestId('presenter-restart').focus();
    });
    clickStep('s2');
    await waitFor(() => {
      expect(written(comments)).toEqual([['s2', 'Deux.']]);
    });
    expect(await fieldOn('s2')).toEqual(['Commentaire de présentation : Vérifier la demande', 'Deux.']);
    expect(editor.presenter.getState().writing).toBe('s2');
    // Clicked again, its text left as it was: nothing is sent, and the cursor comes back all the same.
    act(() => {
      screen.getByTestId('presenter-restart').focus();
    });
    clickStep('s2');
    expect(await fieldOn('s2')).toEqual(['Commentaire de présentation : Vérifier la demande', 'Deux.']);
    expect(written(comments)).toEqual([['s2', 'Deux.']]);
  });

  it('keeps the text before the pencil closes the field and before the way out leaves the presentation', async () => {
    const { editor, comments } = await onTheSecondStep();
    key('t');
    draft('Par le crayon.');
    click('presenter-comment');
    await closed();
    key('t');
    draft('Par la sortie.');
    click('presenter-exit');
    await waitFor(() => {
      expect(editor.presenter.getState().active).toBe(false);
    });
    expect(written(comments)).toEqual([['s2', 'Par le crayon.'], ['s2', 'Par la sortie.']]);
  });

  it('stays in the presentation when the host refuses the text on the way out, the refusal said; the way out again leaves, the text given up', async () => {
    const comments = memoryComments();
    comments.state.failure = new ApiError(400, 'TEXTE_HTML_INTERDIT', 'Le texte ne doit pas contenir de balise.');
    const { editor } = await onTheSecondStep(comments);
    key('t');
    draft('<b>Refusé</b>');
    click('presenter-exit');
    expect(await within(screen.getByTestId('presenter')).findByRole('alert')).toHaveTextContent('Le texte ne doit pas contenir de balise.');
    await waitFor(() => {
      expect(field()).not.toHaveAttribute('readonly');
    });
    expect([editor.presenter.getState().active, field().value]).toEqual([true, '<b>Refusé</b>']);
    // The refusal read: it has been shown long enough for a gesture to give the text up.
    afterReading();
    click('presenter-exit');
    expect([editor.presenter.getState().active, comments.writes.length]).toEqual([false, 1]);
  });
});

describe('the translation of a comment of presentation, the map shown in another language (ADR 099 revised)', () => {
  const lines = () => [
    { step_id: 's2', text: 'Dit par le présentateur.', language: 'fr', source: 'manual', version: 3 },
    { step_id: 's2', text: 'Said by the presenter.', language: 'en', source: 'assistant', version: 2 },
  ];

  it('says the translation, starts with it, shows the original above and writes it with its own version', async () => {
    const { editor, comments } = await onTheSecondStep(memoryComments(lines()), sampleMap(), 'en');
    expect(line()).toBe('Said by the presenter.');
    key('t');
    expect(field().value).toBe('Said by the presenter.');
    expect([field().lang, screen.getByTestId('presenter-comment-original').textContent, screen.getByTestId('presenter-comment-language').textContent])
      .toEqual(['en', 'Original (Français)Dit par le présentateur.', 'Langue : Anglais']);
    typeIn('Said better by the presenter.');
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes).toEqual([{ stepId: 's2', text: 'Said better by the presenter.', language: 'en', version: 2 }]);
    expect(line()).toBe('Said better by the presenter.');
    // The original stays as it was.
    expect(editor.presenter.getState().comments[MAP_ID]?.s2?.map((kept) => [kept.language, kept.text, kept.version])).toEqual([
      ['fr', 'Dit par le présentateur.', 3], ['en', 'Said better by the presenter.', 3],
    ]);
  });

  it('emptied, removes this translation only: the original stays', async () => {
    const { editor, comments } = await onTheSecondStep(memoryComments(lines()), sampleMap(), 'en');
    key('t');
    typeIn('  ');
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes).toEqual([{ stepId: 's2', text: '', language: 'en', version: 2 }]);
    expect(editor.presenter.getState().comments[MAP_ID]?.s2?.map((kept) => kept.language)).toEqual(['fr']);
    expect(line()).not.toBe('Said by the presenter.');
  });

  it('without an original, writes the translation alone and shows none above', async () => {
    const { comments } = await onTheSecondStep(memoryComments(), sampleMap(), 'en');
    key('t');
    expect([field().value, screen.queryByTestId('presenter-comment-original')]).toEqual(['', null]);
    typeIn('Only in English.');
    await waitFor(() => {
      expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
    });
    expect(comments.writes).toEqual([{ stepId: 's2', text: 'Only in English.', language: 'en', version: 0 }]);
    expect(line()).toBe('Only in English.');
  });
});

