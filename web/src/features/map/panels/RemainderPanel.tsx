// RemainderPanel.tsx
// The panel of the remainder of the open frame: what the dissolved step carried, to assign to the steps or to delete, its sheet read only, then « Finish »; docked on the right, not modal (annex B 2.9, spec 9.5).

import { useState } from 'react';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import { Button } from '../../../shared/ui/Button.tsx';
import { Confirm } from '../../../shared/ui/Confirm.tsx';
import { useDock } from '../../../shared/ui/dock.ts';
import { FoldButton } from '../../../shared/ui/FoldButton.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { stepLabel } from '../model/mapLayout.ts';
import { assignCandidates, remainderSize } from '../model/remainder.ts';
import { assignRemainder, finishFrame, removeRemainder } from '../state/actions/dissolution.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';
import { RemainderSheet } from './RemainderSheet.tsx';

type MapView = Schema<'MapView'>;
type FrameView = Schema<'FrameView'>;
type RemainderKind = Schema<'RemainderKind'>;

/** An element of the remainder as a line: its kind, its identifier, its name, what is said of it, the steps that use it. */
interface Element {
  readonly kind: RemainderKind;
  readonly id: string;
  readonly name: string;
  readonly detail: string;
  readonly uses: readonly string[];
}

/** The value of the choice « La carte (sans étape) », for a pain point (B-2.9-31). */
const TO_MAP = 'map';

/** « Affecter à… »: the steps of the frame, then the others, in the reading order; the map for a pain point (B-2.9-29 to B-2.9-31). */
function AssignChoice({ map, frame, element }: { map: MapView; frame: FrameView; element: Element }) {
  const { t } = useTranslation();
  const language = useMapLanguage();
  const editor = useEditor();
  const { inFrame, others } = assignCandidates(map, frame);
  const option = (step: Schema<'StepView'>) => <option key={step.id} value={step.id}>{`${step.code} · ${stepLabel(step, language)}`}</option>;
  return (
    <select className="remainder-assign" aria-label={t('frame.assign_named', { name: element.name })} value="" data-testid={`remainder-assign-${element.id}`}
      onChange={(event) => {
        const chosen = event.target.value;
        if (chosen !== '') void assignRemainder(editor, element.kind, element.id, chosen === TO_MAP ? null : chosen);
      }}>
      <option value="">{t('frame.assign')}</option>
      {inFrame.length > 0 && <optgroup label={t('frame.in_frame')}>{inFrame.map(option)}</optgroup>}
      {others.length > 0 && <optgroup label={t('frame.others')}>{others.map(option)}</optgroup>}
      {element.kind === 'pain_point' && <option value={TO_MAP}>{t('frame.map_no_step')}</option>}
    </select>
  );
}

/** The elements of the remainder by section: documents produced and received, documents used, pain points (B-2.9-25 to B-2.9-27). */
function useSections(frame: FrameView | null): { readonly title: string; readonly elements: readonly Element[] }[] {
  const { t } = useTranslation();
  if (frame === null) return [];
  const documents = frame.documents.map((document): Element => ({
    kind: 'document', id: document.id, name: document.name, uses: document.used_by,
    detail: [t(document.direction === 'output' ? 'frame.produced' : 'frame.received_outside'), document.used_by.length > 0 ? t('frame.used_by', { codes: document.used_by.join(', ') }) : '']
      .filter((part) => part !== '').join(' · '),
  }));
  const usages = frame.usages.map((usage): Element => ({
    kind: 'usage', id: usage.document_id, name: usage.name, uses: [], detail: usage.producer_code === null ? '' : t('frame.produced_by', { code: usage.producer_code }),
  }));
  const painPoints = frame.pain_points.map((pain): Element => ({ kind: 'pain_point', id: pain.id, name: pain.description, uses: [], detail: pain.code }));
  return [
    { title: t('frame.documents'), elements: documents }, { title: t('frame.usages'), elements: usages }, { title: t('frame.pain_points'), elements: painPoints },
  ].filter((section) => section.elements.length > 0);
}

