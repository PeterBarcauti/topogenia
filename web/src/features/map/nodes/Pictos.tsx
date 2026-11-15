// Pictos.tsx
// The pictograms of a step: documents produced and received, open pain points, duration of the sheet, each only when not zero (annex B 2.10).

import type { LucideIcon } from 'lucide-react';
import { FileText, Flame, Timer } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { readableDuration } from '../model/stepFacts.ts';

type StepView = Schema<'StepView'>;

/** Whether a step has a pictogram to show: documents, open pain points or a duration (B-2.10-11). */
export const hasPictos = (step: StepView): boolean =>
  step.markers.documents.length > 0 || step.markers.pain_points.length > 0 || (step.sheet.duration_minutes ?? 0) > 0;

function Picto({ icon, text, help, kind, className }: { icon: LucideIcon; text: string; help: string; kind: string; className?: string }) {
  return (
    <span className={className} role="img" aria-label={help} title={help} data-testid={`picto-${kind}`}>
      <Icon icon={icon} size={10} strokeWidth={2.25} />
      {text}
    </span>
  );
}

/** Ten pixels in the bottom right corner, the pain points in muted red; the details on hover (B-2.10-05 to B-2.10-09). */
export function Pictos({ step }: { step: StepView }) {
  const { t } = useTranslation();
  if (!hasPictos(step)) return null;
  const { documents, pain_points: painPoints } = step.markers;
  const duration = step.sheet.duration_minutes ?? 0;
  const names = documents.map((document) => `${document.name} (${t(document.direction === 'output' ? 'map.produced' : 'map.received')})`).join(', ');
  const list = painPoints.map((point) => `${point.code} ${point.description}`).join(' ; ');
  return (
    <span className="step-pictos" data-testid="step-pictos">
      {documents.length > 0 && <Picto icon={FileText} kind="documents" text={String(documents.length)} help={t('map.pictos_documents', { names })} />}
      {painPoints.length > 0 && <Picto icon={Flame} kind="pain-points" className="picto-irritant" text={String(painPoints.length)} help={t('map.pictos_pain_points', { list })} />}
      {duration > 0 && <Picto icon={Timer} kind="duration" text={readableDuration(duration)} help={t('map.pictos_duration', { duration: readableDuration(duration) })} />}
    </span>
  );
}
