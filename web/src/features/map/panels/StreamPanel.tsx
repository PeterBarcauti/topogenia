// StreamPanel.tsx
// The panel of the value stream docked on the right, with its layer on (spec 3.0 section 15.3, ADR 086): the summary in tiles and its base, the main path a row a step, the completeness check grouped by code; each step and each object leads to itself on the map; closing it turns the layer off.

import { useMemo, type ReactNode } from 'react';
import { CircleAlert, Info, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { paths } from '../../../app/router.ts';
import { FoldButton } from '../../../shared/ui/FoldButton.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { streamPanelOf, type CheckGroup, type CheckSubject, type PathFigure, type StreamTile } from '../model/streamPanel.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';

type MapView = Schema<'MapView'>;

/** Where an object leads: on the map shown, its steps chosen and put in view (a lane by its steps); in a detailed map, that map opened. */
function useLeadTo(map: MapView): (subject: Pick<CheckSubject, 'mapId' | 'stepId' | 'laneId'>) => void {
  const editor = useEditor();
  return (subject) => {
    if (subject.mapId !== map.id) {
      editor.navigate(paths.map(subject.mapId));
      return;
    }
    const steps = subject.stepId !== null ? [subject.stepId] : subject.laneId !== null ? map.steps.filter((step) => step.lane_id === subject.laneId).map((step) => step.id) : [];
    if (steps.length === 0) return;
    editor.store.setState({ selection: { steps, flows: [] } });
    editor.canvas.reveal(steps);
  };
}

function Section({ title, aside, children }: { title: string; aside?: ReactNode; children: ReactNode }) {
  return (
    <section className="stream-section">
      <h3>{title}{aside}</h3>
      {children}
    </section>
  );
}

function Tiles({ tiles, basis }: { tiles: readonly StreamTile[]; basis: string }) {
  const { t } = useTranslation();
  return (
    <>
      <dl className="stream-tiles">
        {tiles.map((tile) => (
          <div key={tile.key} className="stream-tile" title={t(`stream.panel.hints.${tile.key}`)} data-testid={`stream-tile-${tile.key}`}>
            <dt>{t(`stream.panel.tiles.${tile.key}`)}</dt>
            <dd className="stream-tile-value">{tile.value}</dd>
            {tile.note !== null && <dd className="stream-tile-note">{tile.note}</dd>}
          </div>
        ))}
      </dl>
      <p className="stream-basis" data-testid="stream-basis">{basis}</p>
    </>
  );
}

function MainPath({ path, onStep }: { path: PathFigure | null; onStep: (stepId: string) => void }) {
  const { t } = useTranslation();
  if (path === null) return <p className="stream-none">{t('stream.timeline.none')}</p>;
  return (
    <>
      <dl className="stream-path-totals" data-testid="stream-path-totals">
        <div><dt>{t('stream.timeline.lead')}</dt><dd>{path.lead}</dd></div>
        <div><dt>{t('stream.timeline.work')}</dt><dd>{path.work}</dd></div>
        <div><dt>{t('stream.panel.ratio')}</dt><dd>{path.ratio}</dd></div>
      </dl>
      <table className="stream-path" data-testid="stream-path">
        <thead>
          <tr><th scope="col">{t('stream.panel.step')}</th><th scope="col">{t('stream.timeline.lead')}</th><th scope="col">{t('stream.timeline.work')}</th></tr>
        </thead>
        <tbody>
          {path.rows.map((row) => (
            <tr key={row.stepId} data-testid={`stream-row-${row.code}`}>
              <td>
                <button type="button" className="stream-step" title={`${row.code} ${row.label}`} onClick={() => { onStep(row.stepId); }} data-testid={`stream-step-${row.code}`}>
                  <code>{row.code}</code><span>{row.label}</span>
                </button>
              </td>
              <td>{row.lead}</td>
              <td>{row.work}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}

function Checks({ groups, leadTo }: { groups: readonly CheckGroup[]; leadTo: ReturnType<typeof useLeadTo> }) {
  const { t } = useTranslation();
  if (groups.length === 0) return <p className="stream-check-none" data-testid="stream-check-none">{t('stream.check.none')}</p>;
  return (
    <ul className="stream-checks" data-testid="stream-checks">
      {groups.map((group) => (
        <li key={group.code} className={`stream-group is-${group.severity}`} data-testid={`stream-check-${group.code}`}>
          <div className="stream-group-head" title={`${t(`stream.check.${group.severity}`)} · ${group.code}`}>
            <Icon icon={group.severity === 'warning' ? CircleAlert : Info} size={14} />
            <span>{group.label}</span>
            <span className="stream-group-count">{group.subjects.length}</span>
          </div>
          <div className="stream-chips">
            {group.subjects.map((subject, index) => (
              <button key={`${subject.code}:${String(index)}`} type="button" className="stream-chip" title={subject.name} onClick={() => { leadTo(subject); }}
                data-testid={`stream-chip-${group.code}-${subject.code}`}>
                {subject.code === '' ? subject.name : subject.code}
              </button>
            ))}
          </div>
        </li>
      ))}
    </ul>
  );
}

export function StreamPanel({ map }: { map: MapView }) {
  const { t, i18n } = useTranslation();
  const editor = useEditor();
  const shown = useMapLanguage();
  const workday = map.value_stream.work_time.workday_hours;
  const figure = useMemo(() => streamPanelOf(map, shown, { language: i18n.language, workdayHours: workday, t }), [map, shown, i18n.language, workday, t]);
  const leadTo = useLeadTo(map);
  const title = t('ribbon.value_stream');
  return (
    <aside className="side-panel stream-panel" aria-label={title} data-testid="stream-panel">
      <header className="side-panel-head">
        <div className="stream-panel-title">
          <h2>{title}</h2>
          <p className="stream-quantified" data-testid="stream-quantified">{figure.quantified}</p>
        </div>
        <FoldButton />
        <button type="button" className="icon-button" aria-label={t('app.close')} title={t('app.close')} data-testid="stream-panel-close"
          onClick={() => { editor.preferences.getState().set('showValueStream', false); }}>
          <Icon icon={X} size={16} />
        </button>
      </header>
      <div className="side-panel-body">
        <Section title={t('stream.panel.summary')}><Tiles tiles={figure.tiles} basis={figure.basis} /></Section>
        <Section title={t('stream.timeline.title')}><MainPath path={figure.path} onStep={(stepId) => { leadTo({ mapId: map.id, stepId, laneId: null }); }} /></Section>
        <Section title={t('stream.check.title')}><Checks groups={figure.checks} leadTo={leadTo} /></Section>
      </div>
    </aside>
  );
}
