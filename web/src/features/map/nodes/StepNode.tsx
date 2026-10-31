// StepNode.tsx
// A step on the canvas: shape of its type in the ink of its lane, code, label or its field, pictograms, gauge, warning, detailed step, RACI badges, handles, quick add, states (annex B 2.9 to 2.11, 3.2, 3.3, spec 9.4).

import { memo, type CSSProperties } from 'react';
import { Handle, type NodeProps } from '@xyflow/react';
import type { LucideIcon } from 'lucide-react';
import { Circle, Plus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { ownName } from '../../../shared/i18n/languageNames.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { HANDLE_POSITIONS, SIDES, type StepNode } from '../canvas/flowElements.ts';
import { STEP_HEIGHT, STEP_WIDTH } from '../model/geometry/constants.ts';
import { renameStep, stepTyped } from '../state/actions/rename.ts';
import { STEP_LABEL_MAX } from '../state/actions/steps.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
import { LabelInput } from './LabelInput.tsx';
import { hasPictos, Pictos } from './Pictos.tsx';
import { QuickAdd } from './QuickAdd.tsx';
import { RaciBadges } from './RaciBadges.tsx';
import { DetailedBadges, LayoutWarning, SheetGauge } from './StepBadges.tsx';

/** The markers of the gateways, centred in the diamond: exclusive ×, parallel +, inclusive ○ (B-3.2-08 to B-3.2-10). */
const MARKERS: Partial<Record<Schema<'StepType'>, LucideIcon>> = { decision: X, parallel: Plus, inclusive: Circle };

/** The diamond of a gateway, drawn in the box of a step, one pixel inside for its contour. */
const DIAMOND = `1,${String(STEP_HEIGHT / 2)} ${String(STEP_WIDTH / 2)},1 ${String(STEP_WIDTH - 1)},${String(STEP_HEIGHT / 2)} ${String(STEP_WIDTH / 2)},${String(STEP_HEIGHT - 1)}`;

/**
 * The classes of a step: its type, gateway, chosen, cut, source of a link, detailed, with pictograms (the label moves up
 * when they are always shown, B-2.10-14), what hangs under it aside of the axis of its bottom point (AR-58, AR-128),
 * covered by the frame of a dissolution (B-2.9-19).
 */
function classesOf(data: StepNode['data'], selected: boolean): string {
  const { step, macro, under, outside } = data.figure;
  return ['step', `step--${step.type}`, MARKERS[step.type] && 'step--gateway', selected && 'is-selected', data.mark !== null && `is-ai-${data.mark}`, data.cut && 'is-cut', data.linkSource && 'is-link-source',
    macro && 'is-macro', hasPictos(step) && 'has-pictos', under !== 'centre' && `is-under-${under}`, outside && 'is-outside']
    .filter(Boolean)
    .join(' ');
}

/**
 * The label of the step, or the field where its name is typed, from its label or from the character typed on it
 * (B-2.3-11, B-2.5-28 to B-2.5-32, AR-151); in the language shown, a label not written in it shown to translate, and
 * typed as its translation (ADR 083).
 */
function StepLabel({ data }: { data: StepNode['data'] }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const { step, label, untranslated } = data.figure;
  const seed = useMapState((state) => (state.editingSeed?.stepId === step.id ? state.editingSeed.text : null));
  const shown = useMapState((state) => state.shown?.language ?? null);
  if (!data.editing) {
    return untranslated && shown !== null
      ? <span className="step-label untranslated" title={t('process_language.untranslated', { language: ownName(shown) })} data-testid="step-label">{label}</span>
      : <span className="step-label" data-testid="step-label">{label}</span>;
  }
  const stop = () => {
    editor.store.setState({ editing: null, editingSeed: null });
  };
  return (
    <LabelInput initial={seed ?? stepTyped(editor, step)} begun={seed !== null} label={t('map.rename')} max={STEP_LABEL_MAX} testId="step-label-input"
      placeholder={untranslated ? step.label : undefined} onCancel={stop}
      onDone={(text) => {
        stop();
        void renameStep(editor, step, text);
      }} />
  );
}

export const StepNodeView = memo(function StepNodeView({ data, selected }: NodeProps<StepNode>) {
  const { step, tint, parity, warnings, gauge, macro, raci, raciTop } = data.figure;
  const marker = MARKERS[step.type];
  const style = { '--ink': `var(--lane-${String(tint)}-ink)`, '--lane-bg': `var(--lane-${parity})`, '--raci-top': `${String(raciTop)}px` } as CSSProperties;
  return (
    <div className={classesOf(data, selected)} style={style} title={`${step.code} · ${step.path}`} data-testid={`step-${step.code}`}>
      {SIDES.map((side) => <Handle key={side} id={side} type="source" position={HANDLE_POSITIONS[side]} className="step-handle" isConnectable={data.connectable} />)}
      {macro && <span className="step-stack" aria-hidden="true" />}
      {marker && (
        <svg className="gateway-shape" viewBox={`0 0 ${String(STEP_WIDTH)} ${String(STEP_HEIGHT)}`} aria-hidden="true">
          <polygon points={DIAMOND} />
        </svg>
      )}
      {data.showCodes && <span className="step-code" data-testid="step-code">{step.code}</span>}
      {marker && <span className="gateway-marker" aria-hidden="true"><Icon icon={marker} size={16} strokeWidth={2.5} /></span>}
      <StepLabel data={data} />
      <Pictos step={step} />
      {gauge !== null && <SheetGauge gauge={gauge} completeness={step.sheet.completeness} />}
      {warnings.length > 0 && <LayoutWarning codes={warnings} />}
      {macro && !data.showStream && <DetailedBadges frequency={step.sheet.frequency} />}
      {data.showRaci && raci !== null && <RaciBadges raci={raci} />}
      {data.quickAdd !== null && !data.editing && <QuickAdd stepId={step.id} offered={data.quickAdd} />}
    </div>
  );
});
