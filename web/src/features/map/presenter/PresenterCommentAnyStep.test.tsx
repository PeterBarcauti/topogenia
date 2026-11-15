// PresenterCommentAnyStep.test.tsx
// The comment of presentation on every kind of step a presentation stops on (lot J10, asked by the project owner: « les portes OU/ET/INCLUSIF peuvent aussi avoir un message »): a start, a decision, a parallel gateway, an inclusive one, a wait and an end take a text as a task does, and say it instead of their own sentences.

import { act, fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments, showEditorWith } from '../../../test/editor.tsx';
import { flowOf, laneOf, MAP_ID, mapOf, stepOf } from '../../../test/maps.ts';
import { closed, key, line, typeIn } from '../../../test/presenterStage.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

const shown = () => screen.queryByTestId('presenter-canvas')?.querySelector('.react-flow__node.is-presented')?.getAttribute('data-id') ?? null;
const clickStep = (stepId: string) => {
  fireEvent.click(screen.getByTestId('presenter-canvas').querySelector(`.react-flow__node[data-id="${stepId}"]`) as HTMLElement);
};

/** One step of each type on a line: a start, a wait, a decision, a parallel gateway, an inclusive one, a task, an end. */
const everyType = () => mapOf({
  id: MAP_ID,
  lanes: [laneOf('l1', 0)],
  steps: [
    stepOf('start', 'l1', 0, { type: 'start' }), stepOf('wait', 'l1', 1, { type: 'wait' }), stepOf('decision', 'l1', 2, { type: 'decision' }),
    stepOf('parallel', 'l1', 3, { type: 'parallel' }), stepOf('inclusive', 'l1', 4, { type: 'inclusive' }), stepOf('task', 'l1', 5), stepOf('end', 'l1', 6, { type: 'end' }),
  ],
  flows: [
    flowOf('f1', 'start', 'wait'), flowOf('f2', 'wait', 'decision'), flowOf('f3', 'decision', 'parallel'), flowOf('f4', 'parallel', 'inclusive'),
    flowOf('f5', 'inclusive', 'task'), flowOf('f6', 'task', 'end'),
  ],
});

describe('a comment of presentation on every kind of step (lot J10)', () => {
  it('is typed on a start, a wait, a decision, a parallel gateway, an inclusive one, a task and an end, and each says its own', async () => {
    const comments = memoryComments();
    await showEditorWith({ comments: comments.source }, everyType());
    key('F5');
    await waitFor(() => {
      expect(comments.reads).toEqual([MAP_ID]);
    });
    for (const stepId of ['start', 'wait', 'decision', 'parallel', 'inclusive', 'task', 'end']) {
      act(() => {
        clickStep(stepId);
      });
      expect(shown()).toBe(stepId);
      // The pencil is offered on every stop, whatever the type of its step.
      expect(screen.getByTestId('presenter-comment')).toBeInTheDocument();
      key('t');
      typeIn(`Dit de ${stepId}.`);
      await closed();
      await waitFor(() => {
        expect(line()).toBe(`Dit de ${stepId}.`);
      });
    }
    expect(comments.writes.map((write) => [write.stepId, write.text])).toEqual(
      ['start', 'wait', 'decision', 'parallel', 'inclusive', 'task', 'end'].map((stepId) => [stepId, `Dit de ${stepId}.`]));
  });
});
