// stepActivation.ts
// What choosing a step does, by Enter, or by a click in the link mode and on the RACI layer: the source then the target of the link mode, the RACI panel on its layer, else the sheet (B-2.3-09 to B-2.3-12, NF08, AR-151).

import { i18next } from '../../../../shared/i18n/i18n.ts';
import type { Point } from '../../model/geometry/grid.ts';
import { hasContent } from '../../model/stepFacts.ts';
import type { MapEditor } from '../editor.ts';
import { addFlow } from './flows.ts';

/** The RACI panel opens at (+12, −20) of the point aimed: the pointer, or the step reached by the keyboard (B-2.3-12). */
export const raciPanelPoint = (at: Point): Point => ({ x: at.x + 12, y: at.y - 20 });

/**
 * In the link mode, the step is the source, then the target of a new flow; else, with the RACI layer on and the right to
 * change the map, a step with content opens its RACI panel; else a step with content opens its sheet, another closes it.
 */
export function activateStep(editor: MapEditor, stepId: string, at: Point): void {
  const { map, mode, linkSource } = editor.store.getState();
  const step = map?.steps.find((candidate) => candidate.id === stepId);
  if (map === null || step === undefined) return;
  if (mode === 'link') {
    if (linkSource === null) {
      editor.store.setState({ linkSource: step.id });
      editor.notify(i18next.t('map.link_source', { code: step.code }));
    } else if (linkSource !== step.id) {
      editor.store.setState({ mode: 'normal', linkSource: null });
      void addFlow(editor, linkSource, step.id);
    }
  } else if (map.rights.structure && editor.preferences.getState().showRaci) {
    if (hasContent(step.type)) editor.floating.getState().open({ kind: 'raci', stepId: step.id, at: raciPanelPoint(at) });
  } else editor.store.setState({ openSheet: hasContent(step.type) ? step.id : null });
}
