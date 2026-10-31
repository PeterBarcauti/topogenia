// Confirm.tsx
// A question to confirm before an action: the message, Cancel, and the confirming button (danger for what removes).

import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from './Button.tsx';
import { Dialog } from './Dialog.tsx';

export interface ConfirmProps {
  readonly title: string;
  readonly testId: string;
  readonly children: ReactNode;
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
  /** The label of the confirming button; « Confirm » by default. */
  readonly confirmLabel?: string;
  readonly danger?: boolean;
  /** The confirming button stays disabled until the condition holds (the code typed before a deletion, annex B 1.3). */
  readonly ready?: boolean;
}

export function Confirm({ title, testId, children, onConfirm, onCancel, confirmLabel, danger = false, ready = true }: ConfirmProps) {
  const { t } = useTranslation();
  const footer = (
    <>
      <Button testId={`${testId}-cancel`} variant="secondary" onClick={onCancel}>{t('app.cancel')}</Button>
      <Button testId={`${testId}-confirm`} variant={danger ? 'danger' : 'primary'} disabled={!ready} onClick={onConfirm}>
        {confirmLabel ?? t('app.confirm')}
      </Button>
    </>
  );
  return <Dialog title={title} testId={testId} onClose={onCancel} footer={footer}>{children}</Dialog>;
}
