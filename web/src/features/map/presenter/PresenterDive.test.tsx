// PresenterDive.test.tsx
// The dive of a presentation into a detailed step (F-120, spec of the 10/11/2026): a double click, the button « Voir le détail » of the bar or D presents the map of its detail from its opening, with its own counters; at its end the story comes back to the step, paused, and Play goes on; level by level; Échap leaves the whole presentation, the editor on the map it showed.

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { ApiError } from '../../../api/errors.ts';
import { memoryComments, memoryStorage, renderMapScreen, showEditor } from '../../../test/editor.tsx';
import { flowOf, laneOf, MAP_ID, mapOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { createMapEditor } from '../state/editor.ts';
import type { MapSource } from '../state/mapStore.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.useRealTimers();
});

const key = (name: string) => {
  act(() => {
    fireEvent.keyDown(window, { key: name });
  });
};

/** A map whose step `detailed` opens the map `sub`. */
const detailing = (map: Schema<'MapView'>, detailed: string, sub: string): Schema<'MapView'> => ({
  ...map, steps: map.steps.map((step) => (step.id === detailed ? { ...step, sub_map_id: sub, sub_map_empty: false } : step)),
});

/** The detail of « Vérifier la demande »: a start, two tasks, an end; its task `x3` detailed in its turn when asked. */
function detailOf(id: string, deeper: string | null = null): Schema<'MapView'> {
  const map = mapOf({
    id, name: `Détail ${id}`, lanes: [laneOf('m1', 0, { name: 'Contrôle' })],
    steps: [stepOf('x1', 'm1', 0, { type: 'start', label: 'Dossier reçu' }), stepOf('x2', 'm1', 1, { label: 'Lire le dossier' }),
      stepOf('x3', 'm1', 2, { label: 'Comparer' }), stepOf('x4', 'm1', 3, { type: 'end', label: 'Dossier vérifié' })],
    flows: [flowOf('g1', 'x1', 'x2'), flowOf('g2', 'x2', 'x3'), flowOf('g3', 'x3', 'x4')],
  });
  return deeper === null ? map : detailing(map, 'x3', deeper);
}

const node = (id: string) => screen.getByTestId('presenter-canvas').querySelector<HTMLElement>(`.react-flow__node[data-id="${id}"]`);
const shownMap = (editor: Awaited<ReturnType<typeof showEditor>>['editor']) => editor.presenter.getState().map?.id ?? null;

describe('the dive into a detailed step (F-120)', () => {
  it('presents the map of a detailed step double clicked, from its opening; at its end, back to the step, paused; Play goes on', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    const { editor } = await showEditor(detailing(sampleMap(), 's2', 'sub'), detailOf('sub'));
    key('F5');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    await waitFor(() => {
      expect(shownMap(editor)).toBe('sub');
    });
    const presenter = () => editor.presenter.getState();
    expect([presenter().player.scene, presenter().levels.map((level) => level.stepId), node('x2') !== null, node('s2')]).toEqual([0, ['s2'], true, null]);
    // Its own story to its end, then back to the step left, paused, the story of the map above going on by Play.
    key('End');
    key(' ');
    act(() => {
      vi.advanceTimersByTime(30000);
    });
    expect([shownMap(editor), presenter().levels.length, presenter().player.playing, node('s2') !== null]).toEqual([null, 0, false, true]);
    expect(screen.getByTestId('presenter-canvas').querySelector('.react-flow__node.is-presented')?.getAttribute('data-id')).toBe('s2');
    key(' ');
    expect(presenter().player.playing).toBe(true);
  });

  it('offers « Voir le détail » in the bar on a detailed step only, and D; Échap leaves the whole presentation, the editor on its map', async () => {
    const { editor } = await showEditor(detailing(sampleMap(), 's2', 'sub'), detailOf('sub'), detailOf('sub'));
    key('F5');
    key('ArrowRight');
    expect(screen.queryByTestId('presenter-detail')).toBeNull();
    key('ArrowRight');
    act(() => {
      screen.getByTestId('presenter-detail').click();
    });
    await waitFor(() => {
      expect(shownMap(editor)).toBe('sub');
    });
    key('Escape');
    expect([editor.presenter.getState().active, shownMap(editor), editor.presenter.getState().levels]).toEqual([false, null, []]);
    expect(editor.store.getState().map?.steps.some((step) => step.id === 's2')).toBe(true);
    key('F5');
    key(' ');
    expect(node('s2')).not.toBeNull();
    key(' ');
    key('ArrowRight');
    key('ArrowRight');
    key('d');
    await waitFor(() => {
      expect(shownMap(editor)).toBe('sub');
    });
  });

  it('dives level by level, and comes up one level at a time by the button of the bar', async () => {
    const { editor } = await showEditor(detailing(sampleMap(), 's2', 'sub'), detailOf('sub', 'deep'), detailOf('deep'));
    key('F5');
    key(' ');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    await waitFor(() => {
      expect(shownMap(editor)).toBe('sub');
    });
    fireEvent.doubleClick(node('x3') as HTMLElement);
    await waitFor(() => {
      expect(shownMap(editor)).toBe('deep');
    });
    expect(editor.presenter.getState().levels.map((level) => level.stepId)).toEqual(['s2', 'x3']);
    act(() => {
      screen.getByTestId('presenter-surface').click();
    });
    expect([shownMap(editor), editor.presenter.getState().player.playing]).toEqual(['sub', false]);
    act(() => {
      screen.getByTestId('presenter-surface').click();
    });
    expect([shownMap(editor), screen.queryByTestId('presenter-surface')]).toEqual([null, null]);
  });
});

