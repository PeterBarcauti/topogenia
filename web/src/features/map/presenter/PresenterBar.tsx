// PresenterBar.tsx
// The bar of a presentation (ADR 097), in the language of the interface: back to the start, the step before, play or pause, the step after; the stops as chapters in the ink of their lane, the one shown marked; the speed, the comment of the step (ADR 099), the subtitles, the full screen, the way out. It hides while it plays and the mouse rests. While a comment is written, its moves keep the text and the field follows the step reached; the pencil and the way out keep the text first (essai J6 of 3.2).

import { useEffect, useRef, type CSSProperties } from 'react';
import { Captions, CaptionsOff, ChevronLeft, ChevronRight, CornerLeftUp, Layers, Maximize, MessageSquare, MessageSquareOff, Pause, Pencil, Play, Route, RouteOff, SkipBack, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { useShortcutLabel } from '../../../shared/ui/useShortcutLabel.ts';
import type { MapEditor } from '../state/editor.ts';
import { closeComment, commentableAt, isFlow, leavePresentation, moveWritten, openComment } from './comments.ts';
import { switchMessages } from './messages.ts';
import { detailedAt, diveInto } from './dive.ts';
import { SPEEDS, type Move } from './model/player.ts';
import { PresenterVoiceButtons } from './PresenterVoice.tsx';
import type { Presentation } from './usePresentation.ts';
import type { PresenterVoice } from './usePresenterVoice.ts';

interface BarButton {
  readonly id: string;
  readonly label: string;
  /** The key that does the same, written for the person; none for a move without one. */
  readonly keys: string | null;
  readonly icon: typeof Play;
  readonly run: () => void;
  readonly pressed?: boolean;
}

/** The stop of the scene shown: none at the opening, the last at the closing. */
const stopOf = (presentation: Presentation, scene: number): number => {
  const current = presentation.scenes[scene];
  if (current === undefined || current.kind === 'opening') return -1;
  return current.kind === 'closing' ? presentation.tour.stops.length : current.stop;
};

/** The steps told up to a stop, each counted once: a decision shown again for its next exit counts for none (lot J10). */
const toldUpTo = (presentation: Presentation, stop: number): number => presentation.tour.stops.slice(0, stop + 1).filter((candidate) => candidate.again !== true).length;

/** The stop that marks the step of a stop: itself, or, for a decision shown again, its first stop, the one that has a mark and a rank (independent review, M4). */
const markOf = (presentation: Presentation, stop: number): number => {
  const shown = presentation.tour.stops[stop];
  return shown?.again === true ? presentation.tour.stops.findIndex((candidate) => candidate.stepId === shown.stepId) : stop;
};

/** Chapters up to this count are buttons of 24 px at least (WCAG 2.5.8); more, the marks show the way without a click. */
const CLICKABLE_CHAPTERS = 18;

/** The stops as chapters: one mark a step told in the ink of its lane (none for a decision shown again, lot J10), the ones run filled, the one shown raised; a click goes to it. */
function Chapters({ editor, presentation, stop }: { editor: MapEditor; presentation: Presentation; stop: number }) {
  const { t } = useTranslation();
  const tintOf = new Map(presentation.layout.steps.map((figure) => [figure.step.id, figure.tint]));
  const labelOf = new Map(presentation.layout.steps.map((figure) => [figure.step.id, figure.label]));
  const mark = markOf(presentation, stop);
  if (toldUpTo(presentation, presentation.tour.stops.length - 1) > CLICKABLE_CHAPTERS) {
    return (
      <div className="presenter-chapters is-track" aria-hidden="true">
        {presentation.tour.stops.map((candidate, index) => {
          if (candidate.again === true) return null;
          const style = { '--ink': `var(--lane-${String(tintOf.get(candidate.stepId) ?? 1)}-ink)` } as CSSProperties;
          return <span key={`${String(index)}-${candidate.stepId}`} className={`presenter-chapter ${index === mark ? 'is-shown' : index < stop ? 'is-run' : ''}`} style={style} />;
        })}
      </div>
    );
  }
  return (
    <div className="presenter-chapters" role="group" aria-label={t('presenter.bar.chapters')}>
      {presentation.tour.stops.map((candidate, index) => {
        if (candidate.again === true) return null;
        const style = { '--ink': `var(--lane-${String(tintOf.get(candidate.stepId) ?? 1)}-ink)` } as CSSProperties;
        const state = index === mark ? 'is-shown' : index < stop ? 'is-run' : '';
        return (
          <button key={`${String(index)}-${candidate.stepId}`} type="button" className={`presenter-chapter ${state}`} style={style} title={labelOf.get(candidate.stepId) ?? ''}
            aria-label={labelOf.get(candidate.stepId) ?? ''} aria-current={index === mark ? 'step' : undefined} data-testid={`presenter-chapter-${String(index)}`}
            onClick={() => {
              moveWritten(editor, presentation, () => {
                editor.presenter.getState().move({ stop: index }, presentation.scenes);
              });
            }} />
        );
      })}
    </div>
  );
}

/**
 * The two buttons of the messages (lot J10, asked by the project owner): what is said of the steps, and what is said of
 * the ways between them, each turned off alone, at any time and at any level; off, neither shown nor read: the
 * presenter then speaks in its place. The browser keeps them.
 */
function useMessageButtons(editor: MapEditor, scenes: Presentation['scenes']): BarButton[] {
  const { t } = useTranslation();
  const stepMessages = useStore(editor.preferences, (state) => state.presenterStepMessages);
  const flowMessages = useStore(editor.preferences, (state) => state.presenterFlowMessages);
  return [
    { id: 'step-messages', label: t('presenter.bar.step_messages'), keys: 'E', icon: stepMessages ? MessageSquare : MessageSquareOff, pressed: stepMessages,
      run: () => { switchMessages(editor, 'step', scenes); } },
    { id: 'flow-messages', label: t('presenter.bar.flow_messages'), keys: 'R', icon: flowMessages ? Route : RouteOff, pressed: flowMessages,
      run: () => { switchMessages(editor, 'passage', scenes); } },
  ];
}

export function PresenterBar({ editor, presentation, fullscreen, voice }: { editor: MapEditor; presentation: Presentation; fullscreen: () => void; voice: PresenterVoice }) {
  const { t, i18n } = useTranslation();
  const shortcut = useShortcutLabel();
  const playing = useStore(editor.presenter, (state) => state.player.playing);
  const speed = useStore(editor.presenter, (state) => state.player.speed);
  const scene = useStore(editor.presenter, (state) => state.player.scene);
  const depth = useStore(editor.presenter, (state) => state.levels.length);
  const detailed = detailedAt(presentation, scene);
  const commentable = commentableAt(presentation, scene, useStore(editor.presenter, (state) => state.pointed));
  const writing = useStore(editor.presenter, (state) => state.writing !== null);
  // The field of a comment closed gives the focus back to the pencil that opens it, in the stage (R2-10).
  const pencil = useRef<HTMLButtonElement>(null);
  const wrote = useRef(false);
  useEffect(() => {
    if (wrote.current && !writing) pencil.current?.focus();
    wrote.current = writing;
  }, [writing]);
  const subtitles = useStore(editor.preferences, (state) => state.presenterSubtitles);
  const messages = useMessageButtons(editor, presentation.scenes);
  const presenter = () => editor.presenter.getState();
  /** A move by a button of the bar: while a comment is written, its text is kept and its field follows the step reached. */
  const go = (move: Move) => () => {
    moveWritten(editor, presentation, () => {
      presenter().move(move, presentation.scenes);
    });
  };
  const stop = stopOf(presentation, scene);
  const total = presentation.tour.stops.length;
  const left: BarButton[] = [
    { id: 'restart', label: t('presenter.bar.restart'), keys: null, icon: SkipBack, run: go('start') },
    { id: 'previous', label: t('presenter.bar.previous'), keys: null, icon: ChevronLeft, run: go('previousStep') },
    { id: 'play', label: t(playing ? 'presenter.bar.pause' : 'presenter.bar.play'), keys: shortcut('Space'), icon: playing ? Pause : Play, run: () => { presenter().toggle(presentation.durations); } },
    { id: 'next', label: t('presenter.bar.next'), keys: null, icon: ChevronRight, run: go('nextStep') },
  ];
  // The levels of a detail (F-120): back to the map above while in a detail, into the detail of the detailed step shown.
  const levels: BarButton[] = [
    ...(depth > 0 ? [{ id: 'surface', label: t('presenter.bar.surface'), keys: 'U', icon: CornerLeftUp, run: () => { presenter().surface(); } }] : []),
    ...(detailed === null ? [] : [{ id: 'detail', label: t('presenter.bar.detail'), keys: 'D', icon: Layers, run: () => { diveInto(editor, presentation, detailed); } }]),
  ];
  const right: BarButton[] = [
    // The comment of the step shown (ADR 099), for those who may change the map.
    ...(commentable === null ? [] : [{ id: 'comment', label: t(isFlow(presentation, commentable) ? 'presenter.bar.comment_flow' : 'presenter.bar.comment'), keys: 'T', icon: Pencil, pressed: writing,
      run: () => { if (writing) closeComment(editor, presentation, true); else openComment(editor, presentation); } }]),
    ...messages,
    { id: 'subtitles', label: t('presenter.bar.subtitles'), keys: 'C', icon: subtitles ? Captions : CaptionsOff, pressed: subtitles,
      run: () => { editor.preferences.getState().set('presenterSubtitles', !subtitles); } },
    { id: 'fullscreen', label: t('presenter.bar.fullscreen'), keys: 'F', icon: Maximize, run: fullscreen },
    // The way out keeps the text of a comment written first; refused by the host, the presentation stays and says why.
    { id: 'exit', label: t('presenter.bar.exit'), keys: shortcut('Escape'), icon: X, run: () => { leavePresentation(editor, presentation); } },
  ];
  const draw = (button: BarButton) => (
    <button key={button.id} ref={button.id === 'comment' ? pencil : undefined} type="button" className={`presenter-button${button.id === 'play' ? ' presenter-button--play' : ''}`}
      title={button.keys === null ? button.label : `${button.label} (${button.keys})`} aria-label={button.label} aria-pressed={button.pressed} data-testid={`presenter-${button.id}`}
      onClick={button.run}>
      <Icon icon={button.icon} size={button.id === 'play' ? 22 : 18} />
    </button>
  );
  const speedText = new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 2 }).format(speed);
  return (
    <div className="presenter-bar" role="toolbar" aria-label={t('presenter.bar.name')} data-testid="presenter-bar">
      <div className="presenter-bar-group">{left.map(draw)}</div>
      {levels.length > 0 && <div className="presenter-bar-group">{levels.map(draw)}</div>}
      <Chapters editor={editor} presentation={presentation} stop={stop} />
      <span className="presenter-progress" data-testid="presenter-progress">
        {stop < 0 ? t('presenter.bar.overview') : stop >= total ? t('presenter.bar.summary') : t('presenter.bar.progress', { current: toldUpTo(presentation, markOf(presentation, stop)), total: toldUpTo(presentation, total - 1) })}
      </span>
      <div className="presenter-bar-group">
        <button type="button" className="presenter-button presenter-speed" title={`${t('presenter.bar.speed')} (+ −)`} aria-label={t('presenter.bar.speed')} data-testid="presenter-speed"
          onClick={() => {
            if (speed >= (SPEEDS[SPEEDS.length - 1] ?? 2)) for (let index = 0; index < SPEEDS.length; index++) presenter().slower();
            else presenter().faster();
          }}>
          {`×${speedText}`}
        </button>
        <PresenterVoiceButtons editor={editor} voice={voice} language={presentation.language} />
        {right.map(draw)}
      </div>
    </div>
  );
}
