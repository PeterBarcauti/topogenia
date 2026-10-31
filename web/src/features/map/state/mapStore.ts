// mapStore.ts
// The store of one editor of a map: the map and its history as the host gives them, the loading, the language it is shown in and the lock of its version of origin, a translation of the assistant previewed in place (ADR 083), and the state of the editor not kept (annex B 7).

import { createStore, type StoreApi } from 'zustand/vanilla';
import { api, type Schema } from '../../../api/client.ts';

type StepType = Schema<'StepType'>;
type MapView = Schema<'MapView'>;

/** Where the map comes from: the host by default, a fake in the tests. */
export type MapSource = (mapId: string, signal: AbortSignal) => Promise<Schema<'MapReply'>>;

export const hostSource: MapSource = (mapId, signal) => api.get('/api/maps/{id}', { path: { id: mapId }, signal });

/** Normal, or linking steps by successive clicks (B-2.1-22). */
export type EditorMode = 'normal' | 'link';

/** Select draws a rectangle on the background, Hand moves the view (B-2.1-10). */
export type Tool = 'select' | 'hand';

/** The steps and flows chosen, by their identifiers. */
export interface Selection {
  readonly steps: readonly string[];
  readonly flows: readonly string[];
}

export const NO_SELECTION: Selection = { steps: [], flows: [] };

/** What a preview of a proposal does to a step of the map. */
export type StepMark = 'changed' | 'removed';

/** The three exports of the map (annex B 2.14). */
export type ExportFormat = 'pdf' | 'png' | 'xlsx';

/** An export running: its format, and the line of the classification printed under the image of a PNG (S-4.1-08). */
export interface ExportRun {
  readonly format: ExportFormat;
  readonly caption: string | null;
}

/**
 * The language the map is shown in (ADR 083): its language, the language of origin of the project, and whether the
 * version of origin is locked (another language shown): then the structure cannot change, and a name typed is the
 * translation of the object into the language shown.
 */
export interface Shown {
  readonly language: string;
  readonly origin: string;
  readonly locked: boolean;
}

/**
 * A translation of the assistant previewed (ADR 083): its language and the texts it would write, by identifier of the
 * object (the project, the lanes, the steps, the flows). The map shows them in place, in that language and locked,
 * until the translation is applied or put aside; nothing is written.
 */
export interface PreviewedTranslation {
  readonly language: string;
  readonly texts: Readonly<Record<string, string>>;
}

/** The map with the texts of a translation previewed, its structure locked: nothing of it can change meanwhile. */
function withPreviewed(map: MapView, { language, texts }: PreviewedTranslation): MapView {
  const into = <T extends { readonly id: string; readonly translations: Readonly<Record<string, string>> }>(item: T): T => {
    const text = texts[item.id];
    return text === undefined ? item : { ...item, translations: { ...item.translations, [language]: text } };
  };
  return { ...map, rights: { ...map.rights, structure: false }, lanes: map.lanes.map(into), steps: map.steps.map(into), flows: map.flows.map(into) };
}

export interface MapState {
  /**
   * The map read, null before the first reading; in a language other than its language of origin, its structure is
   * locked (`rights.structure` false, whatever the right of the viewer: ADR 083).
   */
  readonly map: MapView | null;
  /** The language the map is shown in; null until the screen tells it. */
  readonly shown: Shown | null;
  /** The viewer may type the translations of the names into the language shown: a facilitator, the version of origin locked. */
  readonly translating: boolean;
  /** Shows the map in a language: the lock follows (ADR 083). */
  readonly show: (shown: Shown) => void;
  /** The translation of the assistant previewed on the map, null else; while it is, the map is shown in its language. */
  readonly previewed: PreviewedTranslation | null;
  /** Shows a translation previewed in place, or the map again as it is (null). */
  readonly preview: (previewed: PreviewedTranslation | null) => void;
  /** What the user can undo and redo on the map, from the host (spec 4.6). */
  readonly history: Schema<'HistoryView'> | null;
  readonly loading: boolean;
  /** The failure of the last reading, null after a reading that succeeded. */
  readonly error: unknown;
  readonly mode: EditorMode;
  /** The source chosen in the link mode. */
  readonly linkSource: string | null;
  /** The object whose name is being typed: a step by its identifier, a lane as `lane:<id>`. */
  readonly editing: string | null;
  /** The character typed on the step chosen alone, that its name starts from instead of its label (R1, AR-151); null else. */
  readonly editingSeed: { readonly stepId: string; readonly text: string } | null;
  /** The type of the next step, `task` at each opening and again once a step is created (B-7-08, AR-152). */
  readonly newStepType: StepType;
  /** The steps and flows chosen; they survive the readings of the map (B-2.3-28). */
  readonly selection: Selection;
  /** Hand at each opening, never kept (B-2.1-11, B-7-07). */
  readonly tool: Tool;
  /** The Space bar held: the Hand for the time being (B-2.3-06). */
  readonly spaceHand: boolean;
  /** The step whose sheet is open, never kept (B-2.3-13, B-7-10); the panel comes with the sheet (P6-T07). */
  readonly openSheet: string | null;
  /** An undo or a redo asked and not answered yet: the buttons wait for it (B-2.6-06). */
  readonly replaying: boolean;
  /** The export running, null else; the canvas shows its state of export while an image is captured (B-2.14-02, B-7-20). */
  readonly exporting: ExportRun | null;
  /** The steps a preview of a proposal of the assistant changes or removes, marked on the map while it is open (spec 3.0 section 3.1-5). */
  readonly marks: Readonly<Record<string, StepMark>>;
  /** Reads the map; a reading still running is cancelled first (B-7-18). */
  readonly load: (mapId: string) => Promise<void>;
  /** Cancels the reading running, when the editor leaves. */
  readonly cancel: () => void;
  /**
   * Takes the reply of a write: its map and its history when the map is the one of the editor, else the map is read
   * again; nothing is changed before the host answers (B-2.6-11).
   */
  readonly apply: (reply: Schema<'StructureReply'>) => Promise<void>;
}

