// insertPmapForm.ts
// The state of the insertion of a .pmap file as a detailed step, or of the replacement of a detail by one (3.3, F-208): the text read by the host a little after its last change, the label and the place of the step, the sheets on option, the sending in one operation.

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../../api/client.ts';
import { isApiError } from '../../../api/errors.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { isCollection } from '../../project/files/collection.ts';
import { conversionMessage, pmapOfFile, ZIP_SIGNATURE } from '../../project/files/fileConversion.ts';
import { faultsOf, type FileFault } from '../../project/files/fileFaults.ts';
import type { GridCell } from '../model/geometry/grid.ts';
import { laneRanks, lanesInOrder, readingOrder } from '../model/readingOrder.ts';
import type { MapEditor } from '../state/editor.ts';

type MapView = Schema<'MapView'>;
export type InsertionPreview = Schema<'PmapInsertionPreview'>;

/** A refusal shown in the dialog: its message and the faults of the file it lists. */
export interface InsertionError {
  readonly message: string;
  readonly faults: FileFault[];
}

/** The wait after the last change of the text before it is read by the host. */
export const PREVIEW_DELAY = 400;

/** The largest file read for an insertion, in bytes: the request the host reads for the opening of a file (`PmapApi.OpeningLimit`). */
export const INSERTED_BYTES_MAX = 10 * 1024 * 1024;

/** The value of « Placer après » that puts the step at the start of the map. */
export const AT_START = '';

/** The value of « Placer après » that puts the step on a flow: this prefix, then the identifier of the flow. */
const ON_FLOW = 'flow:';

/** The value of « Placer après » for a flow. */
export const flowChoice = (flowId: string) => `${ON_FLOW}${flowId}`;

/** Where the new step goes: a lane, and the step it follows (none: the start of the map), or the flow it goes on (that flow cut in two). */
export interface InsertionPlace {
  readonly laneId: string;
  readonly after: string;
  readonly flow: string | null;
}

/** On a flow of the map: the lane of its origin, as « Insérer une étape après » that origin puts it; nothing for a flow not on the map. */
export function onFlow(map: MapView, flowId: string): InsertionPlace | undefined {
  const flow = map.flows.find((candidate) => candidate.id === flowId);
  const origin = flow === undefined ? undefined : map.steps.find((candidate) => candidate.id === flow.source_step_id);
  return flow === undefined || origin === undefined ? undefined : { laneId: origin.lane_id, after: origin.id, flow: flow.id };
}

/** A step a new step may follow in the flow: any but an end, out of which no flow may go (independent review of the 3.3). */
export const mayFollow = (step: { readonly type: string }): boolean => step.type !== 'end';

/**
 * Where the new step goes by default: on the flow chosen alone, as a shape dropped on a connector; after the step chosen, in
 * its lane, unless it is an end; otherwise before the last end, on the one flow that reaches it; otherwise after the last
 * step that is no end, in its lane; the first lane of an empty map. Never out of an end, never into a start.
 */
export function placeOf(map: MapView, chosen: { readonly steps: readonly string[]; readonly flows: readonly string[] }): InsertionPlace {
  const along = chosen.steps.length === 0 && chosen.flows.length === 1 ? onFlow(map, chosen.flows[0] ?? '') : undefined;
  if (along !== undefined) return along;
  const step = chosen.steps.length === 1 ? map.steps.find((candidate) => candidate.id === chosen.steps[0] && mayFollow(candidate)) : undefined;
  if (step !== undefined) return { laneId: step.lane_id, after: step.id, flow: null };
  const ordered = [...map.steps].sort(readingOrder(laneRanks(map.lanes)));
  const end = ordered.filter((candidate) => !mayFollow(candidate)).at(-1);
  const into = end === undefined ? [] : map.flows.filter((flow) => flow.target_step_id === end.id);
  const before = into.length === 1 ? onFlow(map, into[0]?.id ?? '') : undefined;
  if (before !== undefined) return before;
  const last = ordered.filter(mayFollow).at(-1);
  return last === undefined ? { laneId: lanesInOrder(map.lanes)[0] ?? '', after: AT_START, flow: null } : { laneId: last.lane_id, after: last.id, flow: null };
}

