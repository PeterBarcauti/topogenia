// ArrangementDialog.tsx
// Where « Finish » stored the remainder of a frame: « nom → CODE », « → la carte », « supprimé », « déjà reçu par CODE », and why when it was stored on its own (annex B 2.9).

import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { arrangementOutcome } from '../model/remainder.ts';

type Arrangement = Schema<'Arrangement'>;

export interface ArrangementDialogProps {
  readonly code: string;
  readonly arrangements: readonly Arrangement[];
  readonly auto: boolean;
  readonly onClose: () => void;
}

/** B-2.9-37 to B-2.9-39: one line an element, the text « auto » first after the finish on its own; the button Close has the focus. */
export function ArrangementDialog({ code, arrangements, auto, onClose }: ArrangementDialogProps) {
  const { t } = useTranslation();
  const textOf = (arrangement: Arrangement): string => {
    const values = { name: arrangement.name, code: arrangement.step_code ?? '' };
    switch (arrangementOutcome(arrangement)) {
      case 'duplicate':
        return t('frame.stored_duplicate', values);
      case 'to-step':
        return t('frame.stored_to_step', values);
      case 'to-map':
        return t('frame.stored_to_map', values);
      case 'deleted':
        return t('frame.stored_deleted', values);
    }
  };
  const footer = <Button testId="arrangements-done" variant="primary" data-autofocus onClick={onClose}>{t('app.close')}</Button>;
  return (
    <Dialog title={t('frame.stored_title', { code })} testId="arrangements" onClose={onClose} footer={footer}>
      {auto && <p data-testid="arrangements-auto">{t('frame.stored_auto')}</p>}
      <ul className="arrangement-list">
        {arrangements.map((arrangement) => <li key={`${arrangement.kind}:${arrangement.object_id}`}>{textOf(arrangement)}</li>)}
      </ul>
    </Dialog>
  );
}
