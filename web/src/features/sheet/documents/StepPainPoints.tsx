// StepPainPoints.tsx
// « Irritants de l'étape » (R10): the pain points raised on the step that no use of a document carries (a package may bring some), after the outputs, folded with its count; each with its code, its status, its description, its time lost and its action plan, read only; its status button for who may fill the sheet, out of the view in BPMN 2.0.

import { useTranslation } from 'react-i18next';
import { FoldSection } from '../FoldSection.tsx';
import { useSheet } from '../SheetContext.tsx';
import { setPainPointStatus } from './documentWrites.ts';
import { PainPointLine } from './DocumentParts.tsx';
import { PainPointStatusField } from './PainPointStatusField.tsx';

/** Whether the sheet draws the section: only when the step holds a pain point without a document. */
export const hasStepPainPoints = (view: { readonly step_pain_points: readonly unknown[] }): boolean => view.step_pain_points.length > 0;

/**
 * The section, drawn only when the step holds such a pain point. Its description and its plan are written through the use
 * of a document only: here they are read; the status is chosen under the right to fill this sheet (the rule of the core).
 */
export function StepPainPoints() {
  const { t } = useTranslation();
  const sheet = useSheet();
  const points = sheet.view.step_pain_points;
  if (points.length === 0) return null;
  // The view in BPMN 2.0 gives the sheet no right (read only, ADR 106).
  const fill = sheet.view.permissions.fill;
  return (
    <FoldSection section="pain_points" title={t('sheet.step_pain_points')} summary={t('sheet.pain_points', { count: points.length })} hint={t(fill ? 'sheet.step_pain_points_help_fill' : 'sheet.step_pain_points_help')}>
      <ul className="documents-list" data-testid="sheet-step-pain-points">
        {points.map((painPoint) => (
          <li key={painPoint.id} className="output step-pain" data-testid={`step-pain-${painPoint.id}`}>
            <PainPointLine painPoint={painPoint} />
            {fill && (
              <PainPointStatusField painPoint={painPoint} testId={`step-pain-status-${painPoint.id}`} write={(status) => setPainPointStatus(sheet, painPoint.id, status)} />
            )}
          </li>
        ))}
      </ul>
    </FoldSection>
  );
}