function RemainderLine({ map, frame, element, onRemove }: { map: MapView; frame: FrameView; element: Element; onRemove: (element: Element) => void }) {
  const { t } = useTranslation();
  return (
    <li className="remainder-line" data-testid={`remainder-${element.kind}-${element.id}`}>
      <span className="remainder-name">{element.name}{element.detail !== '' && <small> · {element.detail}</small>}</span>
      {map.rights.structure && <AssignChoice map={map} frame={frame} element={element} />}
      {map.rights.structure && (
        <button type="button" className="remainder-delete" aria-label={t('frame.delete_named', { name: element.name })} data-testid={`remainder-delete-${element.id}`}
          onClick={() => { onRemove(element); }}>
          {t('frame.delete')}
        </button>
      )}
    </li>
  );
}

/** Delete for good, saying which uses go too (B-2.9-32). */
function RemoveConfirm({ element, onClose }: { element: Element; onClose: () => void }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const text = [t('frame.delete_text'), element.uses.length > 0 ? t('frame.delete_uses', { codes: element.uses.join(', ') }) : ''].filter((part) => part !== '').join(' ');
  return (
    <Confirm title={t('frame.delete_named', { name: element.name })} testId="remainder-delete-confirm" danger confirmLabel={t('frame.delete')} onCancel={onClose}
      onConfirm={() => {
        onClose();
        void removeRemainder(editor, element.kind, element.id);
      }}>
      <p>{text}</p>
    </Confirm>
  );
}

/**
 * The panel: Escape closes it, the other keys go on to the canvas (B-2.5-04, B-2.9-24). A reader sees the remainder
 * without the choices nor the buttons, which the host would refuse him (AR-122).
 */
export function RemainderPanel({ map, onClose }: { map: MapView; onClose: () => void }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const [removing, setRemoving] = useState<Element | null>(null);
  // Folded to its strip, the panel leaves Escape to the map (spec 3.0 section 15.2).
  const folded = useDock()?.folded === true;
  useKeyLayer('panel', (event, chord) => {
    // The release of the key that closed a dialog above is no second Escape.
    if (event.type !== 'keydown' || chord !== 'Escape') return false;
    onClose();
    return true;
  }, !folded);
  const sections = useSections(map.dissolution);
  const frame = map.dissolution;
  if (frame === null) return null;
  const title = t('frame.title', { code: frame.step_code, label: frame.step_label });
  const empty = remainderSize(frame) === 0;
  return (
    <aside className="side-panel remainder-panel" aria-label={title} data-testid="remainder-panel">
      <header className="side-panel-head">
        <h2>{title}</h2>
        <FoldButton />
        <button type="button" className="icon-button" aria-label={t('app.close')} title={t('app.close')} data-testid="remainder-panel-close" onClick={onClose}>
          <Icon icon={X} size={16} />
        </button>
      </header>
      <div className="side-panel-body">
        {empty && <p className="remainder-nothing">{t('frame.nothing')}</p>}
        {sections.map((section) => (
          <section key={section.title} className="remainder-section">
            <h3>{section.title}</h3>
            <ul className="remainder-list">
              {section.elements.map((element) => <RemainderLine key={`${element.kind}:${element.id}`} map={map} frame={frame} element={element} onRemove={setRemoving} />)}
            </ul>
          </section>
        ))}
        <RemainderSheet sheet={map.dissolution_sheet} />
      </div>
      {map.rights.structure && (
        <footer className="side-panel-foot">
          <Button testId="remainder-finish" variant="primary" title={empty ? undefined : t('frame.finish_help')} onClick={() => void finishFrame(editor)}>
            {empty ? t('frame.close_frame') : t('frame.finish')}
          </Button>
        </footer>
      )}
      {removing !== null && <RemoveConfirm element={removing} onClose={() => { setRemoving(null); }} />}
    </aside>
  );
}
