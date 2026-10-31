// RaciPanel.tsx
// The RACI panel of a step next to the click: the lane R first, then a button a letter for each other lane, pressed when given; Escape or a click outside closes it; the keyboard goes in and back to the step (annex B 2.11, NF08).

import { useEffect, useLayoutEffect, useRef, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { isInputField } from '../../../shared/keyboard/keyboard.ts';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import { fromKeyboard } from '../../../shared/ui/inputModality.ts';
import { fitInWindow } from '../../../shared/ui/menuPlacement.ts';
import { focusBackOnStep } from '../canvas/stepFocus.ts';
import type { Point } from '../model/geometry/grid.ts';
import { laneName, stepLabel } from '../model/mapLayout.ts';
import { concernedByRaci, raciLetters, rolesOf } from '../model/raci.ts';
import { lanesInOrder } from '../model/readingOrder.ts';
import { setRaci } from '../state/actions/raci.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';

type LaneView = Schema<'LaneView'>;

/** The space kept between the panel and the right edge of the window. */
const EDGE_GAP = 8;

export interface RaciPanelProps {
  readonly map: Schema<'MapView'>;
  readonly stepId: string;
  readonly at: Point;
  readonly onClose: () => void;
}

/**
 * The panel, kept in the window once drawn: moved left from the right edge, moved up from the bottom (B-2.3-12, 1.x
 * `PanneauRaci`). Not modal: Escape closes it and the other keys go on to the canvas; a press outside closes it. Opened
 * from the keyboard, its first letter takes the focus, or the panel itself when it has none (a step of the only lane: its R
 * alone); closed while it holds the focus, the step takes it back (NF08).
 */
function usePanel(stepId: string, at: Point, onClose: () => void): RefObject<HTMLDivElement | null> {
  const panel = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const box = panel.current;
    if (box === null) return;
    const drawn = box.getBoundingClientRect();
    if (drawn.right > window.innerWidth - EDGE_GAP) box.style.left = `${String(Math.max(0, window.innerWidth - drawn.width - EDGE_GAP))}px`;
    const top = fitInWindow(drawn, window.innerHeight);
    if (top !== null) box.style.top = `${String(top)}px`;
  }, [at]);
  useEffect(() => {
    if (fromKeyboard() && !isInputField(document.activeElement)) (panel.current?.querySelector<HTMLElement>('button') ?? panel.current)?.focus();
  }, [stepId]);
  useLayoutEffect(() => () => {
    focusBackOnStep(stepId, '[data-testid="raci-panel"]');
  }, [stepId]);
  useKeyLayer('panel', (event, chord) => {
    // The release of the key that closed a dialog above is no second Escape.
    if (event.type !== 'keydown' || chord !== 'Escape') return false;
    onClose();
    return true;
  });
  useEffect(() => {
    const outside = (event: PointerEvent) => {
      if (!(event.target instanceof Node) || panel.current?.contains(event.target) !== true) onClose();
    };
    document.addEventListener('pointerdown', outside, true);
    return () => {
      document.removeEventListener('pointerdown', outside, true);
    };
  }, [onClose]);
  return panel;
}

export function RaciPanel({ map, stepId, at, onClose }: RaciPanelProps) {
  const { t } = useTranslation();
  const language = useMapLanguage();
  const editor = useEditor();
  const panel = usePanel(stepId, at, onClose);
  const step = map.steps.find((candidate) => candidate.id === stepId);
  if (step === undefined || !concernedByRaci(step)) return null;
  const roles = rolesOf(map, step);
  const letters = raciLetters(map);
  const byId = new Map(map.lanes.map((lane) => [lane.id, lane]));
  const lanes = lanesInOrder(map.lanes).map((id) => byId.get(id) as LaneView);
  const own = byId.get(step.lane_id);
  const label = stepLabel(step, language);
  return (
    <div ref={panel} className="raci-panel" role="dialog" aria-label={t('raci.title', { label })} data-testid="raci-panel" tabIndex={-1} style={{ left: at.x, top: at.y }}>
      <p className="raci-panel-title">{`${step.code} · ${label}`}</p>
      <table>
        <tbody>
          {own !== undefined && (
            <tr className="raci-panel-own">
              <th scope="row">{laneName(own, language)}</th>
              <td><span className="raci-letter raci-letter--R" title={t('raci.names.R')}>{t('raci.performer')}</span> <span className="raci-panel-performs">{t('raci.performs')}</span></td>
            </tr>
          )}
          {lanes.filter((lane) => lane.id !== step.lane_id).map((lane) => {
            const name = laneName(lane, language);
            return (
              <tr key={lane.id}>
                <th scope="row">{name}</th>
                <td>
                  {letters.map((letter) => {
                    const active = roles.get(lane.id) === letter;
                    return (
                      <button key={letter} type="button" className={active ? `raci-letter raci-letter--${letter} is-active` : `raci-letter raci-letter--${letter}`}
                        aria-pressed={active} title={t(`raci.names.${letter}`)} aria-label={t('raci.badge', { role: t(`raci.names.${letter}`), lane: name })}
                        data-testid={`raci-${lane.id}-${letter}`} onClick={() => void setRaci(editor, step.id, lane.id, active ? null : letter)}>
                        {letter}
                      </button>
                    );
                  })}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <p className="raci-panel-help">{t('raci.help')}</p>
    </div>
  );
}
