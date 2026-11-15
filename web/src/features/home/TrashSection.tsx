// TrashSection.tsx
// The projects in the trash, under the projects when there is one: restore for all, delete for good for the administrator (B-1.3-15 to 20).

import { useState } from 'react';
import { RotateCcw, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../api/client.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { formatDate } from '../../shared/i18n/formatDate.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Confirm } from '../../shared/ui/Confirm.tsx';
import { Icon } from '../../shared/ui/Icon.tsx';
import { notify } from '../../shared/ui/toasts.ts';

type TrashItem = Schema<'TrashItem'>;

export interface TrashSectionProps {
  readonly trash: Schema<'TrashView'>;
  readonly isAdmin: boolean;
  /** Reads the projects and the trash again after an action, done or refused. */
  readonly onChange: () => void;
}

export function TrashSection({ trash, isAdmin, onChange }: TrashSectionProps) {
  const { t, i18n } = useTranslation();
  const [toErase, setToErase] = useState<TrashItem | null>(null);
  const date = (iso: string) => formatDate(iso, i18n.language);

  const act = async (action: () => Promise<unknown>, done: string) => {
    try {
      await action();
      notify(done, 'success');
    } catch (failure) {
      notify(messageOf(failure), 'error');
    }
    onChange();
  };

  if (trash.projects.length === 0) return null;
  return (
    <section className="trash" aria-labelledby="trash-title" data-testid="trash">
      <div className="trash-head">
        <h2 id="trash-title"><Icon icon={Trash2} size={15} />{t('home.trash')} ({trash.projects.length})</h2>
        <p>{t('home.trash_help')}</p>
      </div>
      <ul className="trash-list">
        {trash.projects.map((item) => (
          <li key={item.id} className="trash-row" data-testid={`trash-${item.code}`}>
            <span className="grow">
              <b>{item.name}<span className="chip-code">{item.code}</span></b>
              <small>
                {t('home.steps', { count: item.step_count })} · {t('home.trash_line', { on: date(item.deleted_at), by: item.deleted_by ?? t('home.someone'), purge: date(item.purge_at) })}
              </small>
            </span>
            <Button testId={`trash-${item.code}-restore`} icon={RotateCcw} variant="secondary"
              onClick={() => void act(() => api.post('/api/trash/{id}/restore', { path: { id: item.id } }), t('home.restored', { code: item.code }))}>
              {t('home.restore')}
            </Button>
            {isAdmin && (
              <Button testId={`trash-${item.code}-erase`} variant="danger" onClick={() => {
                setToErase(item);
              }}>{t('home.erase')}</Button>
            )}
          </li>
        ))}
      </ul>
      {toErase !== null && (
        <Confirm title={t('home.erase_title', { code: toErase.code })} testId="erase-project" danger confirmLabel={t('home.erase')}
          onCancel={() => {
            setToErase(null);
          }}
          onConfirm={() => {
            const item = toErase;
            setToErase(null);
            void act(() => api.delete('/api/trash/{id}', { path: { id: item.id } }), t('home.erased', { code: item.code }));
          }}>
          <p>{t('home.erase_text', { name: toErase.name, code: toErase.code })}</p>
        </Confirm>
      )}
    </section>
  );
}
