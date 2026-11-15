// PresenterStage.tsx
// The stage of a presentation (ADR 097): a layer over the screen of the map, put in full screen by the gesture that opened it, with its own map read only, its title, its caption, its counters and its bar, its voice (the online ones as the classification of the project allows, F-137); the editor under it left as it was, its view kept.

import { useRef } from 'react';
import { ReactFlowProvider } from '@xyflow/react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import { useEditor } from '../state/EditorContext.tsx';
import { PresenterAlert } from './PresenterAlert.tsx';
import { PresenterBar } from './PresenterBar.tsx';
import { PresenterCaption, PresenterCounters } from './PresenterCaption.tsx';
import { PresenterComment } from './PresenterComment.tsx';
import { usePresenterComments } from './comments.ts';
import { useOnlineVoicesAllowed } from './onlineVoices.ts';
import { PresenterFlow } from './PresenterFlow.tsx';
import { PresenterTitle } from './PresenterTitle.tsx';
import { useReducedMotion } from './usePresenterCamera.ts';
import { usePresentation } from './usePresentation.ts';
import { usePresenterVoice } from './usePresenterVoice.ts';
import { useStageFocus } from './usePresenterFocus.ts';
import { stageFieldKey, useFullscreen, useIdle, usePresenterClock, usePresenterKeys, voicePanelOpen } from './usePresenterStage.ts';

/** The stage of the map presented: the map of the editor, `rootId`, or a detail of it, whose project is the same (F-120). */
function PresenterStage({ map, rootId }: { map: Schema<'MapView'>; rootId: string }) {
  const { t } = useTranslation();
  const editor = useEditor();
  usePresenterComments(editor, map.id);
  const presentation = usePresentation(map);
  const root = useRef<HTMLDivElement>(null);
  const screen = useStore(editor.presenter, (state) => state.screen);
  const still = useReducedMotion();
  const fullscreen = useFullscreen(editor, root);
  usePresenterClock(editor, presentation);
  // Read once for the presentation, on the map of the editor: a detail dived into is of the same project, and its voice does not change on the way.
  const allowed = useOnlineVoicesAllowed(editor, rootId);
  const voice = usePresenterVoice(editor, presentation, allowed);
  const focus = useStageFocus(root);
  usePresenterKeys({ editor, presentation, fullscreen, voice, focus });
  const { idle, moved } = useIdle(editor);
  // The question and the settings of the voice stand in the bar: it stays shown while one of them is open (R6-01).
  const asked = useStore(editor.presenter, voicePanelOpen);
  return (
    <div ref={root} className={['presenter', idle && !asked && 'is-idle', still && 'is-still', focus.keyed && 'is-keyed'].filter(Boolean).join(' ')} role="dialog" aria-modal="true"
      aria-label={t('presenter.stage')} tabIndex={-1} data-testid="presenter" onMouseMove={moved} onPointerDownCapture={focus.pointed}
      onKeyDown={(event) => {
        stageFieldKey(editor, focus, event);
      }}>
      <ReactFlowProvider>
        <PresenterFlow editor={editor} presentation={presentation} still={still} />
      </ReactFlowProvider>
      <PresenterTitle presentation={presentation} />
      <PresenterCounters editor={editor} presentation={presentation} />
      <PresenterCaption editor={editor} presentation={presentation} silent={voice.silent} />
      <PresenterComment editor={editor} presentation={presentation} keyed={focus.keyed} />
      <PresenterBar editor={editor} presentation={presentation} fullscreen={fullscreen} voice={voice} />
      {screen !== 'map' && <div className={screen === 'black' ? 'presenter-blank presenter-blank--black' : 'presenter-blank presenter-blank--white'} data-testid="presenter-blank" />}
      <PresenterAlert editor={editor} />
    </div>
  );
}

/** The stage while a presentation is on, nothing else: the map of the editor, or the detail it dived into (F-120). */
export function PresenterHost({ map }: { map: Schema<'MapView'> }) {
  const editor = useEditor();
  const active = useStore(editor.presenter, (state) => state.active);
  const detail = useStore(editor.presenter, (state) => state.map);
  return active ? <PresenterStage map={detail ?? map} rootId={map.id} /> : null;
}
