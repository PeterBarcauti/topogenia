// constants.ts
// The geometry of the map, written once: grid, step, margins, view, aisles, bands, frame, flows (annex B 3.1, spec 3.5, AR-19).

/** Horizontal pitch of the columns (B-3.1-01). */
export const COLUMN_WIDTH = 220;

/** Height of one row of a lane (B-3.1-02). */
export const ROW_HEIGHT = 120;

/** Space before the column 0 (B-3.1-03). */
export const LEFT_MARGIN = 30;

/** Size of a step, whatever its type (B-3.1-04). */
export const STEP_WIDTH = 170;
export const STEP_HEIGHT = 64;

/** The fixed margin of the lane headers on the left (spec 9.3, AR-19; 1.x 160). */
export const LANE_HEADER_WIDTH = 184;

/** Columns shown after the last step, to drop a step there (B-3.1-06). */
export const FREE_COLUMNS = 3;

/** A drop in the lowest part of the last row of a lane, below this share of its height, makes a new row (B-3.1-07). */
export const NEW_ROW_THRESHOLD = 0.82;

/** Bounds of the view (B-3.1-08). */
export const TOP_MARGIN = 16;
export const BOTTOM_MARGIN = 24;
export const RIGHT_MARGIN = 40;

/** The view at the opening: the first column right of the lane headers, the first lane under the ribbon (B-3.1-09, AR-19). */
export const INITIAL_VIEW = { x: LANE_HEADER_WIDTH + 8, y: TOP_MARGIN, zoom: 1 } as const;

/** Half of the space between two columns, where the vertical segments run (B-3.1-10). */
export const AISLE = (COLUMN_WIDTH - STEP_WIDTH) / 2;

/** Free band above and below the steps of a row (B-3.1-11). */
export const FREE_BAND = (ROW_HEIGHT - STEP_HEIGHT) / 2;

/** Margins of the frame of a dissolution around its steps, its label on top (B-3.1-12). */
export const FRAME_MARGIN = 14;
export const FRAME_MARGIN_TOP = 26;

/** Radius of the corners of a flow (B-3.4-18, B-3.4-28). */
export const CORNER_RADIUS = 10;

/** Depth of the control points of a loop under its steps, and the rise of its label (B-3.4-25, B-3.4-26). */
export const LOOP_DEPTH = 32;
export const LOOP_LABEL_RISE = 8;

/** Spacing of the tracks given to the segments that share a passage (spec 9.7, AR-24, AR-25). */
export const TRACK = 6;

/** The zoom of the canvas, from 0.2 to 2 (B-2.2-01). */
export const MIN_ZOOM = 0.2;
export const MAX_ZOOM = 2;

/** The dots of the background, every 24 px (B-2.1-47, AR-41). */
export const DOT_GAP = 24;

/** The mini map in the bottom right corner, and the space kept around it (B-2.1-45). */
export const MINIMAP_WIDTH = 200;
export const CANVAS_INSET = 16;

/** The zoom controls stand left of the mini map (B-2.1-41; 1.x `right: 220` written in the editor). */
export const CONTROLS_RIGHT = CANVAS_INSET + MINIMAP_WIDTH + 12;

/** The four handles of a step: dots of 10 px (B-3.3-03). */
export const HANDLE_SIZE = 10;

/** The round « + » of the quick add and between the lanes (B-2.3-41, B-2.12-20). */
export const QUICK_ADD_SIZE = 20;

/** The flows: stroke of 1.6, arrow head of 18 × 18, clickable band of 16 px (B-3.4-29, B-3.4-31, B-3.4-32). */
export const EDGE_STROKE = 1.6;
export const ARROW_SIZE = 18;
export const EDGE_HIT_WIDTH = 16;

/**
 * The label box of a flow: bordered white box of 110 px at most (B-3.4-35), text of 11 px (validated mockup), the document
 * icon and its count after the text (B-3.4-38). The routing places the box with the size measured from these values.
 */
export const EDGE_LABEL = {
  maxWidth: 110, fontSize: 11, fontWeight: 500, lineHeight: 14, paddingX: 6, paddingTop: 1, paddingBottom: 2, border: 1,
  iconSize: 11, iconGap: 3, documentsGap: 6,
} as const;

/** The label of a gateway written under its shape (AR-57): 13 px, two lines of 15 px at most, 200 px wide at most. */
export const GATEWAY_LABEL = { maxWidth: 200, fontSize: 13, fontWeight: 500, lineHeight: 15, lines: 2, paddingX: 4 } as const;

