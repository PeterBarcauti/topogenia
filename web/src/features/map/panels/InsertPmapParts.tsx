// InsertPmapParts.tsx
// The parts of the dialog of the insertion of a .pmap file (3.3, F-208): the file chosen, dropped or pasted; what it holds; the place of the new step; a refusal with the faults of the file.

import { useRef, type DragEvent } from 'react';
import { FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { localizedName } from '../../../shared/i18n/localizedName.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { OPENED_FILES } from '../../project/files/fileConversion.ts';
import { laneRanks, lanesInOrder, readingOrder } from '../model/readingOrder.ts';
import { AT_START, flowChoice, followed, mayFollow, type InsertionError, type InsertionPlace, type InsertionPreview } from './insertPmapForm.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;

export const stepName = (step: StepView, language: string) =>
  localizedName({ name: step.label, source_language: step.source_language, name_translations: step.translations }, language);

/** The file chosen with the button, dropped on the field or pasted in it; the same file chosen again is read again. */
export function FileChoice({ content, fileName, onText, onFile }: {
  content: string; fileName: string | null; onText: (text: string) => void; onFile: (file: File | undefined) => void;
}) {
  const { t } = useTranslation();
  const chooser = useRef<HTMLInputElement>(null);
  const drop = (event: DragEvent<HTMLTextAreaElement>) => {
    event.preventDefault();
    onFile(event.dataTransfer.files[0]);
  };
  return (
    <>
      <div className="file-choice">
        <Button testId="insert-pmap-choose" icon={FileText} variant="secondary" onClick={() => chooser.current?.click()}>{t('files.choose')}</Button>
        <input ref={chooser} type="file" accept={OPENED_FILES} hidden aria-label={t('files.choose')} data-testid="insert-pmap-file"
          onChange={(event) => {
            const input = event.target;
            onFile(input.files?.[0]);
            input.value = '';
          }} />
        {fileName !== null && <span className="file-name">{fileName}</span>}
      </div>
      <Field label={t('files.or_paste')} testId="insert-pmap-content">
        {(control) => <textarea {...control} className="field-control file-content" rows={6} spellCheck={false} value={content} placeholder={t('files.area')}
          onChange={(event) => { onText(event.target.value); }} onDragOver={(event) => { event.preventDefault(); }} onDrop={drop} />}
      </Field>
    </>
  );
}

/** What the file holds, its levels against the room the map leaves, the languages left out, the points to check. */
export function PreviewSummary({ preview }: { preview: InsertionPreview }) {
  const { t } = useTranslation();
  return (
    <div className="insert-preview" data-testid="insert-pmap-preview">
      <p role="status">
        <strong>{preview.project_code === null ? preview.project_name : `${preview.project_name} (${preview.project_code})`}</strong>
        {' · '}
        {t('files.insert_counts', { steps: preview.steps, lanes: preview.lanes, flows: preview.flows, maps: preview.maps, sheets: preview.sheets })}
      </p>
      {preview.depth > preview.room && (
        <p className="message-error" role="alert" data-testid="insert-pmap-too-deep">{t('files.insert_too_deep', { depth: preview.depth, room: preview.room })}</p>
      )}
      {preview.dropped_languages.length > 0 && (
        <p className="field-hint" data-testid="insert-pmap-dropped">{t('files.insert_dropped', { languages: preview.dropped_languages.join(', ') })}</p>
      )}
      {preview.remote_locations > 0 && (
        <p className="message-warning" data-testid="insert-pmap-remote">{t('files.remote_locations', { count: preview.remote_locations })}</p>
      )}
      {preview.uncertainties.length > 0 && (
        <>
          <p className="dialog-subtitle">{t('files.uncertainties')}</p>
          <ul className="uncertainties">{preview.uncertainties.map((text, index) => <li key={index}>{text}</li>)}</ul>
        </>
      )}
    </div>
  );
}

/** The flow offered, by the names of its two ends; nothing for a flow no longer on the map. */
function FlowOption({ map, language, flowId }: { map: MapView; language: string; flowId: string }) {
  const { t } = useTranslation();
  const flow = map.flows.find((candidate) => candidate.id === flowId);
  const from = map.steps.find((step) => step.id === flow?.source_step_id);
  const to = map.steps.find((step) => step.id === flow?.target_step_id);
  if (from === undefined || to === undefined) return null;
  return <option value={flowChoice(flowId)}>{t('files.insert_on_flow', { from: stepName(from, language), to: stepName(to, language) })}</option>;
}

/** The lane of the new step and the step it follows, in the order of the map (the start of the map first); the flow chosen on the map first, when one was. */
export function PlaceFields({ map, language, place, offeredFlow, onChange }: {
  map: MapView; language: string; place: InsertionPlace; offeredFlow: string | null; onChange: (place: InsertionPlace) => void;
}) {
  const { t } = useTranslation();
  const steps = [...map.steps].sort(readingOrder(laneRanks(map.lanes)));
  return (
    <div className="form-row">
      <Field label={t('files.insert_lane')} testId="insert-pmap-lane">
        {(control) => (
          <select {...control} value={place.laneId} onChange={(event) => { onChange({ ...place, laneId: event.target.value }); }}>
            {lanesInOrder(map.lanes).map((id) => {
              const lane = map.lanes.find((candidate) => candidate.id === id);
              return lane === undefined ? null : (
                <option key={id} value={id}>{localizedName({ name: lane.name, source_language: lane.source_language, name_translations: lane.translations }, language)}</option>
              );
            })}
          </select>
        )}
      </Field>
      <Field label={t('files.insert_after')} testId="insert-pmap-after">
        {(control) => (
          <select {...control} value={place.flow === null ? place.after : flowChoice(place.flow)} onChange={(event) => { onChange(followed(map, place, event.target.value)); }}>
            {offeredFlow !== null && <FlowOption map={map} language={language} flowId={offeredFlow} />}
            <option value={AT_START}>{t('files.insert_at_start')}</option>
            {steps.filter(mayFollow).map((step) => <option key={step.id} value={step.id}>{`${step.code} · ${stepName(step, language)}`}</option>)}
          </select>
        )}
      </Field>
    </div>
  );
}

/** A refusal of the host or of the reading of the file, with the faults of the file it lists (A-1.2-19). */
export function InsertionErrorBox({ error }: { error: InsertionError }) {
  return (
    <div className="message-error" role="alert" data-testid="insert-pmap-error">
      <p>{error.message}</p>
      {error.faults.length > 0 && (
        <ul className="file-faults">{error.faults.map((fault, index) => <li key={index}>{fault.path !== '' && <code>{fault.path}</code>} {fault.message}</li>)}</ul>
      )}
    </div>
  );
}
