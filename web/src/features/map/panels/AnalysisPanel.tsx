// AnalysisPanel.tsx
// The panel of the layer « Analysis » docked on the right (Makigami, plan 3.1): its tiles, each one putting its objects forward on the map while hovered or held by a click, the rest faded; the lead time of a case in work and waits; the places where the team can act, each leading to its step; on the main map, the photos and the target state (P3e); closing it turns the layer off.

import { useEffect, useMemo, useState } from 'react';
import { ArrowRightLeft, Clock, Replace, Trash2, X, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { FoldButton } from '../../../shared/ui/FoldButton.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { analysisTilesOf, type AnalysisTile } from '../model/analysis.ts';
import { actionRowsOf, leadStoryOf, type ActionRow, type LeadSplit, type LeadStory, type PlaceBadge } from '../model/analysisPanel.ts';
import { useEditor, useMapLanguage, useMapState } from '../state/EditorContext.tsx';
import type { AnalysisFocus } from '../state/mapStore.ts';
import { useLeadTo } from './useLeadTo.ts';
import { SnapshotsSection } from './SnapshotsSection.tsx';

type MapView = Schema<'MapView'>;

/** The places shown before « See all »: the three where a case loses the most. */
const FIRST_PLACES = 3;

/** A mark in the text of a translation where a part in bold goes; it never occurs in a translation. */
const STRONG = '\u0000';

/** A tile that puts its objects forward on the map: while hovered or focused, or held by a click until a second one. */
function FocusTile({ tile }: { tile: AnalysisTile }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const focus = useMapState((state) => state.analysisFocus);
  const held = focus?.held === true && focus.tile === tile.key;
  const set = (next: AnalysisFocus | null) => {
    editor.store.setState({ analysisFocus: next });
  };
  const show = () => {
    if (focus?.held !== true) set({ tile: tile.key, held: false });
  };
  const hide = () => {
    if (focus?.held !== true) set(null);
  };
  return (
    <button type="button" className="stream-tile analysis-tile" aria-pressed={held} title={t(`analysis.${tile.key}.help`)} data-testid={`analysis-tile-${tile.key}`}
      onMouseEnter={show} onMouseLeave={hide} onFocus={show} onBlur={hide} onClick={() => { set(held ? null : { tile: tile.key, held: true }); }}>
      <span className="analysis-tile-name">{t(`analysis.${tile.key}.title`)}</span>
      <span className="stream-tile-value" data-testid={`analysis-value-${tile.key}`}>{tile.value}</span>
      <span className="stream-tile-note" data-testid={`analysis-note-${tile.key}`}>{tile.note}</span>
    </button>
  );
}

/** The parts of the lead time: a bar of the three with their whole percents, then one sentence, the wait after a handoff in bold. */
function Split({ lead, split }: { lead: string; split: LeadSplit }) {
  const { t, i18n } = useTranslation();
  const percent = new Intl.NumberFormat(i18n.language, { style: 'percent', maximumFractionDigits: 0 });
  const parts = (['work', 'handoff', 'other'] as const).map((key, index) => ({ key, value: split.percents[index] ?? 0 }));
  const legend = (part: (typeof parts)[number]) => t(`analysis.story.legend.${part.key}`, { value: percent.format(part.value / 100) });
  const [before = '', after = ''] = split.handoff === null || split.wait === null ? [] : t('analysis.story.waits_handoff', { wait: split.wait, lead, handoff: STRONG }).split(STRONG);
  return (
    <>
      <div className="analysis-bar" role="img" aria-label={parts.map(legend).join(', ')}>
        {parts.map((part) => <span key={part.key} className={`is-${part.key}`} style={{ width: `${String(part.value)}%` }} />)}
      </div>
      <ul className="analysis-legend" data-testid="analysis-legend">
        {parts.map((part) => <li key={part.key} className={`is-${part.key}`}>{legend(part)}</li>)}
      </ul>
      <p className="analysis-say" data-testid="analysis-say">
        {split.wait === null ? t('analysis.story.no_wait')
          : split.handoff === null ? t('analysis.story.waits', { wait: split.wait, lead })
            : <>{before}<strong>{t('analysis.story.after_handoff', { value: split.handoff })}</strong>{after}</>}
      </p>
    </>
  );
}

/** The lead time of a case: its total and its work, its parts or why they are not known yet, the lead times entered. */
function Story({ story }: { story: LeadStory }) {
  const { t } = useTranslation();
  return (
    <>
      <dl className="stream-path-totals" data-testid="analysis-lead">
        <div><dt>{t('analysis.story.lead')}</dt><dd>{story.lead}</dd></div>
        {story.split !== null && <div><dt>{t('analysis.story.work')}</dt><dd>{story.split.work}</dd></div>}
      </dl>
      {story.split === null
        ? <p className="analysis-ask" data-testid="analysis-split-unknown">{t('analysis.story.split_unknown')}</p>
        : <Split lead={story.lead} split={story.split} />}
      {story.entered !== null && <p className="stream-basis" data-testid="analysis-entered">{t('analysis.story.entered', { entered: story.entered.count, total: story.entered.total })}</p>}
    </>
  );
}

const BADGES: Readonly<Record<Exclude<PlaceBadge, 'detail'>, LucideIcon>> = { handoff: ArrowRightLeft, break: Replace, waste: Trash2, wait: Clock };

/** A place: its mark, its step, the time lost there (by passage said so in a list by case), its causes; a click leads to the step. */
function Place({ row, byCase, onLead }: { row: ActionRow; byCase: boolean; onLead: () => void }) {
  const { t } = useTranslation();
  const lost = row.lost !== null && byCase && !row.perCase ? t('analysis.places.lost_per_passage', { value: row.lost }) : row.lost;
  return (
    <li>
      <button type="button" className="analysis-place" onClick={onLead} data-testid={`analysis-place-${row.code}`}>
        <span className="analysis-place-head">
          <span className={`analysis-badge is-${row.badge}`} title={t(`analysis.badge.${row.badge}`)}>
            {row.badge === 'detail' ? row.count : <Icon icon={BADGES[row.badge]} size={11} />}
          </span>
          <code>{row.code}</code>
          <span className="analysis-place-name">{row.name}</span>
          <span className={row.lost === null ? 'analysis-lost is-unknown' : 'analysis-lost'} title={row.lost === null ? t('analysis.places.unknown') : undefined}>
            {lost ?? '?'}
          </span>
        </span>
        {row.causes.length > 0 && (
          <span className="analysis-causes">
            {row.causes.map((cause, index) => <span key={`${cause.kind}:${String(index)}`} className={`is-${cause.kind}`}>{cause.text}</span>)}
          </span>
        )}
      </button>
    </li>
  );
}

/** Whether the list tells the time lost by case (F-106): as soon as one place knows it, the cases being known. */
const byCase = (rows: readonly ActionRow[]): boolean => rows.some((row) => row.perCase);

/** The places where the team can act, the three first then all on demand. */
function Places({ map, rows }: { map: MapView; rows: readonly ActionRow[] }) {
  const { t } = useTranslation();
  const leadTo = useLeadTo(map);
  const [all, setAll] = useState(false);
  if (rows.length === 0) return <p className="stream-none" data-testid="analysis-places-none">{t('analysis.places.none')}</p>;
  return (
    <>
      <ul className="analysis-places">
        {rows.slice(0, all ? rows.length : FIRST_PLACES).map((row) => (
          <Place key={row.stepId} row={row} byCase={byCase(rows)} onLead={() => { leadTo({ mapId: map.id, stepId: row.stepId, laneId: null }); }} />
        ))}
      </ul>
      {rows.length > FIRST_PLACES && (
        <button type="button" className="link-button analysis-more" data-testid="analysis-places-more" onClick={() => { setAll(!all); }}>
          {all ? t('analysis.places.less') : t('analysis.places.more', { total: rows.length })}
        </button>
      )}
    </>
  );
}

export function AnalysisPanel({ map }: { map: MapView }) {
  const { t, i18n } = useTranslation();
  const editor = useEditor();
  const shown = useMapLanguage();
  const stream = map.value_stream;
  const writing = useMemo(() => ({ language: i18n.language, workdayHours: stream.work_time.workday_hours, t }), [i18n.language, stream.work_time.workday_hours, t]);
  const tiles = useMemo(() => analysisTilesOf(stream, t, i18n.language), [stream, t, i18n.language]);
  const story = useMemo(() => leadStoryOf(stream, writing), [stream, writing]);
  const rows = useMemo(() => actionRowsOf(map, writing, shown), [map, writing, shown]);
  // The focus does not outlive its panel: the map is drawn plain again once the panel leaves.
  useEffect(() => () => { editor.store.setState({ analysisFocus: null }); }, [editor]);
  const title = t('ribbon.analysis');
  return (
    <aside className="side-panel stream-panel analysis-panel" aria-label={title} data-testid="analysis-panel">
      <header className="side-panel-head">
        <div className="stream-panel-title"><h2>{title}</h2></div>
        <FoldButton />
        <button type="button" className="icon-button" aria-label={t('app.close')} title={t('app.close')} data-testid="analysis-panel-close"
          onClick={() => { editor.preferences.getState().set('showAnalysis', false); }}>
          <Icon icon={X} size={16} />
        </button>
      </header>
      <div className="side-panel-body">
        <div>
          <div className="stream-tiles analysis-tiles">
            {tiles.map((tile) => <FocusTile key={tile.key} tile={tile} />)}
          </div>
          <p className="stream-basis">{t('analysis.focus_help')}</p>
        </div>
        <section className="stream-section" data-testid="analysis-story">
          <h3>{t('analysis.story.title')}</h3>
          {story === null ? <p className="analysis-ask" data-testid="analysis-ask">{t('analysis.story.unknown')}</p> : <Story story={story} />}
        </section>
        <section className="stream-section" data-testid="analysis-act">
          <h3>{t('analysis.places.title')}{rows.length > 0 && <span className="analysis-aside" data-testid="analysis-places-measure">{t(byCase(rows) ? 'analysis.places.per_case' : 'analysis.places.per_passage')}</span>}</h3>
          <Places map={map} rows={rows} />
        </section>
        {map.parent_step_id === null && <SnapshotsSection map={map} writing={writing} />}
      </div>
    </aside>
  );
}