/** Where the host puts the new step: a cell; on a flow, cut in two; or the step it goes before or after in the flow, whose flow is cut in two. */
export type Placement =
  | { readonly lane_id: string; readonly column: number; readonly row: number }
  | { readonly lane_id: string; readonly column: null; readonly row: null; readonly flow_id: string }
  | { readonly lane_id: string; readonly column: null; readonly row: null; readonly anchor_step_id: string; readonly position: 'before' | 'after' };

/**
 * The place sent: the cell of the menu of the map, linked to nothing; on the flow chosen; after the step chosen, in the
 * flow; at the start of the map, after its start when it opens on one, before its first step in the order of reading
 * otherwise, in the flow; in the first column of an empty map.
 */
export function placementOf(map: MapView, place: InsertionPlace, cell: GridCell | undefined): Placement {
  if (cell !== undefined) return { lane_id: cell.laneId, column: cell.column, row: cell.row };
  if (place.flow !== null) return { lane_id: place.laneId, column: null, row: null, flow_id: place.flow };
  if (place.after !== AT_START) return { lane_id: place.laneId, column: null, row: null, anchor_step_id: place.after, position: 'after' };
  const first = [...map.steps].sort(readingOrder(laneRanks(map.lanes)))[0];
  if (first === undefined) return { lane_id: place.laneId, column: 0, row: 0 };
  return { lane_id: place.laneId, column: null, row: null, anchor_step_id: first.id, position: first.type === 'start' ? 'after' : 'before' };
}

/**
 * The place once « Placer après » changes: on a flow, the lane of its origin; after a step, the lane of that step, as
 * « Insérer une étape après » puts it; the first lane for the start of the map.
 */
export function followed(map: MapView, place: InsertionPlace, choice: string): InsertionPlace {
  if (choice.startsWith(ON_FLOW)) return onFlow(map, choice.slice(ON_FLOW.length)) ?? place;
  const step = map.steps.find((candidate) => candidate.id === choice);
  const first = [...map.steps].sort(readingOrder(laneRanks(map.lanes)))[0];
  return { after: choice, laneId: step?.lane_id ?? first?.lane_id ?? place.laneId, flow: null };
}

interface PreviewRead {
  readonly key: string;
  readonly preview: InsertionPreview | null;
  readonly error: InsertionError | null;
}

/**
 * The text read by the host a little after its last change: what it holds, or why it is refused. A reply is kept with the
 * text it answers, so that a reply to an older text is never shown.
 */
export function usePreview(mapId: string, content: string, stepId: string | undefined, round: number) {
  const key = `${round}\u0000${stepId ?? ''}\u0000${content}`;
  const [read, setRead] = useState<PreviewRead | null>(null);
  useEffect(() => {
    if (content.trim() === '') return undefined;
    let current = true;
    const timer = window.setTimeout(() => {
      api.post('/api/maps/{id}/insert-pmap/preview', { path: { id: mapId }, body: { content, step_id: stepId ?? null } })
        .then((preview) => {
          if (current) setRead({ key, preview, error: null });
        })
        .catch((failure: unknown) => {
          if (current) setRead({ key, preview: null, error: { message: messageOf(failure), faults: faultsOf(failure) } });
        });
    }, PREVIEW_DELAY);
    return () => {
      current = false;
      window.clearTimeout(timer);
    };
  }, [mapId, content, stepId, round, key]);
  const answered = read !== null && read.key === key && content.trim() !== '';
  return { preview: answered ? read.preview : null, error: answered ? read.error : null };
}

/** Where the dialog is opened from: the step whose detail is replaced, the cell of the menu of the map, the flow of its menu. */
export interface InsertionOrigin {
  readonly stepId?: string;
  readonly cell?: GridCell;
  readonly flowId?: string;
}

/** The new step put in view once the canvas has drawn it, a frame after the map is shown again (nothing while no canvas is shown). */
function showInserted(editor: MapEditor, stepId: string) {
  window.requestAnimationFrame(() => {
    editor.canvas.reveal([stepId]);
    window.requestAnimationFrame(() => {
      editor.canvas.reveal([stepId]);
    });
  });
}

/**
 * The text of a file chosen, or why it is refused. Beyond what the host reads, it is said too large before it is read whole
 * in the page; a package of exchange is imported as a project, never inserted (audit of the copy 64, P2-06; the opening
 * says it too, AR-154).
 */
