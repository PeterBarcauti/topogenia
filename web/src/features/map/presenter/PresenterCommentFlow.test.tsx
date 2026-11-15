// PresenterCommentFlow.test.tsx
// The comment of presentation of a flow (lot J10, asked by the project owner: « pouvoir associer un message au relais sur le même principe que les étapes ; on sélectionne un relais, la boîte de saisie reste ouverte et suit les clics sur étapes ou relais »): typed during the passage that lights the flow, said there instead of its handoff, kept by the route of the flows; its field opened by T, the pencil or a click on the flow, following the clicks on steps and flows.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments, showEditorWith } from '../../../test/editor.tsx';
import { MAP_ID, sampleMap, stepOf, streamOf } from '../../../test/maps.ts';
import { closed, field, key, line, onTheSecondStep, typeIn } from '../../../test/presenterStage.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

const shown = () => screen.queryByTestId('presenter-canvas')?.querySelector('.react-flow__node.is-presented')?.getAttribute('data-id') ?? null;
const clickFlow = (flowId: string) => {
  fireEvent.click(within(screen.getByTestId('presenter-canvas')).getByTestId(`rf__edge-${flowId}`));
};
const clickStep = (stepId: string) => {
  fireEvent.click(screen.getByTestId('presenter-canvas').querySelector(`.react-flow__node[data-id="${stepId}"]`) as HTMLElement);
};
const label = () => screen.getByTestId('presenter-comment-editor').querySelector('label')?.textContent;
const draft = (text: string) => {
  act(() => {
    fireEvent.change(field(), { target: { value: text } });
  });
};

/** The sample whose flow from the check to the decision is a handoff the core counts. */
const handed = () => sampleMap({ value_stream: streamOf({ handoffs: { flows: ['f2'], detailed_steps: {}, count: 1, per_case: null } }) });

/** The stage on the passage from the check to the decision, paused: the scene after the step s2. */
async function onThePassage(comments = memoryComments()) {
  const opened = await onTheSecondStep(comments, handed());
  const step = opened.editor.presenter.getState().player.scene;
  for (let presses = 0; presses < 12 && opened.editor.presenter.getState().player.scene === step; presses++) key('PageDown');
  expect(opened.editor.presenter.getState().player.scene).toBe(step + 1);
  return { ...opened, passage: step + 1 };
}

describe('the comment of presentation of a flow, typed on its passage (lot J10)', () => {
  it('opens on the flow the passage lights, the story staying there; kept by the route of the flows, it is said there instead of the handoff', async () => {
    const { editor, comments, passage } = await onThePassage();
    expect(line()).toBe('Relais : Commercial → Commercial.');
    // The pencil names what it comments: the handoff, on a passage.
    expect(screen.getByTestId('presenter-comment')).toHaveAttribute('aria-label', 'Commentaire du relais');
    key('t');
    expect([editor.presenter.getState().writing, editor.presenter.getState().player.scene]).toEqual(['f2', passage]);
    expect(label()).toBe('Commentaire de présentation : Vérifier la demande → Conforme ?');
    typeIn('Le dossier passe au contrôle.');
    await closed();
    expect([comments.flowWrites, comments.writes]).toEqual([[{ flowId: 'f2', text: 'Le dossier passe au contrôle.', language: 'fr', version: 0 }], []]);
    await waitFor(() => {
      expect(line()).toBe('Le dossier passe au contrôle.');
    });
    expect(editor.presenter.getState().player).toMatchObject({ scene: passage, elapsed: 0, playing: false });
    // An empty text removes it: the passage says its handoff again.
    key('t');
    expect(field().value).toBe('Le dossier passe au contrôle.');
    typeIn('');
    await closed();
    await waitFor(() => {
      expect(line()).toBe('Relais : Commercial → Commercial.');
    });
  });

  it('says the comment a flow already has, read with the map, and the messages of the handoffs turned off silence it as they silence the handoff', async () => {
    const comments = memoryComments();
    comments.giveFlow({ flow_id: 'f2', text: 'Dit du relais.', language: 'fr', source: 'manual', version: 3 });
    const { editor } = await onThePassage(comments);
    await waitFor(() => {
      expect(line()).toBe('Dit du relais.');
    });
    key('r');
    expect([editor.preferences.getState().presenterFlowMessages, line()]).toEqual([false, null]);
    key('r');
    key('t');
    // The version of the line read is the one sent.
    typeIn('Dit autrement.');
    await closed();
    expect(comments.flowWrites).toEqual([{ flowId: 'f2', text: 'Dit autrement.', language: 'fr', version: 3 }]);
  });

  it('on a jump without a flow, goes back to the step the jump leaves, whose comment it opens', async () => {
    // A step tied to nothing is told last: the way to it is a jump.
    const map = handed();
    const comments = memoryComments();
    const { editor } = await showEditorWith({ comments: comments.source }, { ...map, steps: [...map.steps, stepOf('alone', 'l2', 0)] });
    key('F5');
    await waitFor(() => {
      expect(comments.reads).toEqual([MAP_ID]);
    });
    act(() => {
      clickStep('s4');
    });
    const step = editor.presenter.getState().player.scene;
    key('PageDown');
    expect(editor.presenter.getState().player.scene).toBe(step + 1);
    expect(screen.getByTestId('presenter-comment')).toHaveAttribute('aria-label', 'Commentaire de l’étape');
    key('t');
    expect([editor.presenter.getState().writing, editor.presenter.getState().player.scene]).toEqual(['s4', step]);
  });
});

describe('a flow clicked during a presentation, and the field that follows the clicks (lot J10)', () => {
  it('shows the passage of the flow clicked; the field open on a step follows to the flow, its text kept first, then back to a step', async () => {
    const { editor, comments } = await onTheSecondStep(memoryComments(), handed());
    const step = editor.presenter.getState().player.scene;
    // No field open: the click shows the passage that lights the flow.
    act(() => {
      clickFlow('f2');
    });
    expect([editor.presenter.getState().player.scene, editor.presenter.getState().writing]).toEqual([step + 1, null]);
    act(() => {
      clickStep('s2');
    });
    key('t');
    draft('Dit de la vérification.');
    act(() => {
      clickFlow('f2');
    });
    await waitFor(() => {
      expect([editor.presenter.getState().writing, document.activeElement]).toEqual(['f2', field()]);
    });
    expect(comments.writes).toEqual([{ stepId: 's2', text: 'Dit de la vérification.', language: 'fr', version: 0 }]);
    expect([editor.presenter.getState().player.scene, label()]).toEqual([step + 1, 'Commentaire de présentation : Vérifier la demande → Conforme ?']);
    draft('Dit du relais.');
    act(() => {
      clickStep('s3');
    });
    await waitFor(() => {
      expect([editor.presenter.getState().writing, shown()]).toEqual(['s3', 's3']);
    });
    expect(comments.flowWrites).toEqual([{ flowId: 'f2', text: 'Dit du relais.', language: 'fr', version: 0 }]);
  });
});
