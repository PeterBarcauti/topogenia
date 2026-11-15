// WallPrintDialog.tsx
// Prints the map shown large for a wall (Makigami M9): the paper A4 or A3 landscape, the pages across from 1 to 10 (the fewest that print it at least at its size on the screen offered first), the count of pages, the size of the poster and its scale said before anything is made, the marks of the layer « Analysis » on option (ticked when the layer is on); a poster of too many pages is refused before the PDF is made.

import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { ACROSS_MAX, ACROSS_MIN, defaultAcross, PAPERS, WALL_OVERLAP, WALL_PAGES_MAX, wallPlan, type Paper } from '../export/wallPrint.ts';
import { exportMap, wallSize } from '../state/actions/exports.ts';
import { useEditor } from '../state/EditorContext.tsx';

const PAPER_CODES = Object.keys(PAPERS) as Paper[];
const ACROSS = Array.from({ length: ACROSS_MAX - ACROSS_MIN + 1 }, (_, index) => ACROSS_MIN + index);

export function WallPrintDialog({ map, onClose }: { map: Schema<'MapView'>; onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const editor = useEditor();
  const size = useMemo(() => wallSize(editor, map), [editor, map]);
  const [paper, setPaper] = useState<Paper>('a4');
  const [across, setAcross] = useState(() => defaultAcross(size, 'a4'));
  const [analysis, setAnalysis] = useState(() => editor.preferences.getState().showAnalysis);
  const plan = useMemo(() => wallPlan(size, paper, across), [size, paper, across]);
  const centimetres = (millimetres: number) => new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 0 }).format(millimetres / 10);

  const submit = () => {
    onClose();
    void exportMap(editor, 'wall', { wall: { paper, across, analysis } });
  };

  const footer = (
    <>
      <Button testId="wall-print-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="wall-print-confirm" variant="primary" disabled={plan.tooMany} onClick={submit}>{t('export.wall_button')}</Button>
    </>
  );
  return (
    <Dialog title={t('export.wall_title')} testId="wall-print" onClose={onClose} footer={footer}>
      <p className="muted">{t('export.wall_help', { overlap: WALL_OVERLAP })}</p>
      <fieldset className="wall-paper" data-testid="wall-print-paper">
        <legend className="field-label">{t('export.wall_paper')}</legend>
        {PAPER_CODES.map((code) => (
          <label key={code} className="check">
            <input type="radio" name="wall-paper" value={code} checked={paper === code} data-testid={`wall-print-paper-${code}`} onChange={() => { setPaper(code); }} />
            <span>{t(`export.wall_paper_${code}`)}</span>
          </label>
        ))}
      </fieldset>
      <Field label={t('export.wall_across')} testId="wall-print-across" error={plan.tooMany ? t('export.wall_too_many', { max: WALL_PAGES_MAX }) : undefined}>
        {(control) => (
          <select {...control} value={across} onChange={(event) => { setAcross(Number(event.target.value)); }}>
            {ACROSS.map((count) => <option key={count} value={count}>{count}</option>)}
          </select>
        )}
      </Field>
      <p className="dialog-note" data-testid="wall-print-summary">
        {t('export.wall_summary', { count: plan.across * plan.down, across: plan.across, down: plan.down, width: centimetres(plan.wall.width), height: centimetres(plan.wall.height), percent: plan.percent })}
      </p>
      <label className="check">
        <input type="checkbox" checked={analysis} data-testid="wall-print-analysis" onChange={(event) => { setAnalysis(event.target.checked); }} />
        <span>{t('export.wall_analysis')}</span>
      </label>
    </Dialog>
  );
}
