// ClassificationField.tsx
// The choice of the classification of a project, at its creation and in its properties: the three levels of the closed list, Internal first (spec 4.1, Q5).

import { useTranslation } from 'react-i18next';
import { Field } from '../../shared/ui/Field.tsx';
import { CLASSIFICATIONS, type Classification } from './classification.ts';

export interface ClassificationFieldProps {
  readonly value: Classification;
  readonly testId: string;
  readonly onChange: (value: Classification) => void;
  /** Shown but not chosen: a member who is not a facilitator reads the properties of the project. */
  readonly disabled?: boolean;
  /** Takes the focus when it shows: the first field of a dialog drawn once its project is read. */
  readonly autoFocus?: boolean;
}

export function ClassificationField({ value, testId, onChange, disabled = false, autoFocus = false }: ClassificationFieldProps) {
  const { t } = useTranslation();
  return (
    <Field label={t('project.classification')} testId={testId} hint={t('project.classification_help')}>
      {(control) => (
        <select {...control} value={value} disabled={disabled} autoFocus={autoFocus} onChange={(event) => { onChange(event.target.value as Classification); }}>
          {CLASSIFICATIONS.map((level) => <option key={level} value={level}>{t(`classification.${level}`)}</option>)}
        </select>
      )}
    </Field>
  );
}
