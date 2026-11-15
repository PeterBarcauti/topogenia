// PresenterMessagesClock.test.tsx
// The story when the messages of the steps or of the ways between them are cut or given back while a scene is shown (lot J10, independent review): the scene shown starts again with its new text, and the clock never carries the time of the longer text into the scenes after it; nothing is skipped.

import { act, fireEvent, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments, showEditorWith } from '../../../test/editor.tsx';
import { sampleMap } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

const key = (name: string) => {
  act(() => {
    fireEvent.keyDown(window, { key: name });
  });
};

const settle = () => act(async () => {
  for (let turn = 0; turn < 6; turn++) await Promise.resolve();
});

const pass = (ms: number) => {
  act(() => {
    vi.advanceTimersByTime(ms);
  });
};

/** Four seconds of story, frame after frame: the scenes reached, none of them skipped. */
const scenesCrossed = (scene: () => number): number[] => {
  const reached = [scene()];
  for (let frame = 0; frame < 40; frame++) {
    pass(100);
    if (scene() !== reached.at(-1)) reached.push(scene());
  }
  return reached;
};

const LONG = 'Une phrase assez longue pour rester plusieurs secondes devant la salle qui écoute le présentateur.';
const THREE = `${LONG}\n${LONG}\n${LONG}`;

/** The presentation of the sample map, paused on its second step, whose comment is three long sentences. */
const onLongStep = async () => {
  const comments = memoryComments([{ step_id: 's2', text: THREE, language: 'fr', source: 'manual', version: 1 }]);
  const { editor } = await showEditorWith({ comments: comments.source }, sampleMap());
  key('F5');
  await settle();
  key('ArrowRight');
  key('ArrowRight');
  return editor;
};

describe('the messages of the steps cut while a long step is told (lot J10, independent review)', () => {
  it('playing: the step starts again without its text, then the story goes on to the very next scene, skipping nothing', async () => {
    const editor = await onLongStep();
    const player = () => editor.presenter.getState().player;
    const step = player().scene;
    key(' ');
    pass(12_000);
    expect(player().scene).toBe(step);
    key('e');
    expect([player().scene, player().elapsed, player().playing]).toEqual([step, 0, true]);
    pass(120);
    expect(player().scene).toBe(step);
    // A step without a text is shown a moment, then its way to the next one is told, then that step: scene after scene.
    const reached = scenesCrossed(() => player().scene);
    expect(reached.slice(0, 2)).toEqual([step, step + 1]);
    expect(reached).toEqual(reached.map((_, index) => step + index));
  });

  it('paused, then played: the story stays on the step', async () => {
    const editor = await onLongStep();
    const player = () => editor.presenter.getState().player;
    const step = player().scene;
    key(' ');
    pass(12_000);
    key(' ');
    key('e');
    key(' ');
    pass(120);
    expect(player().scene).toBe(step);
  });

  it('by the button of the bar as by the key, and given back the same way: the step says its text again from its first sentence', async () => {
    const editor = await onLongStep();
    const player = () => editor.presenter.getState().player;
    const step = player().scene;
    key(' ');
    pass(12_000);
    act(() => {
      screen.getByTestId('presenter-step-messages').click();
    });
    expect([player().scene, player().elapsed]).toEqual([step, 0]);
    pass(1_000);
    act(() => {
      screen.getByTestId('presenter-step-messages').click();
    });
    expect([player().scene, player().elapsed]).toEqual([step, 0]);
  });

  it('leaves the scene shown as it is when the messages cut are not its own: R on a step, E on a way', async () => {
    const editor = await onLongStep();
    const player = () => editor.presenter.getState().player;
    key(' ');
    pass(5_000);
    const before = player();
    key('r');
    expect(player()).toEqual(before);
  });
});

describe('the messages of the ways cut while a long comment of a flow is told (lot J10, independent review)', () => {
  it('playing: the way starts again without its text, and the story goes on to the step it leads to, not past it', async () => {
    const comments = memoryComments();
    comments.giveFlow({ flow_id: 'f1', text: THREE, language: 'fr', source: 'manual', version: 1 });
    const { editor } = await showEditorWith({ comments: comments.source }, sampleMap());
    const player = () => editor.presenter.getState().player;
    key('F5');
    await settle();
    act(() => {
      fireEvent.click(within(screen.getByTestId('presenter-canvas')).getByTestId('rf__edge-f1'));
    });
    const passage = player().scene;
    key(' ');
    pass(14_000);
    expect(player().scene).toBe(passage);
    key('r');
    expect([player().scene, player().elapsed]).toEqual([passage, 0]);
    pass(120);
    expect(player().scene).toBe(passage);
    const reached = scenesCrossed(() => player().scene);
    expect(reached.slice(0, 2)).toEqual([passage, passage + 1]);
    expect(reached).toEqual(reached.map((_, index) => passage + index));
  });
});
