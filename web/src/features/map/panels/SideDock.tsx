// SideDock.tsx
// The frame of the panel docked on the right (spec 3.0 section 15.2, ADR 086): its width chosen by the handle of its left edge, by the mouse or the arrow keys, the first width again by a double click; folded to a strip on the right edge that opens it again, the panel kept as it is meanwhile; the notifications on its left.

import { useEffect, useLayoutEffect, useMemo, useRef, useSyncExternalStore, type PointerEvent, type ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import { DockContext } from '../../../shared/ui/dock.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { DOCK, shownWidth, type DockPanel } from '../state/dock.ts';
import { useEditor } from '../state/EditorContext.tsx';

const subscribeToResize = (changed: () => void) => {
  window.addEventListener('resize', changed);
  return () => {
    window.removeEventListener('resize', changed);
  };
};

/** The width of the window, read again when it is resized. */
const useWindowWidth = (): number => useSyncExternalStore(subscribeToResize, () => window.innerWidth);

/** The arrow keys widen or narrow by a step (the handle is on the left edge: left widens), Home and End go to the bounds. */
function keyedWidth(chord: string, width: number, max: number): number | null {
  if (chord === 'ArrowLeft') return width + DOCK.step;
  if (chord === 'ArrowRight') return width - DOCK.step;
  if (chord === 'Home') return DOCK.min;
  return chord === 'End' ? max : null;
}

/** The handle of the left edge: a separator the mouse drags and the keyboard moves (WAI-ARIA « window splitter »). */
function Handle({ width, max }: { width: number; max: number }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const handle = useRef<HTMLDivElement>(null);
  const dragging = useRef(false);
  const set = (next: number, keep: boolean) => {
    editor.dock.getState().setWidth(Math.max(DOCK.min, Math.min(max, next)), keep);
  };
  useKeyLayer('panel', (event, chord) => {
    if (event.type !== 'keydown' || document.activeElement !== handle.current) return false;
    const next = keyedWidth(chord, width, max);
    if (next === null) return false;
    set(next, true);
    return true;
  });
  // The frame ends at the right edge of the window: the width is what lies between the pointer and that edge.
  const dragged = (event: PointerEvent<HTMLDivElement>) => (event.currentTarget.parentElement?.getBoundingClientRect().right ?? window.innerWidth) - event.clientX;
  return (
    <div ref={handle} role="separator" aria-orientation="vertical" aria-label={t('dock.resize')} title={t('dock.resize')} aria-valuemin={DOCK.min} aria-valuemax={max}
      aria-valuenow={width} tabIndex={0} className="side-dock-handle" data-testid="side-dock-handle"
      onPointerDown={(event) => {
        if (event.button !== 0) return;
        event.preventDefault();
        event.currentTarget.setPointerCapture(event.pointerId);
        dragging.current = true;
      }}
      onPointerMove={(event) => {
        if (dragging.current) set(dragged(event), false);
      }}
      onPointerUp={(event) => {
        if (!dragging.current) return;
        dragging.current = false;
        set(dragged(event), true);
      }}
      onPointerCancel={() => {
        dragging.current = false;
        set(width, true);
      }}
      onDoubleClick={() => {
        set(DOCK.width, true);
      }} />
  );
}

/**
 * The room the frame takes on the right, which the notifications keep free (the token `--dock-room`); set and taken back
 * in the same layout effect, so that a panel that replaces another keeps its own.
 */
function useDockRoom(room: number): void {
  useLayoutEffect(() => {
    document.documentElement.style.setProperty('--dock-room', `${String(room)}px`);
    return () => {
      document.documentElement.style.removeProperty('--dock-room');
    };
  }, [room]);
}

export interface SideDockProps {
  readonly panel: DockPanel;
  /** The name of the panel, written along its strip. */
  readonly title: string;
  readonly icon: LucideIcon;
  /** A short count on the strip (the warnings of the value stream), or none. */
  readonly badge?: string | null;
  readonly children: ReactNode;
}

export function SideDock({ panel, title, icon, badge = null, children }: SideDockProps) {
  const { t } = useTranslation();
  const editor = useEditor();
  const chosen = useStore(editor.dock, (state) => state.width);
  const folded = useStore(editor.dock, (state) => state.folded[panel] === true);
  const windowWidth = useWindowWidth();
  const width = shownWidth(chosen, windowWidth);
  const max = Math.max(DOCK.min, Math.round(windowWidth * DOCK.maxShare));
  const frame = useRef<HTMLDivElement>(null);
  const control = useMemo(() => ({ folded, fold: () => { editor.dock.getState().fold(panel, true); } }), [editor, panel, folded]);
  useDockRoom(folded ? DOCK.strip : width);
  // The focus follows the button pressed: on the strip once folded, on the button that folds once opened again.
  const first = useRef(true);
  useEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }
    frame.current?.querySelector<HTMLElement>(folded ? '[data-testid="side-dock-strip"]' : '[data-testid="panel-fold"]')?.focus();
  }, [folded]);
  return (
    <DockContext.Provider value={control}>
      <div ref={frame} className={`side-dock${folded ? ' is-folded' : ''}`} style={{ width: folded ? DOCK.strip : width }} data-testid="side-dock" data-panel={panel}>
        {folded ? (
          <button type="button" className="side-dock-strip" aria-expanded={false} aria-label={t('dock.unfold', { name: title })} title={t('dock.unfold', { name: title })}
            data-testid="side-dock-strip" onClick={() => { editor.dock.getState().fold(panel, false); }}>
            <Icon icon={icon} size={16} />
            <span className="side-dock-title">{title}</span>
            {badge !== null && <span className="side-dock-badge" data-testid="side-dock-badge">{badge}</span>}
          </button>
        ) : <Handle width={width} max={max} />}
        <div className="side-dock-panel" hidden={folded}>{children}</div>
      </div>
    </DockContext.Provider>
  );
}
