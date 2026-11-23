// useUnsavedForm.ts
// A form of the sheet or of a panel that writes in the project, followed while it is changed and not validated, so that « Quit » does not lose it silently (R11, ADR 115).

import { useEffect } from 'react';
import { trackUnsavedForm } from './pendingWrites.ts';

/** Follows the form while `changed` holds; validated, given up, closed or back as it opened, the form is followed no more. */
export function useUnsavedForm(changed: boolean): void {
  useEffect(() => (changed ? trackUnsavedForm() : undefined), [changed]);
}