/** An editor whose detailed map comes only when the test lets it: a slow host (R6-10). */
async function slowDetail() {
  const map = detailing(sampleMap(), 's2', 'sub');
  const gate: { open: () => void; asked: number } = { open: () => undefined, asked: 0 };
  const opened = new Promise<void>((resolve) => {
    gate.open = resolve;
  });
  const source: MapSource = async () => {
    const detail = gate.asked++ > 0;
    if (detail) await opened;
    return { map: detail ? detailOf('sub') : map, history: { undo: [], redo: [] } };
  };
  const editor = createMapEditor({ source, storage: memoryStorage(), notify: () => undefined, comments: memoryComments().source });
  renderMapScreen({ mapId: MAP_ID, editor });
  await screen.findByTestId('map-canvas');
  const arrive = () => act(async () => {
    gate.open();
    await Promise.resolve();
    await Promise.resolve();
  });
  return { editor, arrive };
}

describe('a detailed map that comes late (relecture 6: R6-10)', () => {
  it('is not dived into when the presenter moved on meanwhile', async () => {
    const { editor, arrive } = await slowDetail();
    key('F5');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    key('ArrowRight');
    await arrive();
    expect([shownMap(editor), editor.presenter.getState().levels, node('s3')?.classList.contains('is-presented')]).toEqual([null, [], true]);
  });

  it('is not taken by a presentation left meanwhile', async () => {
    const { editor, arrive } = await slowDetail();
    key('F5');
    key(' ');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    key('Escape');
    await arrive();
    expect([editor.presenter.getState().active, shownMap(editor), editor.presenter.getState().levels]).toEqual([false, null, []]);
  });

  it('is not dived into by a presentation started again meanwhile, even brought back to the same step: Échap then F5 before it comes', async () => {
    const { editor, arrive } = await slowDetail();
    key('F5');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    const { scene } = editor.presenter.getState().player;
    key('Escape');
    key('F5');
    key('ArrowRight');
    key('ArrowRight');
    expect([editor.presenter.getState().player.scene, node('s2')?.classList.contains('is-presented')]).toEqual([scene, true]);
    await arrive();
    expect([editor.presenter.getState().active, shownMap(editor), editor.presenter.getState().levels]).toEqual([true, null, []]);
  });

  it('is not dived into when the story was told again from another step meanwhile, its scene bearing the same number', async () => {
    const { editor, arrive } = await slowDetail();
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    act(() => {
      fireEvent.keyDown(window, { key: 'F5', shiftKey: true });
    });
    key(' ');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    const { scene } = editor.presenter.getState().player;
    // The first step is off a story started from the second: clicked, the story is told again from it.
    fireEvent.click(node('s1') as HTMLElement);
    expect([editor.presenter.getState().from, editor.presenter.getState().player.scene]).toEqual(['s1', scene]);
    await arrive();
    expect([shownMap(editor), editor.presenter.getState().levels]).toEqual([null, []]);
  });
});

