// laneBorder.ts
// The border between two lane headers, every gesture on it in one place (ADR 096): pulled, a lane taller by a row each height of a row pulled, what is under the border going down with the hand; double-clicked, the empty row on each side of it taken out; the « + » on it clicked, a lane added there once no double click came; Escape gives a pull up.

import { useEffect, useState, type PointerEvent } from 'react';
import type { Schema } from '../../../api/client.ts';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import { DOUBLE_CLICK_MS, PULL_THRESHOLD } from '../model/geometry/constants.ts';
import type { LaneRows } from '../model/gridShape.ts';
import { pulledLane, roomOf, rowsPulled, type Pulled, type RowsDraft } from '../model/laneRows.ts';
import type { LaneBand } from '../model/mapLayout.ts';
import { addLane, growLane, shrinkLane } from '../state/actions/lanes.ts';
import type { MapEditor } from '../state/editor.ts';

/** What a pull aims at: the lane and its end, and the rows it would give. */
export interface PullAim extends Pulled {
  readonly count: number;
}

/**
 * The aim of a border pulled by `dy` pixels of the screen: a row each height of a row at the zoom of the view, bounded by
 * the room the lane had when the pull started (its map and its rows before the pull, never the ones drawn meanwhile);
 * null when the pull aims at no lane or gives no row.
 */
export function pullAim(map: Pick<Schema<'MapView'>, 'steps' | 'flows'>, rows: LaneRows, lanes: readonly LaneBand[], zoom: number, border: number, dy: number): PullAim | null {
  const pulled = pulledLane(lanes.map((band) => band.lane.id), border, dy);
  const band = pulled === null ? undefined : lanes.find((candidate) => candidate.lane.id === pulled.laneId);
  if (pulled === null || band === undefined) return null;
  const count = Math.min(rowsPulled(dy, (band.height / band.rows) * zoom), roomOf(map, pulled.laneId, rows));
  return count > 0 ? { ...pulled, count } : null;
}

/** A border held: its rank from the top of the first lane, where the pointer went down, whether it went past 4 px. */
interface Held {
  readonly border: number;
  readonly startY: number;
  readonly far: boolean;
}

const sameDraft = (a: RowsDraft | null, b: RowsDraft | null): boolean =>
  a === b || (a !== null && b !== null && a.laneId === b.laneId && a.end === b.end && a.count === b.count && a.sent === b.sent && a.version === b.version);

/** Never drawn: whether the last press went past 4 px, read by its click, and the lane added once no double click came. */
interface Press {
  pulled: boolean;
  adding: ReturnType<typeof setTimeout> | undefined;
}

/** What the gestures of the borders read: the editor, the lanes drawn and the zoom, the border held and the press. */
interface BorderContext {
  readonly editor: MapEditor;
  readonly lanes: readonly LaneBand[];
  readonly zoom: number;
  readonly held: Held | null;
  readonly setHeld: (held: Held | null) => void;
  readonly press: Press;
}

/** A pull given up, by Escape or a lost pointer: nothing drawn any more, unless its rows were already sent. */
function giveUp({ editor, setHeld }: Pick<BorderContext, 'editor' | 'setHeld'>): void {
  setHeld(null);
  if (editor.store.getState().rowsDraft?.sent === false) editor.store.setState({ rowsDraft: null });
}

/** The draft of a pull past 4 px: what the lane would gain, on the map it started on. */
function draftOf({ editor, lanes, zoom }: BorderContext, held: Held, dy: number): RowsDraft | null {
  const { map, rowsAtLeast } = editor.store.getState();
  const aim = map === null || !held.far ? null : pullAim(map, rowsAtLeast, lanes, zoom, held.border, dy);
  return aim === null || map === null ? null : { ...aim, sent: false, version: map.structure_version };
}

/** The pull itself, with the capture of the pointer: pressed, moved (the map drawn as it would leave it), released (sent). */
function pullHandlers(context: BorderContext) {
  const { editor, held, setHeld, press } = context;
  return {
    start: (event: PointerEvent<HTMLElement>, border: number) => {
      if (event.button !== 0 || editor.store.getState().rowsDraft?.sent === true) return;
      event.currentTarget.setPointerCapture(event.pointerId);
      press.pulled = false;
      setHeld({ border, startY: event.clientY, far: false });
    },
    move: (event: PointerEvent<HTMLElement>) => {
      if (held === null) return;
      const dy = event.clientY - held.startY;
      const state = held.far || Math.abs(dy) < PULL_THRESHOLD ? held : { ...held, far: true };
      press.pulled = state.far;
      if (state !== held) setHeld(state);
      const next = draftOf(context, state, dy);
      if (!sameDraft(editor.store.getState().rowsDraft, next)) editor.store.setState({ rowsDraft: next });
    },
    end: () => {
      if (held === null) return;
      setHeld(null);
      const draft = editor.store.getState().rowsDraft;
      if (draft === null || draft.sent) return;
      editor.store.setState({ rowsDraft: { ...draft, sent: true } });
      void growLane(editor, draft.laneId, draft.end, draft.count).finally(() => {
        if (editor.store.getState().rowsDraft !== null) editor.store.setState({ rowsDraft: null });
      });
    },
    cancel: () => {
      giveUp(context);
    },
  };
}

/** The clicks: on the « + », a lane added once no second click came; a double click, the empty rows on each side taken out. */
function clickHandlers({ editor, lanes, press }: BorderContext) {
  return {
    // A click of the keyboard (`detail` 0: Enter, Space, a screen reader) follows no pull: never swallowed (audit 3.1 V2, 5.4.4).
    click: (border: number, keyboard = false) => {
      const pulled = press.pulled;
      press.pulled = false;
      if (pulled && !keyboard) return;
      clearTimeout(press.adding);
      press.adding = setTimeout(() => {
        press.adding = undefined;
        void addLane(editor, border);
      }, DOUBLE_CLICK_MS);
    },
    doubleClick: (border: number) => {
      clearTimeout(press.adding);
      press.adding = undefined;
      const above = lanes[border - 1]?.lane.id;
      const below = lanes[border]?.lane.id;
      void (async () => {
        if (above !== undefined) await shrinkLane(editor, above, 'bottom');
        if (below !== undefined) await shrinkLane(editor, below, 'top');
      })();
    },
  };
}

/**
 * The gestures of the borders, with the capture of the pointer as the drag of the headers. A pull draws the map as it
 * would leave it (the draft of the editor), sends its rows on release and stays drawn until they are answered. A click on
 * the « + » adds a lane once no second click came; a double click takes out the empty last row of the lane above and the
 * empty first row of the lane below, either or both. While the pointer pulls, the keys are its own.
 */
export function useLaneBorder(editor: MapEditor, lanes: readonly LaneBand[], zoom: number) {
  const [held, setHeld] = useState<Held | null>(null);
  const [press] = useState<Press>(() => ({ pulled: false, adding: undefined }));
  useEffect(() => () => {
    clearTimeout(press.adding);
  }, [press]);
  useKeyLayer('modal', (event, chord) => {
    if (chord !== 'Escape' || event.type !== 'keydown') return false;
    giveUp({ editor, setHeld });
    return true;
  }, held !== null);
  const context: BorderContext = { editor, lanes, zoom, held, setHeld, press };
  return { pulling: held?.far === true, ...pullHandlers(context), ...clickHandlers(context) };
}
