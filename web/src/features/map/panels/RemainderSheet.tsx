// RemainderSheet.tsx
// The sheet of the dissolved step in its remainder, read only, as the viewer may see it: its fields, its procedure, its free text (annex B 2.9, B-8.1-18).

import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { readableDuration } from '../model/stepFacts.ts';

/** The fields of the sheet in their order, each with its text in the language shown, null when empty. */
function useFields(content: Schema<'SheetContent'>): [string, string][] {
  const { t } = useTranslation();
  const fields: [string, string | null][] = [
    ['actor', content.actor], ['purpose', content.purpose], ['tools', content.tools],
    ['frequency', content.frequency === null ? null : t(`map.frequencies.${content.frequency}`)],
    ['duration', content.duration_minutes === null ? null : readableDuration(content.duration_minutes)],
  ];
  return fields.filter((field): field is [string, string] => field[1] !== null && field[1].trim() !== '');
}

/** Nothing when the viewer may see no content of it (spec 5.6 point 1: a reader without validated version sees an empty sheet). */
export function RemainderSheet({ sheet }: { sheet: Schema<'RemainderSheetView'> | null }) {
  const { t } = useTranslation();
  const content = sheet?.content ?? null;
  const fields = useFields(content ?? { actor: null, purpose: null, tools: null, frequency: null, duration_minutes: null, procedure: [], free_text: null });
  if (content === null) return null;
  const freeText = content.free_text?.trim() ?? '';
  if (fields.length === 0 && content.procedure.length === 0 && freeText === '') return null;
  return (
    <section className="remainder-section remainder-sheet" data-testid="remainder-sheet">
      <h3>{t('frame.sheet')}</h3>
      {fields.length > 0 && (
        <dl>
          {fields.map(([name, text]) => (
            <div key={name}>
              <dt>{t(`frame.fields.${name}`)}</dt>
              <dd className={name === 'duration' ? 'duration-value' : undefined}>{text}</dd>
            </div>
          ))}
        </dl>
      )}
      {content.procedure.length > 0 && (
        <>
          <h4>{t('frame.procedure')}</h4>
          <ol>{content.procedure.map((task, index) => <li key={index}>{task.tool === null ? task.text : `${task.text} · ${task.tool}`}</li>)}</ol>
        </>
      )}
      {freeText !== '' && <p className="remainder-free-text">{freeText}</p>}
    </section>
  );
}