/** With the pictograms always shown, the label of a step moves up by 12 px so as not to run under them (B-2.10-14). */
export const PICTOS_RISE = 12;

/**
 * What hangs under a step: the label of a gateway 3 px under its shape (AR-57), the frequency of a detailed step 5 px
 * under it in a badge of 18 px (spec 9.4), then the RACI badges 3 px further (B-2.11-05); all of it 10 px aside of the
 * axis of the bottom point when a flow leaves or reaches it there (AR-58, AR-128).
 */
export const UNDER_STEP = { gatewayLabel: 3, frequencyTop: 5, frequencyHeight: 18, gap: 3, shift: 10 } as const;

/**
 * The value stream layer on (spec 14.5, ADR 085): each row gains a band of 36 px under its steps, where their cartouche
 * hangs (rows of 156 px, as the validated mockup), and the flows run under it: the tracks of the free band, 26 px at
 * most above its boundary, pass under a cartouche 2 px under its step. Off, no band: the geometry above.
 */
export const STREAM_BAND = 36;

/** The band under the steps of each row: the one of the value stream layer when it is on, else none. */
export const bandOf = (valueStream: boolean): number => (valueStream ? STREAM_BAND : 0);

/**
 * The cartouche of a step under the value stream layer: four cases of 75 x 18 px (spec 14.5, the validated mockup), 2 px under its step (under
 * the label of a gateway), centred on its axis, whose flow by the bottom point runs along the line between its cases.
 */
export const STREAM_CARD = { width: 150, height: 36, top: 2 } as const;

/** How far under its step the cartouche hangs: under the label of a gateway, of the height given, else 2 px under it. */
export const streamCardTop = (gatewayLabel: number | null): number =>
  gatewayLabel === null ? STREAM_CARD.top : UNDER_STEP.gatewayLabel + gatewayLabel + UNDER_STEP.gap;

/** With the band, a loop goes deeper by twice the band, under the cartouches of the steps it passes under (spec 14.5). */
export const loopDeeper = (below: number): number => 2 * below;

/** The constants the style sheets need, as CSS variables, so that no value is written twice (spec 3.5). */
export function geometryCssVariables(): Record<string, string> {
  return {
    '--step-width': `${STEP_WIDTH}px`,
    '--step-height': `${STEP_HEIGHT}px`,
    '--lane-header-width': `${LANE_HEADER_WIDTH}px`,
    '--column-width': `${COLUMN_WIDTH}px`,
    '--row-height': `${ROW_HEIGHT}px`,
    '--handle-size': `${HANDLE_SIZE}px`,
    '--quick-add-size': `${QUICK_ADD_SIZE}px`,
    '--edge-stroke': String(EDGE_STROKE),
    '--minimap-width': `${MINIMAP_WIDTH}px`,
    '--canvas-inset': `${CANVAS_INSET}px`,
    '--controls-right': `${CONTROLS_RIGHT}px`,
    '--edge-label-max-width': `${EDGE_LABEL.maxWidth}px`,
    '--edge-label-font-size': `${EDGE_LABEL.fontSize}px`,
    '--edge-label-line-height': `${EDGE_LABEL.lineHeight}px`,
    '--edge-label-padding': `${EDGE_LABEL.paddingTop}px ${EDGE_LABEL.paddingX}px ${EDGE_LABEL.paddingBottom}px`,
    '--edge-label-icon-gap': `${EDGE_LABEL.iconGap}px`,
    '--edge-label-documents-gap': `${EDGE_LABEL.documentsGap}px`,
    '--gateway-label-max-width': `${GATEWAY_LABEL.maxWidth}px`,
    '--gateway-label-line-height': `${GATEWAY_LABEL.lineHeight}px`,
    '--gateway-label-padding-x': `${GATEWAY_LABEL.paddingX}px`,
    '--pictos-rise': `${PICTOS_RISE}px`,
    '--gateway-label-top': `${UNDER_STEP.gatewayLabel}px`,
    '--frequency-top': `${UNDER_STEP.frequencyTop}px`,
    '--frequency-height': `${UNDER_STEP.frequencyHeight}px`,
    '--under-shift': `${UNDER_STEP.shift}px`,
    '--stream-card-width': `${STREAM_CARD.width}px`,
    '--stream-card-height': `${STREAM_CARD.height}px`,
  };
}