describe('a detailed map that comes late while the story plays (relecture 6: R6-10)', () => {
  it('is dived into while the story still shows its step, and the way back is on that step, paused', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    const { editor, arrive } = await slowDetail();
    key('F5');
    key(' ');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    const { scene } = editor.presenter.getState().player;
    act(() => {
      vi.advanceTimersByTime(500);
    });
    await arrive();
    expect(shownMap(editor)).toBe('sub');
    act(() => {
      screen.getByTestId('presenter-surface').click();
    });
    const { player } = editor.presenter.getState();
    expect([shownMap(editor), player.scene, player.playing, node('s2')?.classList.contains('is-presented')]).toEqual([null, scene, false, true]);
  });

  it('is not dived into once the story has left its step', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    const { editor, arrive } = await slowDetail();
    key('F5');
    key(' ');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    const { scene } = editor.presenter.getState().player;
    act(() => {
      vi.advanceTimersByTime(20000);
    });
    expect(editor.presenter.getState().player.scene).toBeGreaterThan(scene);
    await arrive();
    expect([shownMap(editor), editor.presenter.getState().levels]).toEqual([null, []]);
  });
});

describe('the way back from a detailed map by the keyboard (relecture 6: R6-05)', () => {
  it('comes back up one level on U, the key named on the button of the bar; U does nothing on the map of the editor', async () => {
    const { editor } = await showEditor(detailing(sampleMap(), 's2', 'sub'), detailOf('sub'));
    key('F5');
    key(' ');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    await waitFor(() => {
      expect(shownMap(editor)).toBe('sub');
    });
    expect(screen.getByTestId('presenter-surface').getAttribute('title')).toBe('Revenir à la carte parente (U)');
    key('u');
    expect([shownMap(editor), editor.presenter.getState().levels, editor.presenter.getState().player.playing]).toEqual([null, [], false]);
    key('u');
    expect([shownMap(editor), editor.presenter.getState().active]).toEqual([null, true]);
  });
});

describe('a detailed map that cannot be read (relecture 6: R6-02)', () => {
  it('is said in the stage, the story left on the step, and the next gesture takes the message away', async () => {
    const map = detailing(sampleMap(), 's2', 'sub');
    let reads = 0;
    const source: MapSource = () => (reads++ === 0
      ? Promise.resolve({ map, history: { undo: [], redo: [] } })
      : Promise.reject(new ApiError(404, 'INTROUVABLE', 'Cette carte est introuvable.')));
    const editor = createMapEditor({ source, storage: memoryStorage(), notify: () => undefined, comments: memoryComments().source });
    renderMapScreen({ mapId: MAP_ID, editor });
    await screen.findByTestId('map-canvas');
    key('F5');
    fireEvent.doubleClick(node('s2') as HTMLElement);
    const alert = await within(screen.getByTestId('presenter')).findByRole('alert');
    expect(alert).toHaveTextContent('Cette carte est introuvable.');
    expect([shownMap(editor), editor.presenter.getState().levels, node('s2')?.classList.contains('is-presented')]).toEqual([null, [], true]);
    key('ArrowRight');
    expect(within(screen.getByTestId('presenter')).queryByRole('alert')).toBeNull();
  });
});

describe('the dive from a decision shown again for its next exit (lot J10, independent review, M1)', () => {
  it('dives from the stop shown and comes back to it, not to the first stop of that decision', async () => {
    const { editor } = await showEditor(detailing(sampleMap(), 's3', 'sub'), detailOf('sub'));
    key('F5');
    for (let index = 0; index < 4; index++) key('ArrowRight');
    const presenter = () => editor.presenter.getState();
    const again = presenter().player.scene;
    // The decision of the sample is shown twice: by « non », then again by « oui ».
    expect(screen.getByTestId('presenter-line').textContent).toBe('Le récit suit « oui ».');
    key('d');
    await waitFor(() => {
      expect(shownMap(editor)).toBe('sub');
    });
    expect(presenter().levels.map((level) => [level.stepId, level.player.scene])).toEqual([['s3', again]]);
    key('u');
    expect([shownMap(editor), presenter().player.scene]).toEqual([null, again]);
    // A click on the decision shown keeps its stop too.
    fireEvent.click(node('s3') as HTMLElement);
    expect(presenter().player.scene).toBe(again);
  });
});