async function readInserted(file: File, t: Translate, raciLetters: string): Promise<{ text: string } | { refusal: string }> {
  if (file.size > INSERTED_BYTES_MAX) return { refusal: t('files.insert_too_large', { max: INSERTED_BYTES_MAX / (1024 * 1024) }) };
  try {
    const converted = await pmapOfFile(file, t, raciLetters);
    const text = converted ?? await file.text();
    return converted === null && text.startsWith(ZIP_SIGNATURE) ? { refusal: t('files.insert_package') } : { text };
  } catch (caught) {
    return { refusal: conversionMessage(caught, t) ?? t('files.unreadable') };
  }
}

/** The whole state of the dialog, and what it does: read a file, send the copy. */
export function useInsertForm(editor: MapEditor, map: MapView, origin: InsertionOrigin, onClose: () => void) {
  const { t } = useTranslation();
  const { stepId, cell } = origin;
  const [content, setContent] = useState('');
  const [fileName, setFileName] = useState<string | null>(null);
  const [round, setRound] = useState(0);
  const read = usePreview(map.id, content, stepId, round);
  const [label, setLabel] = useState<string | null>(null);
  const [first] = useState(() => (origin.flowId === undefined ? undefined : onFlow(map, origin.flowId)) ?? placeOf(map, editor.store.getState().selection));
  const [place, setPlace] = useState(first);
  const [withSheets, setWithSheets] = useState(true);
  const [sending, setSending] = useState(false);
  const [failure, setFailure] = useState<{ content: string; error: InsertionError } | null>(null);
  const preview = read.preview;
  const chosenLabel = (label ?? preview?.label ?? '').trim();
  const tooDeep = preview !== null && preview.depth > preview.room;
  const ready = !sending && preview !== null && !tooDeep && (stepId !== undefined || (chosenLabel !== '' && place.laneId !== ''));

  const changeContent = (text: string, file: string | null) => {
    // A collection is opened as projects, never inserted: said before anything is sent, the text left as it was.
    if (isCollection(text)) {
      setFailure({ content, error: { message: t('files.insert_collection'), faults: [] } });
      return;
    }
    setContent(text);
    setFileName(file);
  };
  const readFile = async (file: File | undefined) => {
    if (file === undefined) return;
    const read = await readInserted(file, t, map.raci_letters);
    if ('text' in read) changeContent(read.text, file.name);
    else setFailure({ content, error: { message: read.refusal, faults: [] } });
  };

  const submit = async () => {
    if (preview === null) return;
    setSending(true);
    let refused: unknown = null;
    const body = { content, with_sheets: withSheets };
    const placement = placementOf(map, place, cell);
    const reply = await editor.write(async (current) => {
      try {
        return stepId === undefined
          ? await api.post('/api/maps/{id}/insert-pmap', { path: { id: current.id }, version: preview.structure_version, body: { ...body, label: chosenLabel, ...placement } })
          : await api.post('/api/steps/{id}/replace-pmap', { path: { id: stepId }, version: preview.structure_version, body });
      } catch (caught) {
        refused = caught;
        return null;
      }
    });
    setSending(false);
    if (reply !== null) {
      editor.notify(t('files.inserted', { steps: preview.steps, maps: preview.maps }), 'success');
      if (reply.id !== null) {
        editor.store.setState({ selection: { steps: [reply.id], flows: [] } });
        showInserted(editor, reply.id);
      }
      onClose();
      return;
    }
    if (refused === null) return;
    setFailure({ content, error: { message: messageOf(refused), faults: faultsOf(refused) } });
    // A map changed since the preview: it is read again, and the file with it.
    if (isApiError(refused) && refused.status === 409) {
      void editor.reload();
      setRound((value) => value + 1);
    }
  };

  // A refusal of the sending or of the reading of a file stays until the text changes; the reading of the host comes after it.
  const error = failure !== null && failure.content === content ? failure.error : read.error;
  return { content, fileName, preview, error, label: label ?? preview?.label ?? '', place, offeredFlow: first.flow, withSheets, ready, tooDeep,
    changeContent, readFile, submit, setLabel, setPlace, setWithSheets };
}
