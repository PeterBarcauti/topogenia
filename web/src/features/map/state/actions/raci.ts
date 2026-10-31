// raci.ts
// The RACI of the steps: a letter given to a lane for a step, or taken back, one A a step moved by the host; the matrix copied for Excel and PowerPoint (annex B 2.11, A-2.13).

import { api } from '../../../../api/client.ts';
import { matrixHtml, matrixText, type MatrixHeaders, type RaciMatrix, type RaciRole, type TableLook } from '../../model/raci.ts';
import type { MapEditor } from '../editor.ts';

/**
 * Gives a lane a role for a step, or takes it back with null (B-2.11-11); nothing is sent when the lane already has that
 * role. Giving the A to another lane moves it: the host keeps one A a step and its undo gives it back (B-2.11-12, B-2.11-13).
 */
export async function setRaci(editor: MapEditor, stepId: string, laneId: string, role: RaciRole | null): Promise<void> {
  await editor.write((map) => {
    const step = map.steps.find((candidate) => candidate.id === stepId);
    const before = map.raci.find((candidate) => candidate.step_id === stepId && candidate.lane_id === laneId)?.role ?? null;
    if (step === undefined || before === role) return null;
    return api.put('/api/steps/{id}/raci', { path: { id: stepId }, body: { lane_id: laneId, role }, version: step.version });
  });
}

/** The look of the copied table, read from the tokens of the page when it is copied (spec 9.2: no colour is written in the code). */
function tableLook(): TableLook {
  const style = getComputedStyle(document.documentElement);
  return { border: style.getPropertyValue('--border'), header: style.getPropertyValue('--table-header-bg'), font: style.getPropertyValue('--font-sans') };
}

/**
 * Copies the matrix: an HTML table and the tabulated text together (B-2.11-15, B-2.11-16), else the text alone when the
 * rich copy is missing or refused (B-2.11-17). True once copied; a copy refused is silent, the table stays selectable.
 */
export async function copyMatrix(matrix: RaciMatrix, headers: MatrixHeaders): Promise<boolean> {
  const text = matrixText(matrix, headers);
  try {
    const html = new Blob([matrixHtml(matrix, headers, tableLook())], { type: 'text/html' });
    // A browser without ClipboardItem throws here, as one that refuses the rich copy: the text alone is copied.
    await navigator.clipboard.write([new ClipboardItem({ 'text/html': html, 'text/plain': new Blob([text], { type: 'text/plain' }) })]);
    return true;
  } catch {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      return false;
    }
  }
}
