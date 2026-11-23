// PainPointStatusField.tsx
// The status of a pain point on a sheet, once the host holds it (ADR 116): « État », its button that opens the list and its help; the choice made written at once, no button to save, shown until the host answers, then the status the host gives back; drawn under a use of an input, under a pain point of the step without a document and under the pain point a consumer of an output noted (R10).

import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Field } from '../../../shared/ui/Field.tsx';
import type { StatusCode } from '../model/painPointStatus.ts';
import { PainPointStatusOptions } from './PainPointStatusOptions.tsx';

export interface StatusFieldProps {
  readonly painPoint: Schema<'PainPoint'>;
  readonly testId: string;
  /** The write of the status chosen, in the queue of the documents of the sheet. */
  readonly write: (status: StatusCode) => Promise<unknown>;
}

export function PainPointStatusField({ painPoint, testId, write }: StatusFieldProps) {
  const { t } = useTranslation();
  const [chosen, setChosen] = useState<StatusCode | null>(null);
  // The latest choice: two choices in a row leave one after the other, the second one stays shown until it is answered.
  const latest = useRef(0);
  const choose = (status: StatusCode) => {
    latest.current += 1;
    const asked = latest.current;
    setChosen(status);
    void write(status).finally(() => {
      if (asked === latest.current) setChosen(null);
    });
  };
  return (
    <Field label={t('sheet.pain_point.status')} testId={testId} hint={t('sheet.pain_point.status_help')}>
      {(control) => (
        <PainPointStatusOptions code={painPoint.code} held={painPoint.status} shown={chosen ?? painPoint.status} testId={control['data-testid']} className={control.className}
          id={control.id} describedBy={control['aria-describedby']} onChoose={choose} />
      )}
    </Field>
  );
}
