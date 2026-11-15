// useLeadTo.ts
// Where an object named in a side panel leads (F-64): on the map shown, its steps chosen and put in view (a lane by its steps); in a detailed map, that map opened, which chooses them and puts them in view once shown.

import type { Schema } from '../../../api/client.ts';
import { paths } from '../../../app/router.ts';
import { focusOnArrival, stepsNamed, type ArrivalSubject } from '../state/arrival.ts';
import { useEditor } from '../state/EditorContext.tsx';

export function useLeadTo(map: Schema<'MapView'>): (subject: ArrivalSubject) => void {
  const editor = useEditor();
  return (subject) => {
    if (subject.mapId !== map.id) {
      focusOnArrival(subject);
      editor.navigate(paths.map(subject.mapId));
      return;
    }
    const steps = stepsNamed(map, subject);
    if (steps.length === 0) return;
    editor.store.setState({ selection: { steps, flows: [] } });
    editor.canvas.reveal(steps);
  };
}
