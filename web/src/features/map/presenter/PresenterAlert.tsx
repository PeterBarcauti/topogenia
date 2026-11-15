// PresenterAlert.tsx
// What a presentation says of a refusal or a failure (a comment refused, comments or a detailed map that cannot be read), in its stage: the full screen shows the stage alone, and the notifications of the application are under it. The message is the one of the host; it leaves at the next gesture of the presenter, or after the time of an error.

import { useEffect } from 'react';
import { useStore } from 'zustand';
import { TOAST_DELAYS } from '../../../shared/ui/toasts.ts';
import type { MapEditor } from '../state/editor.ts';

export function PresenterAlert({ editor }: { editor: MapEditor }) {
  const alert = useStore(editor.presenter, (state) => state.alert);
  useEffect(() => {
    if (alert === null) return undefined;
    const timer = setTimeout(() => {
      // Only the message this delay was started for: one said since has its own.
      if (editor.presenter.getState().alert === alert) editor.presenter.getState().say(null);
    }, TOAST_DELAYS.error);
    return () => {
      clearTimeout(timer);
    };
  }, [editor, alert]);
  return alert === null ? null : <p className="presenter-alert" role="alert" data-testid="presenter-alert">{alert.text}</p>;
}
