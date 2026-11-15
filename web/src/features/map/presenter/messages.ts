// messages.ts
// The messages of a presentation cut and given back (lot J10): those of the steps, those of the ways between them, each alone, at any time; the scene shown starts again when its own text is the one cut or given back.

import type { MapEditor } from '../state/editor.ts';
import type { Scene } from './model/script.ts';

/** The messages a presenter may cut: those of the steps, or those of the ways between them (the scenes of a passage). */
export type MessageKind = 'step' | 'passage';

const PREFERENCE = { step: 'presenterStepMessages', passage: 'presenterFlowMessages' } as const;

/**
 * Cuts the messages of a kind, or gives them back (E, R and their buttons). The scene shown starts again when it is of
 * that kind: its text has just changed, and so has its time. Cut, a long text leaves a scene of a moment, which the
 * time already played would end at once, carried on into the scenes after it; given back, its text is said from its
 * first sentence (independent review of the lot J10). Playing or paused as before; a scene of the other kind, the
 * opening and the closing are left as they are.
 */
export function switchMessages(editor: MapEditor, kind: MessageKind, scenes: readonly Scene[]): void {
  const preferences = editor.preferences.getState();
  preferences.set(PREFERENCE[kind], !preferences[PREFERENCE[kind]]);
  const presenter = editor.presenter.getState();
  if (scenes[presenter.player.scene]?.kind === kind) presenter.restart();
}
