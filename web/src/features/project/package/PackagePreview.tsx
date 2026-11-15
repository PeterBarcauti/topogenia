// PackagePreview.tsx
// What the preview of a package says before anything is written: counts, people, code, what the import settles (spec 7.3, ADR 052).

import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';

export type Preview = Schema<'ImportPreview'>;

/** The key of the text of the code a new project would take. */
const CODE_TEXT: Readonly<Record<Schema<'CodeState'>, string>> = { free: 'package.code_free', taken: 'package.code_taken', in_trash: 'package.code_in_trash' };

export function PackagePreview({ preview, replacing }: { preview: Preview; replacing: boolean }) {
  const { t } = useTranslation();
  const { counts, settlement } = preview;
  const released = settlement.released_lanes + settlement.released_steps > 0;
  return (
    <section className="package-preview" aria-labelledby="package-preview-title" data-testid="package-preview">
      <h3 id="package-preview-title">{t('package.preview', { code: preview.package_code })}</h3>
      <p>{t('package.counts', { ...counts })}</p>
      {!preview.with_people && <p>{t('package.no_people')}</p>}
      <p>{t('package.members_count', { count: preview.members })}</p>
      {preview.unknown_logins.length > 0 && <p>{t('package.unknown_logins', { logins: preview.unknown_logins.join(', ') })}</p>}
      {settlement.importer_added && <p>{t('package.importer_added')}</p>}
      {released && <p className="message-warning">{t('package.released', { lanes: settlement.released_lanes, steps: settlement.released_steps })}</p>}
      {replacing
        ? preview.trash_code !== null && <p data-testid="package-preview-trash">{t('package.trash_code', { code: preview.trash_code })}</p>
        : <p className={preview.code_state === 'free' ? '' : 'message-error'} data-testid="package-preview-code">{t(CODE_TEXT[preview.code_state], { code: preview.project_code })}</p>}
    </section>
  );
}
