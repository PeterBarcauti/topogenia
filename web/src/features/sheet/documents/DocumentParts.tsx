// DocumentParts.tsx
// The parts of a document line: its name (a link that opens a new tab, or its path on hover), what is said under it, a pain point with its code, its status and its time lost (annex B 4).

import { Flame, Pencil, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { webAddressOf } from '../model/documents.ts';
import { useSheet } from '../SheetContext.tsx';

type Document = Schema<'Document'>;

export function DocumentName({ document }: { document: Document }) {
  const { t } = useTranslation();
  const address = webAddressOf(document);
  return address === null
    ? <span className="document-name" title={document.location ?? undefined}>{document.name}</span>
    : <a className="document-name" href={address} target="_blank" rel="noopener noreferrer" title={t('sheet.document.open')} data-testid={`document-link-${document.id}`}>{document.name}</a>;
}

/** What is said under a document: the parts given first, then its path, its sharing and who declared it. */
export function useDocumentDetails(): (document: Document, ...before: (string | null)[]) => string {
  const { t } = useTranslation();
  const { nameOf } = useSheet();
  return (document, ...before) => [
    ...before,
    document.location !== null && document.location_type === 'path' ? document.location : null,
    document.share_confirmed ? t('sheet.document.shared_short') : null,
    t('sheet.document.declared_by', { name: nameOf(document.created_by) }),
  ].filter((part) => part !== null && part !== '').join(' · ');
}

/** A pain point: its code, its status among the seven, its description and the time it costs (B-4-77, B-4-80, B-4-81). */
export function PainPointLine({ painPoint }: { painPoint: Schema<'PainPoint'> }) {
  const { t } = useTranslation();
  return (
    <p className="document-pain" data-testid={`pain-point-${painPoint.id}`}>
      <Icon icon={Flame} size={12} />
      <strong>{painPoint.code}</strong> · {t(`sheet.pain_point.statuses.${painPoint.status}`)} : {painPoint.description}
      {painPoint.lost_hours_per_month !== null && <> · {t('sheet.pain_point.lost_short', { hours: painPoint.lost_hours_per_month })}</>}
    </p>
  );
}

/** Edit and remove a document declared by the step (B-4-70: Lucide icons in place of the characters of the 1.x). */
export function DocumentButtons({ name, busy, onEdit, onRemove, testId }: { name: string; busy: boolean; onEdit: () => void; onRemove: () => void; testId: string }) {
  const { t } = useTranslation();
  return (
    <span className="link-btns">
      <button type="button" className="icon-button" aria-label={t('sheet.document.edit', { name })} title={t('sheet.document.edit', { name })} disabled={busy}
        data-testid={`${testId}-edit`} onClick={onEdit}>
        <Icon icon={Pencil} size={14} />
      </button>
      <button type="button" className="icon-button" aria-label={t('sheet.document.remove', { name })} title={t('sheet.document.remove', { name })} disabled={busy}
        data-testid={`${testId}-remove`} onClick={onRemove}>
        <Icon icon={X} size={14} />
      </button>
    </span>
  );
}
