// ReadView.tsx
// The sheet for who may not fill it: its latest validated version, in the sections of the form, folded when it opens (essai J10): the key facts, only the fields filled; the documents used; the procedure numbered; the documents produced; the pain points of the step without a document (R10); the tools as pills, always in sight; the free text (annex B 4).

import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { formatDate } from '../../../shared/i18n/formatDate.ts';
import { InputList } from '../documents/InputList.tsx';
import { OutputList } from '../documents/OutputList.tsx';
import { hasStepPainPoints, StepPainPoints } from '../documents/StepPainPoints.tsx';
import { FoldAll, FoldSection } from '../FoldSection.tsx';
import { inputGroups } from '../model/documents.ts';
import { citedTools } from '../model/draft.ts';
import { durationText, factsText, leadTimeText } from '../model/durationText.ts';
import { Responsible } from '../Responsible.tsx';
import { SHEET_SECTIONS, useSheet, type SheetSection } from '../SheetContext.tsx';
import { ToolPills } from '../ToolPills.tsx';

type SheetContent = Schema<'SheetContent'>;

/** The fields filled, each under its name, the numbers as the language of the interface writes them; nothing for an empty one (B-4-58). */
function Fields({ content, names }: { content: SheetContent; names: readonly ('actor' | 'purpose' | 'tools' | 'frequency' | 'duration' | 'lead_time')[] }) {
  const { t, i18n } = useTranslation();
  const text = (name: (typeof names)[number]): string | null => {
    if (name === 'frequency') return content.frequency === null ? null : t(`map.frequencies.${content.frequency}`);
    if (name === 'duration') return durationText(content, t, i18n.language);
    if (name === 'lead_time') return leadTimeText(content, t, i18n.language);
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

/** The inputs the step uses, the only ones a reader sees (B-4-77). */
const usedInputs = (view: Schema<'SheetView'>): number => inputGroups(view.inputs, false).length;

/** A list of documents in its section; the documents stay visible without a validated version (B-4-61), only the inputs used (B-4-77). */
function ReadDocuments({ section }: { section: 'inputs' | 'outputs' }) {
  const { t } = useTranslation();
  const { view } = useSheet();
  const count = section === 'inputs' ? usedInputs(view) : view.outputs.length;
  return (
    <FoldSection section={section} title={t(`sheet.${section}`)} summary={count === 0 ? t('sheet.empty') : t('sheet.documents', { count })}>
      {section === 'inputs' ? <InputList /> : <OutputList />}
    </FoldSection>
  );
}

/**
 * « Version validée le… par… », or « Aucune fiche validée… » (B-4-60). The sheet read only of a member who sees its draft
 * (the view in BPMN 2.0, ADR 106) shows that draft, said as a draft, never as the version validated before it, nor as no
 * sheet at all (audit of the copy 64, P1-03).
 */
export function ReadView() {
  const { t, i18n } = useTranslation();
  const { view, nameOf } = useSheet();
  // A sheet never filled is no draft to show: it says it has no validated sheet yet.
  const draft = view.is_draft && view.status !== 'empty';
  const validated = draft ? null : view.validated;
  const shown = draft || validated !== null;
  const content = view.content;
  const freeText = content.free_text ?? '';
  const tools = citedTools(content.tools ?? '');
  // The key facts in the head of their section: the frequency, the duration and the lead time, else the bounds of the duration, else the actor.
  const facts = factsText(content, t, i18n.language) ?? durationText(content, t, i18n.language) ?? content.actor ?? '';
  const known = facts !== '' || (content.purpose ?? '') !== '';
  // The sections drawn, those « Tout déplier » acts on: the documents without a validated version too (B-4-61), both
  // lists when the step uses or produces one; the tools and the free text do not fold here.
  const documents = usedInputs(view) > 0 || view.outputs.length > 0;
  const drawn: Partial<Record<SheetSection, boolean>> = {
    facts: shown && known, inputs: documents, procedure: shown && content.procedure.length > 0, outputs: documents, pain_points: hasStepPainPoints(view),
  };
  const sections = SHEET_SECTIONS.filter((section) => drawn[section] === true);
  return (
    <div className="side-panel-body sheet-body sheet-read" data-testid="sheet-read">
      <Responsible />
      {draft && <p className="field-hint" data-testid="sheet-read-draft">{t('sheet.read_draft')}</p>}
      {!shown && <p className="sheet-empty" data-testid="sheet-read-empty">{t('sheet.read_empty')}</p>}
      {validated !== null && (
        <p className="field-hint" data-testid="sheet-read-validated">
          {t('sheet.read_validated', { date: formatDate(validated.validated_at, i18n.language), name: nameOf(validated.validated_by) })}
        </p>
      )}
      {sections.length > 0 && <FoldAll sections={sections} />}
      {drawn.facts === true && (
        <FoldSection section="facts" title={t('sheet.facts')} summary={facts}>
          <Fields content={content} names={['actor', 'purpose', 'frequency', 'duration', 'lead_time']} />
        </FoldSection>
      )}
      {documents && <ReadDocuments section="inputs" />}
      {drawn.procedure === true && (
        <FoldSection section="procedure" title={t('sheet.procedure')} summary={t('sheet.operations', { count: content.procedure.length })}>
          <ReadProcedure procedure={content.procedure} />
        </FoldSection>
      )}
      {documents && <ReadDocuments section="outputs" />}
      <StepPainPoints />
      {shown && tools.length > 0 && (
        <div className="sheet-read-tools" data-testid="sheet-read-tools">
          <strong>{t('sheet.tools')}</strong>
          <ToolPills tools={tools} />
        </div>
      )}
      {shown && freeText !== '' && (
        <div className="sheet-read-free" data-testid="sheet-read-free">
          <strong>{t('sheet.mode_free')}</strong>
          <p>{freeText}</p>
        </div>
      )}
    </div>
  );
}
