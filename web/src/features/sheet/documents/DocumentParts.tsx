// DocumentParts.tsx
// The parts of a document line: the icon of its medium (F-104), its name (a link that opens a new tab, or its path on hover), what is said under it (its medium first, Makigami M3), a pain point with its code, its status and its time lost, its action plan under it (annex B 4, plan 3.1).

import { AppWindow, File, FileSpreadsheet, FileText, Flame, Mail, Pencil, ScrollText, Shapes, X, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { formatDay } from '../../../shared/i18n/formatDate.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { webAddressOf, type Medium } from '../model/documents.ts';
import { useSheet } from '../SheetContext.tsx';

type Document = Schema<'Document'>;

/** The icon of each medium of a document (F-104): in the sheet only, never on the diagram. */
const MEDIUM_ICONS: Readonly<Record<Medium, LucideIcon>> = {
  paper: ScrollText, email: Mail, spreadsheet: FileSpreadsheet, file: File, software: AppWindow, other: Shapes,
};

/** The icon at the head of a document line: its medium, named on hover, or the plain document while the medium is not given. */
export function DocumentIcon({ document }: { document: Document }) {
  const { t } = useTranslation();
  const medium = document.medium ?? null;
  if (medium === null) return <Icon icon={FileText} size={14} />;
  const label = t(`sheet.document.media.${medium}`);
  return (
    <span className="document-medium" role="img" aria-label={label} title={label} data-testid={`document-medium-${document.id}`}>
      <Icon icon={MEDIUM_ICONS[medium]} size={14} />
    </span>
  );
}

/** The name of a document; its identifier when a command beside it is named after the document too. */
export function DocumentName({ document, id }: { document: Document; id?: string }) {
  const { t } = useTranslation();
  const address = webAddressOf(document);
  return address === null
    ? <span id={id} className="document-name" title={document.location ?? undefined}>{document.name}</span>
    : <a id={id} className="document-name" href={address} target="_blank" rel="noopener noreferrer" title={t('sheet.document.open')} data-testid={`document-link-${document.id}`}>{document.name}</a>;
}

/** What is said under a document: the parts given first, its medium, then its path, its sharing and who declared it. */
export function useDocumentDetails(): (document: Document, ...before: (string | null)[]) => string {
  const { t } = useTranslation();
  const { nameOf } = useSheet();
  return (document, ...before) => [
    ...before,
    document.medium == null ? null : t(`sheet.document.media.${document.medium}`),
    document.location !== null && document.location_type === 'path' ? document.location : null,
    document.share_confirmed ? t('sheet.document.shared_short') : null,
    t('sheet.document.declared_by', { name: nameOf(document.created_by) }),
  ].filter((part) => part !== null && part !== '').join(' · ');
}

const given = (text: string | null | undefined): text is string => text != null && text !== '';

/**
 * The action plan of a pain point, on a line under it when one of its parts is given (Makigami M7; audit 3.1 V3, U3):
 * the action, who carries it, the day it is due as the language of the interface writes it.
 */
function ActionPlanLine({ painPoint }: { painPoint: Schema<'PainPoint'> }) {
  const { t, i18n } = useTranslation();
  const { action, owner, due_date: due } = painPoint;
  const parts = [
    given(action) ? t('sheet.pain_point.plan_action', { action }) : null,
    given(owner) ? t('sheet.pain_point.plan_owner', { owner }) : null,
    given(due) ? t('sheet.pain_point.plan_due', { date: formatDay(due, i18n.language) }) : null,
  ].filter((part) => part !== null);
  if (parts.length === 0) return null;
  return <p className="document-pain-plan" data-testid={`pain-point-plan-${painPoint.id}`}>{parts.join(' · ')}</p>;
}

/** A pain point: its code, its status among the seven, its description and the time it costs (B-4-77, B-4-80, B-4-81), its action plan under it. */
export function PainPointLine({ painPoint }: { painPoint: Schema<'PainPoint'> }) {
  const { t } = useTranslation();
  return (
    <>
      <p className="document-pain" data-testid={`pain-point-${painPoint.id}`}>
        <Icon icon={Flame} size={12} />
        <strong>{painPoint.code}</strong> · {t(`sheet.pain_point.statuses.${painPoint.status}`)} : {painPoint.description}
        {painPoint.lost_hours_per_month !== null && <> · {t('sheet.pain_point.lost_short', { hours: painPoint.lost_hours_per_month })}</>}
      </p>
      <ActionPlanLine painPoint={painPoint} />
    </>
  );
}

/** Edit and remove a document declared by the step (B-4-70: Lucide icons in place of the characters of the 1.x), in the group of the commands of its line. */
export function DocumentButtons({ name, busy, onEdit, onRemove, testId }: { name: string; busy: boolean; onEdit: () => void; onRemove: () => void; testId: string }) {
  const { t } = useTranslation();
  return (
    <>
      <button type="button" className="icon-button" aria-label={t('sheet.document.edit', { name })} title={t('sheet.document.edit', { name })} disabled={busy}
        data-testid={`${testId}-edit`} onClick={onEdit}>
        <Icon icon={Pencil} size={14} />
      </button>
      <button type="button" className="icon-button" aria-label={t('sheet.document.remove', { name })} title={t('sheet.document.remove', { name })} disabled={busy}
        data-testid={`${testId}-remove`} onClick={onRemove}>
        <Icon icon={X} size={14} />
      </button>
    </>
  );
}