/** What still exists on a map new: the chosen objects, the step whose sheet is open, the source and the name typed. */
function survivors(state: MapState, map: MapView): Partial<MapState> {
  const steps = new Set(map.steps.map((step) => step.id));
  const flows = new Set(map.flows.map((flow) => flow.id));
  const lanes = new Set(map.lanes.map((lane) => `lane:${lane.id}`));
  const alive = (id: string | null) => (id !== null && steps.has(id) ? id : null);
  return {
    selection: { steps: state.selection.steps.filter((id) => steps.has(id)), flows: state.selection.flows.filter((id) => flows.has(id)) },
    openSheet: alive(state.openSheet),
    linkSource: alive(state.linkSource),
    editing: state.editing !== null && (steps.has(state.editing) || lanes.has(state.editing)) ? state.editing : null,
    editingSeed: state.editingSeed !== null && steps.has(state.editingSeed.stepId) ? state.editingSeed : null,
  };
}

/**
 * A store for one editor, made when the editor opens a map and dropped with it: nothing of it is kept in the browser,
 * neither the mode, the tool, the selection nor the type of the next steps (B-7-07, B-7-08, B-7-11, B-7-16).
 */
export function createMapStore(source: MapSource = hostSource): StoreApi<MapState> {
  let running: AbortController | null = null;
  let mapId: string | null = null;
  /** The map as the host gave it, before the lock of the language shown. */
  let given: MapView | null = null;
  /** The language the screen tells, kept while a translation previewed shows the map in its own. */
  let chosen: Shown | null = null;
  let previewed: PreviewedTranslation | null = null;
  /**
   * The map shown, its language and the right to translate, from the map given, the language chosen and the translation
   * previewed: the language of that translation, its texts in place, nothing to type meanwhile.
   */
  const present = (map: MapView): Pick<MapState, 'map' | 'shown' | 'translating'> => {
    given = map;
    if (previewed !== null)
      return { map: withPreviewed(map, previewed), shown: { language: previewed.language, origin: map.languages.origin, locked: true }, translating: false };
    const locked = chosen?.locked === true;
    return locked
      ? { map: map.rights.structure ? { ...map, rights: { ...map.rights, structure: false } } : map, shown: chosen, translating: map.rights.structure }
      : { map, shown: chosen, translating: false };
  };
  return createStore<MapState>()((setState, getState) => ({
    map: null, shown: null, translating: false, history: null, loading: false, error: null, mode: 'normal', linkSource: null, editing: null, editingSeed: null,
    newStepType: 'task', selection: NO_SELECTION, tool: 'hand', spaceHand: false, openSheet: null, replaying: false, exporting: null, marks: {}, previewed: null,
    show(shown) {
      if (chosen !== null && chosen.language === shown.language && chosen.origin === shown.origin && chosen.locked === shown.locked) return;
      chosen = shown;
      setState({ ...(given === null ? { shown: previewed === null ? shown : getState().shown } : present(given)), ...(shown.locked ? { mode: 'normal', linkSource: null } : {}) });
    },
    preview(next) {
      if (next === null && previewed === null) return;
      previewed = next;
      setState({ previewed: next, ...(given === null ? {} : present(given)), ...(next === null ? {} : { mode: 'normal', linkSource: null, editing: null, editingSeed: null }) });
    },
    async load(id) {
      running?.abort();
      const controller = new AbortController();
      running = controller;
      mapId = id;
      setState({ loading: true });
      try {
        const reply = await source(id, controller.signal);
        if (controller.signal.aborted) return;
        setState({ ...present(reply.map), history: reply.history, loading: false, error: null, ...survivors(getState(), reply.map) });
      } catch (failure) {
        if (!controller.signal.aborted) setState({ loading: false, error: failure });
      } finally {
        if (running === controller) running = null;
      }
    },
    cancel() {
      running?.abort();
      running = null;
    },
    async apply(reply) {
      if (reply.map !== null && reply.map.id === getState().map?.id) {
        setState({ ...present(reply.map), history: reply.history ?? getState().history, ...survivors(getState(), reply.map) });
      } else if (mapId !== null) await getState().load(mapId);
    },
  }));
}
