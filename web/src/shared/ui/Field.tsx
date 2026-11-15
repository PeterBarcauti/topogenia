// Field.tsx
// A labelled field of a form: its label, its help and its error tied to the control for assistive technologies.

import { useId, type ReactNode } from 'react';

/** What the control of a field carries: its identifier, its state and the texts that describe it. */
export interface FieldControl {
  readonly id: string;
  readonly className: string;
  readonly 'data-testid': string;
  readonly 'aria-invalid': boolean;
  readonly 'aria-describedby': string | undefined;
}

export interface FieldProps {
  readonly label: string;
  readonly testId: string;
  readonly hint?: string;
  readonly error?: string;
  /** Draws the control (an input, an area, a list) with what it must carry. */
  readonly children: (control: FieldControl) => ReactNode;
}

export function Field({ label, testId, hint, error, children }: FieldProps) {
  const id = useId();
  const hintId = hint === undefined ? undefined : `${id}-hint`;
  const errorId = error === undefined ? undefined : `${id}-error`;
  const described = [hintId, errorId].filter((part) => part !== undefined).join(' ');
  return (
    <div className="field">
      <label htmlFor={id} className="field-label">{label}</label>
      {children({ id, className: 'field-control', 'data-testid': testId, 'aria-invalid': error !== undefined, 'aria-describedby': described === '' ? undefined : described })}
      {hint !== undefined && <span id={hintId} className="field-hint">{hint}</span>}
      {error !== undefined && <span id={errorId} className="field-error" role="alert">{error}</span>}
    </div>
  );
}
