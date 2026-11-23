// projectWalk.ts
// The walk of the maps of the PDF of the project (ADR 113): from the map asked, each map read from the host, level by level, a level in the reading order of the steps it details (that of the view); stopped at the 101st map known and below twelve levels (an empty detailed map there leaves nothing out), the abandon heard before each reading; the scope of a branch offered on a detailed map only.

import { api, type Schema } from '../../../../api/client.ts';
import { PROJECT_PDF_LIMITS } from './projectPdf.ts';

type MapView = Schema<'MapView'>;

/** The maps walked in their order, and whether maps below the twelve levels were left out; or more maps than one document holds. */
export type ProjectWalk =
  | { readonly kind: 'walked'; readonly maps: readonly MapView[]; readonly depthCut: boolean }
  | { readonly kind: 'too_many' };

/** What the walk is asked with: its abandon, and the maps read so far said after each reading. */
export interface WalkAsk {
  readonly signal: AbortSignal;
  readonly progress?: (read: number) => void;
}

/** The map shown is a detailed map: the dialog offers its branch, « This map and its levels », besides the whole project. */
export const branchOffered = (view: MapView): boolean => view.breadcrumb.length > 1;

/** The maps a map details, in the reading order of its steps. */
const detailsOf = (view: MapView): string[] => view.steps.flatMap((step) => (step.sub_map_id === null ? [] : [step.sub_map_id]));

/**
 * Walks the maps from the one asked, which is the first: a level read whole before the next one, the maps of a level in
 * the order of the steps they detail. A map is known before it is read, from the step it details: once more than a
 * hundred are known the walk stops, nothing more is read; an empty detailed map counts, as it has its page. The maps of
 * the thirteenth level are not read, and said when one has steps (an empty one holds nothing that the document lacks). The
 * abandon is heard before each reading: once aborted, nothing more is asked of the host (a reading under way is cancelled
 * by its signal).
 */
export async function walkProject(start: string, ask: WalkAsk): Promise<ProjectWalk> {
  const maps: MapView[] = [];
  let depthCut = false;
  let level: readonly string[] = [start];
  for (let depth = 0; level.length > 0; depth++) {
    const next: string[] = [];
    for (const [rank, id] of level.entries()) {
      ask.signal.throwIfAborted();
      const { map: view } = await api.get('/api/maps/{id}', { path: { id }, signal: ask.signal });
      maps.push(view);
      const details = detailsOf(view);
      if (depth + 1 < PROJECT_PDF_LIMITS.levels) next.push(...details);
      else if (view.steps.some((step) => step.sub_map_id !== null && !step.sub_map_empty)) depthCut = true;
      if (maps.length + (level.length - rank - 1) + next.length > PROJECT_PDF_LIMITS.maps) return { kind: 'too_many' };
      ask.progress?.(maps.length);
    }
    level = next;
  }
  return { kind: 'walked', maps, depthCut };
}
