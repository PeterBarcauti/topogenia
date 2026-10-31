// LaneHeaders.tsx
// The fixed margin of the lane headers: pastel, ink stripe, type icon, name, code; renamed in place, reordered by a drag, a lane added between two, its menu (annex B 2.12, spec 9.3).

import type { CSSProperties, MouseEvent } from 'react';
import { Ellipsis, Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { View } from '../model/geometry/viewport.ts';
import type { LaneBand } from '../model/mapLayout.ts';
import { LabelInput } from '../nodes/LabelInput.tsx';
import { ownName } from '../../../shared/i18n/languageNames.ts';
import { addLane, LANE_NAME_MAX, orderLanes } from '../state/actions/lanes.ts';
import { laneTyped, renameLane } from '../state/actions/rename.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
import { moveInList, useLaneDrag } from './laneDrag.ts';
import { LaneStream } from './LaneStream.tsx';
import { LANE_ICONS } from './shapeIcons.ts';

interface HeadProps {
  readonly band: LaneBand;
  readonly style: CSSProperties;
  readonly className: string;
  readonly showCodes: boolean;
  readonly canEdit: boolean;
  readonly drag: ReturnType<typeof useLaneDrag>;
  readonly rank: number;
}

/** One header: double click or menu renames it (B-2.12-11, B-2.12-12); a drag reorders (B-2.12-14); « ⋯ » or a right click opens its menu (B-2.12-24, B-2.4-45). */
function LaneHead({ band, style, className, showCodes, canEdit, drag, rank }: HeadProps) {
  const { t } = useTranslation();
  const editor = useEditor();
  const editing = useMapState((state) => state.editing === `lane:${band.lane.id}`);
  // A facilitator in another language than the one of origin types the translation of the name (ADR 083).
  const translating = useMapState((state) => state.translating);
  const shown = useMapState((state) => state.shown?.language ?? null);
  const type = t(`map.lane_types.${band.lane.type}`);
  const openMenu = (event: MouseEvent, at: { x: number; y: number }) => {
    event.preventDefault();
    if (canEdit) editor.floating.getState().open({ kind: 'menu', target: { kind: 'lane', laneId: band.lane.id, at }, at });
  };
  const stop = () => {
    editor.store.setState({ editing: null });
  };
  return (
    <div className={className} role="group" aria-label={t('map.lane', { name: band.name, code: band.lane.code, type })} title={type} style={style}
      data-testid={`lane-head-${band.lane.code}`}
      onPointerDown={(event) => {
        if (canEdit && !editing && !(event.target instanceof Element && event.target.closest('button') !== null)) drag.start(event, band.lane.id, rank);
      }}
      onPointerMove={drag.move} onPointerUp={drag.end} onPointerCancel={drag.cancel}
      onDoubleClick={() => {
        if (canEdit || translating) editor.store.setState({ editing: `lane:${band.lane.id}` });
      }}
      onContextMenu={(event) => {
        openMenu(event, { x: event.clientX, y: event.clientY });
      }}>
      {editing ? (
        <LabelInput initial={laneTyped(editor, band.lane)} label={t('map.rename')} max={LANE_NAME_MAX} testId="lane-name-input"
          placeholder={band.untranslated ? band.lane.name : undefined} onCancel={stop}
          onDone={(name) => {
            stop();
            void renameLane(editor, band.lane, name);
          }} />
      ) : (
        <div className="lane-head-name">
          <Icon icon={LANE_ICONS[band.lane.type]} />
          {band.untranslated && shown !== null
            ? <span className="untranslated" title={t('process_language.untranslated', { language: ownName(shown) })}>{band.name}</span>
            : <span>{band.name}</span>}
        </div>
      )}
      {showCodes && !editing && <code className="lane-head-code" data-testid="lane-head-code">{band.lane.code}</code>}
      {band.stream !== null && !editing && <LaneStream stream={band.stream} code={band.lane.code} />}
      {canEdit && !editing && (
        <button type="button" className="lane-more" aria-label={t('map.lane_actions', { name: band.name })} title={t('map.lane_actions', { name: band.name })}
          aria-haspopup="menu" data-testid={`lane-more-${band.lane.code}`}
          onClick={(event) => {
            const box = event.currentTarget.getBoundingClientRect();
            openMenu(event, { x: box.left, y: box.bottom + 4 });
          }}>
          <Icon icon={Ellipsis} size={15} />
        </button>
      )}
    </div>
  );
}

/** The round « + » above the first lane, between two lanes and under the last one, shown on hover (B-2.12-20). */
function AddLaneButtons({ tops, bottom }: { tops: readonly number[]; bottom: number }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const places = [...tops.map((top, order) => ({ order, top })), { order: tops.length, top: bottom }];
  return places.map(({ order, top }) => (
    <button key={order} type="button" className="lane-add" style={{ top }} aria-label={t('map.add_lane')} title={t('map.add_lane')} data-testid={`lane-add-${String(order)}`}
      onClick={() => void addLane(editor, order)}>
      <Icon icon={Plus} size={12} strokeWidth={2.5} />
    </button>
  ));
}

/**
 * One header a lane, at the height of its band on the screen: top and height follow the view and its zoom, the width
 * stays the margin of 184 px (B-2.12-01 to B-2.12-04, B-2.12-10).
 */
export function LaneHeaders({ lanes, view, showCodes }: { lanes: readonly LaneBand[]; view: View; showCodes: boolean }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const canEdit = useMapState((state) => state.map?.rights.structure === true);
  const tops = lanes.map((band) => band.top * view.zoom);
  const heights = lanes.map((band) => band.height * view.zoom);
  const drag = useLaneDrag(tops, heights, (from, to) => void orderLanes(editor, moveInList(lanes.map((band) => band.lane.id), from, to)));
  const bottom = lanes.reduce((sum, band) => sum + band.height, 0) * view.zoom;
  return (
    <div className="lane-heads" role="group" aria-label={t('map.lane_headers')} data-testid="lane-headers">
      {lanes.map((band, rank) => {
        const moving = drag.dragging === band.lane.id;
        const style = {
          top: view.y + (tops[rank] ?? 0) + (moving ? drag.dy : 0), height: heights[rank],
          '--pastel': `var(--lane-${String(band.tint)}-pastel)`, '--ink': `var(--lane-${String(band.tint)}-ink)`,
        } as CSSProperties;
        const className = ['lane-head', moving && 'is-dragging', !moving && drag.target === rank && 'is-drop-target'].filter(Boolean).join(' ');
        return <LaneHead key={band.lane.id} band={band} style={style} className={className} showCodes={showCodes} canEdit={canEdit} drag={drag} rank={rank} />;
      })}
      {canEdit && <AddLaneButtons tops={tops.map((top) => view.y + top)} bottom={view.y + bottom} />}
    </div>
  );
}
