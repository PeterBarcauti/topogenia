// RaciMatrixDialog.tsx
// The RACI matrix of the map in a wide dialog: its steps concerned in rows, its lanes in columns, the legend of the letters, the copy for Excel or PowerPoint (annex B 2.11).

import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { localizedName } from '../../../shared/i18n/localizedName.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { laneName, stepLabel } from '../model/mapLayout.ts';
import { raciLetters, raciMatrix } from '../model/raci.ts';
import { copyMatrix } from '../state/actions/raci.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';

type MapView = Schema<'MapView'>;

/** « R : réalise · A : approuve · C : consulté · I : informé », the letters of the map (B-2.11-18). */
function useLegend(map: MapView): string {
  const { t, i18n } = useTranslation();
  return ['R', ...raciLetters(map)].map((letter) => t('raci.legend_item', { letter, role: t(`raci.names.${letter}`).toLocaleLowerCase(i18n.language) })).join(' · ');
}

export function RaciMatrixDialog({ map, onClose }: { map: MapView; onClose: () => void }) {
  const { t } = useTranslation();
  const language = useMapLanguage();
  const editor = useEditor();
  const legend = useLegend(map);
  const matrix = raciMatrix(map, (lane) => laneName(lane, language), (step) => stepLabel(step, language));
  const headers = { code: t('raci.column_code'), step: t('raci.column_step') };
  const name = localizedName({ name: map.name, source_language: map.source_language, name_translations: map.translations }, language);
  const copy = () => {
    void copyMatrix(matrix, headers).then((copied) => {
      if (copied) editor.notify(t('raci.copied'), 'success');
    });
  };
  const footer = (
    <>
      <Button testId="raci-matrix-cancel" variant="secondary" onClick={onClose}>{t('app.close')}</Button>
      <Button testId="raci-matrix-copy" variant="primary" disabled={matrix.rows.length === 0} onClick={copy}>{t('raci.copy')}</Button>
    </>
  );
  return (
    <Dialog title={t('raci.matrix_title', { map: name })} testId="raci-matrix" wide onClose={onClose} footer={footer}>
      {matrix.rows.length === 0 ? <p className="raci-matrix-empty">{t('raci.matrix_empty')}</p> : (
        <div className="raci-matrix-scroll">
          <table className="raci-matrix">
            <thead>
              <tr>
                <th scope="col">{headers.code}</th>
                <th scope="col">{headers.step}</th>
                {matrix.columns.map((column) => <th key={column.id} scope="col">{column.name}</th>)}
              </tr>
            </thead>
            <tbody>
              {matrix.rows.map((row) => (
                <tr key={row.id}>
                  <td className="raci-matrix-code">{row.code}</td>
                  <td>{row.label}</td>
                  {row.cells.map((letter, index) => <td key={matrix.columns[index]?.id ?? index} className={letter === '' ? 'raci-cell' : `raci-cell raci-cell--${letter}`}>{letter}</td>)}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="raci-matrix-legend" data-testid="raci-matrix-legend">{legend}</p>
    </Dialog>
  );
}
