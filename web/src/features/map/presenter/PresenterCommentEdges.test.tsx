// PresenterCommentEdges.test.tsx
// The comment of presentation at its edges (lot J10, independent review): a first reading answered after a comment was kept never takes it away; a flow the story leaves out is shown, and commented, by a story told again from the step it leaves; the field of a step or of a flow whose step has no name names it by its code.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryComments, showEditorWith } from '../../../test/editor.tsx';
import { flowOf, MAP_ID, sampleMap, stepOf } from '../../../test/maps.ts';
import { closed, field, key, line, typeIn } from '../../../test/presenterStage.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

const settle = () => act(async () => {
  for (let turn = 0; turn < 8; turn++) await Promise.resolve();
});

const clickFlow = (flowId: string) => {
  act(() => {
    fireEvent.click(within(screen.getByTestId('presenter-canvas')).getByTestId(`rf__edge-${flowId}`));
  });
};

const label = () => screen.getByTestId('presenter-comment-editor').querySelector('label')?.textContent;

/**
 * Comments whose first reading of a kind is answered with what the host had when it was asked, but only once the test
 * lets it go: the answer of a slow link, older than a comment kept meanwhile.
 */
function staleFirst(kind: 'read' | 'readFlows') {
  const comments = memoryComments();
  const gate: { open: () => void; asked: number } = { open: () => undefined, asked: 0 };
  const opened = new Promise<void>((resolve) => {
    gate.open = resolve;
  });
  const slow = async <Line,>(answer: Promise<readonly Line[]>): Promise<readonly Line[]> => {
    const lines = await answer;
    if (gate.asked++ === 0) await opened;
    return lines;
  };
  const source = kind === 'read'
    ? { ...comments.source, read: (mapId: string, signal: AbortSignal) => slow(comments.source.read(mapId, signal)) }
    : { ...comments.source, readFlows: (mapId: string, signal: AbortSignal) => slow(comments.source.readFlows(mapId, signal)) };
  return { ...comments, source, release: gate.open };
}

describe('a first reading of the comments answered after a comment was kept (lot J10, independent review)', () => {
  it('leaves the comment of a flow just kept: the comments are read again, and the passage still says it', async () => {
    const comments = staleFirst('readFlows');
    const { editor } = await showEditorWith({ comments: comments.source }, sampleMap());
    key('F5');
    await settle();
    clickFlow('f1');
    key('t');
    typeIn('Dit du relais.');
    await closed();
    await waitFor(() => {
      expect(line()).toBe('Dit du relais.');
    });
    comments.release();
    await settle();
    expect([line(), editor.presenter.getState().comments[MAP_ID]?.f1?.map((kept) => kept.text)]).toEqual(['Dit du relais.', ['Dit du relais.']]);
  });

  it('leaves the comment of a step just kept as well', async () => {
    const comments = staleFirst('read');
    const { editor } = await showEditorWith({ comments: comments.source }, sampleMap());
    key('F5');
    await settle();
    key('ArrowRight');
    key('t');
    typeIn('Dit de l’étape.');
    await closed();
    comments.release();
    await settle();
    expect(editor.presenter.getState().comments[MAP_ID]?.s1?.map((kept) => kept.text)).toEqual(['Dit de l’étape.']);
  });
});

describe('a flow the story leaves out, told from a step after it (lot J10, independent review)', () => {
  it('is shown by a story told again from the step it leaves, as a step clicked off the story is, and takes its comment there', async () => {
    const comments = memoryComments();
    const { editor } = await showEditorWith({ comments: comments.source }, sampleMap());
    act(() => {
      editor.presenter.getState().start('s3');
    });
    await settle();
    clickFlow('f1');
    expect(editor.presenter.getState().from).toBe('s1');
    key('t');
    expect(editor.presenter.getState().writing).toBe('f1');
    expect(label()).toBe('Commentaire de présentation : Demande reçue → Vérifier la demande');
    typeIn('Dit du premier relais.');
    await closed();
    expect(comments.flowWrites.map((write) => write.flowId)).toEqual(['f1']);
  });

  it('is followed by the field of a comment being written, its text kept first', async () => {
    const comments = memoryComments();
    const { editor } = await showEditorWith({ comments: comments.source }, sampleMap());
    act(() => {
      editor.presenter.getState().start('s3');
    });
    await settle();
    key('ArrowRight');
    key('t');
    expect(editor.presenter.getState().writing).toBe('s3');
    act(() => {
      fireEvent.change(field(), { target: { value: 'Dit de la décision.' } });
    });
    clickFlow('f1');
    await waitFor(() => {
      expect(editor.presenter.getState().writing).toBe('f1');
    });
    expect([comments.writes.map((write) => write.text), editor.presenter.getState().from]).toEqual([['Dit de la décision.'], 's1']);
  });
});

describe('a flow clicked among several that leave a step together (lot J10, second independent review)', () => {
  /** The sample whose task s2 has two exits: its passage lights both flows together. */
  const forked = () => sampleMap({ flows: [...sampleMap().flows, flowOf('f5', 's2', 's4')] });

  it.each(['f2', 'f5'])('is the one T opens the field of and writes for, then opens again, whichever it is: %s', async (flowId) => {
    const comments = memoryComments();
    const { editor } = await showEditorWith({ comments: comments.source }, forked());
    key('F5');
    await settle();
    clickFlow(flowId);
    key('t');
    expect(editor.presenter.getState().writing).toBe(flowId);
    typeIn('Dit de ce relais.');
    await closed();
    expect(comments.flowWrites.map((write) => write.flowId)).toEqual([flowId]);
    // T again on the same passage opens the flow last pointed at, not the first one the passage lights.
    key('t');
    expect(editor.presenter.getState().writing).toBe(flowId);
  });

  it('is forgotten on another passage, where T opens the flow that passage lights', async () => {
    const { editor } = await showEditorWith({ comments: memoryComments().source }, forked());
    key('F5');
    await settle();
    clickFlow('f5');
    clickFlow('f1');
    key('t');
    expect(editor.presenter.getState().writing).toBe('f1');
  });
});

describe('the field of a comment whose step has no name (lot J10, independent review)', () => {
  /** The sample whose decision is a gateway without a label, as most gateways are. */
  const unnamed = () => sampleMap({ steps: sampleMap().steps.map((step) => (step.id === 's3' ? stepOf('s3', 'l1', 2, { type: 'decision', label: '' }) : step)) });

  it('names the step, and each end of a flow, by its code', async () => {
    const { editor } = await showEditorWith({}, unnamed());
    key('F5');
    await settle();
    clickFlow('f2');
    key('t');
    expect(label()).toBe('Commentaire de présentation : Vérifier la demande → VIS-E003');
    // The field follows a click on the gateway itself, and names it the same way.
    act(() => {
      fireEvent.click(screen.getByTestId('presenter-canvas').querySelector('.react-flow__node[data-id="s3"]') as HTMLElement);
    });
    await waitFor(() => {
      expect(editor.presenter.getState().writing).toBe('s3');
    });
    expect(label()).toBe('Commentaire de présentation : VIS-E003');
  });
});
