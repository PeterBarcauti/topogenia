// ProjectPdfDialog.tsx
// Exports the project as one PDF (ADR 113): the scope (the whole project, or the map shown and its levels on a detailed map), four boxes (sheets, RACI matrix, value stream, action plan) that start as the display says and are never remembered, the progress and Cancel while the task runs, the refusals (a bound passed, another export running) said here; Escape and the closing button cancel the task.

import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import type { ProjectPdfChoice } from '../export/project/projectPdf.ts';
import { branchOffered } from '../export/project/projectWalk.ts';
import { exportProjectPdf, type ProjectPdfScope, type ProjectPdfStatus } from '../state/actions/projectPdf.ts';
import { useEditor } from '../state/EditorContext.tsx';
import type { MapEditor } from '../state/editor.ts';

/** The boxes as the display says: sheets and plan ticked (the aim is to hand the project over), RACI and value stream as the map shows them. */
const startingChoice = (editor: MapEditor): ProjectPdfChoice => {
  const { showRaci, showValueStream } = editor.preferences.getState();
  return { sheets: true, raci: showRaci, stream: showValueStream, plan: true };
};

const BOXES = [
  { key: 'sheets', label: 'project_pdf.with_sheets' },
  { key: 'raci', label: 'project_pdf.with_raci' },
  { key: 'stream', label: 'project_pdf.with_stream' },
  { key: 'plan', label: 'project_pdf.with_plan' },
] as const;

/** The four boxes, the warning on the sheets under theirs and tied to it for a screen reader. */
function ProjectPdfBoxes({ choice, disabled, onChange }: { choice: ProjectPdfChoice; disabled: boolean; onChange: (key: keyof ProjectPdfChoice, ticked: boolean) => void }) {
  const { t } = useTranslation();
  const helpId = useId();
  return (
    <div className="project-pdf-boxes" data-testid="project-pdf-boxes">
      {BOXES.map(({ key, label }) => (
        <div key={key}>
          <label className="check">
            <input type="checkbox" checked={choice[key]} disabled={disabled} data-testid={`project-pdf-${key}`} aria-describedby={key === 'sheets' ? helpId : undefined}
              onChange={(event) => { onChange(key, event.target.checked); }} />
            <span>{t(label)}</span>
          </label>
          {key === 'sheets' && <p id={helpId} className="field-hint">{t('project_pdf.sheets_help')}</p>}
        </div>
      ))}
    </div>
  );
}

export function ProjectPdfDialog({ map, onClose }: { map: Schema<'MapView'>; onClose: () => void }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const [scope, setScope] = useState<ProjectPdfScope>('project');
  const [choice, setChoice] = useState(() => startingChoice(editor));
  const [status, setStatus] = useState<ProjectPdfStatus | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [task, setTask] = useState<AbortController | null>(null);
  const alive = useRef(true);
  const body = useRef<HTMLDivElement>(null);
  const running = status !== null;
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  useEffect(() => () => { task?.abort(); }, [task]); // the task gives its own way to cancel it, kept in the state

  // The button that started the task goes: the focus goes to Cancel, so that Escape and Tab stay in the dialog.
  useEffect(() => {
    if (running) body.current?.closest('[role="dialog"]')?.querySelector<HTMLElement>('[data-testid="project-pdf-cancel"]')?.focus();
  }, [running]);

  const close = () => {
    task?.abort();
    onClose();
  };
  const submit = () => {
    const controller = new AbortController();
    setTask(controller);
    setRefusal(null);
    setStatus({ text: '', value: null, max: null });
    const progress = (next: ProjectPdfStatus) => {
      if (alive.current) setStatus(next);
    };
    void exportProjectPdf(editor, { scope, choice }, { signal: controller.signal, progress }).then((outcome) => {
      if (!alive.current) return;
      if (outcome.kind === 'refused') {
        setStatus(null);
        setRefusal(outcome.message);
      } else onClose();
    }, onClose);
  };
  const change = (key: keyof ProjectPdfChoice, ticked: boolean) => {
    setRefusal(null);
    setChoice({ ...choice, [key]: ticked });
  };

  const footer = (
    <>
      <Button testId="project-pdf-cancel" variant="secondary" onClick={close}>{t('app.cancel')}</Button>
      {!running && <Button testId="project-pdf-confirm" variant="primary" onClick={submit}>{t('project_pdf.button')}</Button>}
    </>
  );
  const scopes = branchOffered(map) ? (['project', 'branch'] as const) : (['project'] as const);
  return (
    <Dialog title={t('project_pdf.title')} testId="project-pdf" onClose={close} footer={footer}>
      <p ref={body} className="muted">{t('project_pdf.help')}</p>
      <fieldset className="wall-paper" data-testid="project-pdf-scope">
        <legend className="field-label">{t('project_pdf.scope')}</legend>
        {scopes.map((code) => (
          <label key={code} className="check">
            <input type="radio" name="project-pdf-scope" value={code} checked={scope === code} disabled={running} data-testid={`project-pdf-scope-${code}`} onChange={() => { setScope(code); }} />
            <span>{t(`project_pdf.scope_${code}`)}</span>
          </label>
        ))}
      </fieldset>
      <ProjectPdfBoxes choice={choice} disabled={running} onChange={change} />
      {refusal !== null && <p className="field-error" role="alert" data-testid="project-pdf-refusal">{refusal}</p>}
      {status !== null && (
        <div data-testid="project-pdf-progress">
          <progress aria-label={t('project_pdf.progress')} value={status.value ?? undefined} max={status.max ?? undefined} />
          <p className="muted" role="status">{status.text}</p>
        </div>
      )}
    </Dialog>
  );
}
