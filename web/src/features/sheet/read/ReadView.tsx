// ReadView.tsx
// The sheet for who may not fill it: its latest validated version, only the fields filled, the procedure numbered, the documents used and produced, the free text (annex B 4).

import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { formatDate } from '../../../shared/i18n/formatDate.ts';
import { InputList } from '../documents/InputList.tsx';
import { OutputList } from '../documents/OutputList.tsx';
import { FoldSection } from '../FoldSection.tsx';
import { inputGroups } from '../model/documents.ts';
import { Responsible } from '../Responsible.tsx';
import { useSheet } from '../SheetContext.tsx';

type SheetContent = Schema<'SheetContent'>;

/** The fields filled, each under its name; nothing for an empty one (B-4-58). */
function Fields({ content, names }: { content: SheetContent; names: readonly ('actor' | 'purpose' | 'tools' | 'frequency' | 'duration' | 'lead_time')[] }) {
  const { t } = useTranslation();
  const text = (name: (typeof names)[number]): string | null => {
    if (name === 'frequency') return content.frequency === null ? null : t(`map.frequencies.${content.frequency}`);
    if (name === 'duration') return content.duration_minutes === null ? null : t('sheet.duration_value', { count: content.duration_minutes });
    if (name === 'lead_time') return content.lead_time == null ? null : `${String(content.lead_time.value)} ${t(`sheet.lead_time_units.${content.lead_time.unit}`)}`;
    return content[name];
  };
  const filled = names.map((name) => [name, text(name)] as const).filter((field): field is readonly [typeof field[0], string] => field[1] !== null && field[1] !== '');
  if (filled.length === 0) return null;
  return (
    <dl className="sheet-read-fields">
      {filled.map(([name, value]) => (
        <div key={name} data-testid={`sheet-read-${name}`}>
          <dt>{t(`sheet.${name}`)}</dt>
          <dd className={name === 'duration' ? 'duration-value' : undefined}>{value}</dd>
        </div>
      ))}
    </dl>
  );
}

/** The procedure numbered, the tool and the nature muted, the checks in green (B-4-59). */
function ReadProcedure({ procedure }: { procedure: SheetContent['procedure'] }) {
  const { t } = useTranslation();
  return (
    <ol className="procedure-read" data-testid="sheet-read-procedure">
      {procedure.map((task, index) => (
        <li key={index} className={task.nature === 'check' ? 'is-check' : undefined}>
          {task.text}
          {(task.tool !== null || task.nature !== null) && (
            <span className="field-hint"> · {[task.tool, task.nature === null ? null : t(`sheet.natures.${task.nature}`)].filter((part) => part !== null).join(' · ')}</span>
          )}
        </li>
      ))}
    </ol>
  );
}

/** A list of documents in its section; the documents stay visible without a validated version (B-4-61), only the inputs used (B-4-77). */
function ReadDocuments({ section }: { section: 'inputs' | 'outputs' }) {
  const { t } = useTranslation();
  const { view } = useSheet();
  const inputs = inputGroups(view.inputs, false).length;
  if (inputs === 0 && view.outputs.length === 0) return null;
  const count = section === 'inputs' ? inputs : view.outputs.length;
  return (
    <FoldSection section={section} title={t(`sheet.${section}`)} summary={count === 0 ? t('sheet.empty') : t('sheet.documents', { count })}>
      {section === 'inputs' ? <InputList /> : <OutputList />}
    </FoldSection>
  );
}

/** « Version validée le… par… », or « Aucune fiche validée… » (B-4-60). */
export function ReadView() {
  const { t, i18n } = useTranslation();
  const { view, nameOf } = useSheet();
  const validated = view.validated;
  const content = view.content;
  const freeText = content.free_text ?? '';
  return (
    <div className="side-panel-body sheet-body sheet-read" data-testid="sheet-read">
      <Responsible />
      {validated === null ? <p className="sheet-empty" data-testid="sheet-read-empty">{t('sheet.read_empty')}</p> : (
        <>
          <p className="field-hint" data-testid="sheet-read-validated">
            {t('sheet.read_validated', { date: formatDate(validated.validated_at, i18n.language), name: nameOf(validated.validated_by) })}
          </p>
          <Fields content={content} names={['actor', 'purpose']} />
        </>
      )}
      <ReadDocuments section="inputs" />
      {validated !== null && content.procedure.length > 0 && (
        <FoldSection section="procedure" title={t('sheet.procedure')} summary={t('sheet.operations', { count: content.procedure.length })}>
          <ReadProcedure procedure={content.procedure} />
        </FoldSection>
      )}
      <ReadDocuments section="outputs" />
      {validated !== null && <Fields content={content} names={['tools', 'frequency', 'duration', 'lead_time']} />}
      {validated !== null && freeText !== '' && (
        <div className="sheet-read-free" data-testid="sheet-read-free">
          <strong>{t('sheet.mode_free')}</strong>
          <p>{freeText}</p>
        </div>
      )}
    </div>
  );
}
