// map.mjs
// Renders the static map canvas of the mockup (lane bodies, headers, steps, arrows, dissolution frame, minimap) as HTML.

import { GRID, LANES, STEPS, EDGES, FRAME } from './map-data.mjs';

const LANE_ICON = { person: 'user', service: 'building-2', system: 'server' };
const MARKER = { decision: 'x', parallel: 'plus', inclusive: 'circle' };
const SHEET_LABEL = { draft: 'brouillon', validated: 'validée', needs_review: 'à revoir' };

const laneTops = [];
let top = 0;
for (const lane of LANES) {
  laneTops.push(top);
  top += lane.rows * GRID.row;
}
const MAP_HEIGHT = top;
const MAX_COL = Math.max(...STEPS.map((s) => s.col));
const MAP_WIDTH = GRID.margin + (MAX_COL + 1 + 3) * GRID.col;

function box(step) {
  return {
    x: GRID.margin + step.col * GRID.col,
    y: laneTops[step.lane] + (step.row ?? 0) * GRID.row + GRID.band,
    w: GRID.stepW,
    h: GRID.stepH,
  };
}

const byCode = new Map(STEPS.map((s) => [s.code, s]));

function port(code, side) {
  const b = box(byCode.get(code));
  if (side === 'l') return [b.x, b.y + b.h / 2];
  if (side === 'r') return [b.x + b.w, b.y + b.h / 2];
  if (side === 't') return [b.x + b.w / 2, b.y];
  return [b.x + b.w / 2, b.y + b.h];
}

/** Orthogonal polyline with rounded corners (radius 10, annex B 3.4 rule 7). */
function roundedPath(points, radius = 10) {
  let d = `M ${points[0][0]} ${points[0][1]}`;
  for (let i = 1; i < points.length - 1; i++) {
    const [px, py] = points[i - 1];
    const [cx, cy] = points[i];
    const [nx, ny] = points[i + 1];
    const inLen = Math.hypot(cx - px, cy - py);
    const outLen = Math.hypot(nx - cx, ny - cy);
    const r = Math.min(radius, inLen / 2, outLen / 2);
    const ax = cx - ((cx - px) / inLen) * r;
    const ay = cy - ((cy - py) / inLen) * r;
    const bx = cx + ((nx - cx) / outLen) * r;
    const by = cy + ((ny - cy) / outLen) * r;
    d += ` L ${ax} ${ay} Q ${cx} ${cy} ${bx} ${by}`;
  }
  const last = points[points.length - 1];
  return `${d} L ${last[0]} ${last[1]}`;
}

/** Loop b to b (annex B 3.4 rule 6): M s C (sx, low) (tx, low) t, with low = max(sy, ty) + 32. */
function loopPath([[sx, sy], [tx, ty]]) {
  const low = Math.max(sy, ty) + 32;
  return `M ${sx} ${sy} C ${sx} ${low} ${tx} ${low} ${tx} ${ty}`;
}

/**
 * Centre of an arrow label. Loop: rule 6 puts it at low - 8, which would straddle the lane separator 28 px under the
 * steps; it is raised to 12 px above the bottom of the free band, so the label (about 19 px high) stays inside it.
 */
function labelPosition(edge) {
  if (edge.labelAt) return edge.labelAt;
  if (edge.loop) return [(edge.points[0][0] + edge.points[1][0]) / 2, Math.max(edge.points[0][1], edge.points[1][1]) + GRID.band - 12];
  const [a, b] = [edge.points[0], edge.points[edge.points.length - 1]];
  return [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
}

function pictos(step, icon) {
  const p = step.pictos;
  if (!p) return '';
  const items = [];
  if (p.docs) items.push(`<span title="Documents produits et reçus">${icon('file-text', 10, 2.25)}${p.docs}</span>`);
  if (p.irritants) items.push(`<span class="picto-irritant" title="Irritants ouverts">${icon('flame', 10, 2.25)}${p.irritants}</span>`);
  if (p.duration) items.push(`<span title="Durée par exécution">${icon('timer', 10, 2.25)}${p.duration}</span>`);
  return `<span class="step-pictos">${items.join('')}</span>`;
}

/**
 * Renders a step. bottomExit: the step is a gateway whose bottom point carries an exit, so its label moves to the
 * right of the vertical axis (AR-58).
 */
function stepHtml(step, icon, bottomExit = false) {
  const b = box(step);
  const lane = LANES[step.lane];
  const classes = ['step', `step--${step.type}`];
  if (step.selected) classes.push('is-selected');
  if (step.cut) classes.push('is-cut');
  if (step.outside) classes.push('is-outside');
  if (step.hover) classes.push('is-hover');
  if (step.pictos) classes.push('has-pictos');
  if (bottomExit) classes.push('has-bottom-exit');
  const laneBg = step.lane % 2 ? 'var(--lane-even)' : 'var(--lane-odd)';
  let style = `left:${b.x}px;top:${b.y}px;--ink:var(--lane-${lane.tint}-ink);--lane-bg:${laneBg}`;
  // spec 9.4: a label of 3 lines stays 2 px above the 3 px gauge (code 12 + 3 x 15 = 57 px from the top border, gauge
  // from 60 px): no top padding, and without pictograms 5 px at the bottom so that a centred label ends at 58 px too
  if (step.sheet && !MARKER[step.type]) style += `;padding-top:0${step.pictos ? '' : ';padding-bottom:5px'}`;
  const code = `<span class="step-code">PRJ-${step.code}</span>`;
  const label = step.label ? `<span class="step-label">${step.label}</span>` : '';
  let sheetState = step.sheet ? `, fiche ${SHEET_LABEL[step.sheet[0]]} ${step.sheet[1]} %` : '';
  if (step.cut) sheetState += ', coupée';
  if (step.outside) sheetState += ', hors du cadre';
  const aria = `aria-label="PRJ-${step.code} · ${step.label || 'parallèle'}${sheetState}"`;
  let html = '';
  if (step.macro) html += `<div class="step-stack" style="left:${b.x + 3}px;top:${b.y + 3}px" aria-hidden="true"></div>`;
  if (MARKER[step.type]) {
    html +=
      `<div class="${classes.join(' ')}" style="${style}" role="button" tabindex="-1" ${aria}>` +
      `<svg class="gateway-shape" viewBox="0 0 170 64" aria-hidden="true"><polygon points="1,32 85,1 169,32 85,63"/></svg>` +
      code +
      `<span class="gateway-marker">${icon(MARKER[step.type], 16, 2.5)}</span>${label}</div>`;
    return html;
  }
  html += `<div class="${classes.join(' ')}" style="${style}" role="button" tabindex="-1" ${aria}>${code}${label}${pictos(step, icon)}`;
  if (step.sheet) {
    html += `<span class="step-gauge"><span class="gauge--${step.sheet[0]}" style="width:${Math.max(4, step.sheet[1])}%"></span></span>`;
  }
  if (step.warning) html += `<span class="step-warning" title="${step.warning}">!</span>`;
  if (step.macro) {
    html += `<span class="step-detailed">${icon('layers', 11, 2.25)}détaillée</span>`;
    html += `<span class="step-frequency">${icon('clock', 11, 2.25)}${step.macro}</span>`;
  }
  html += '</div>';
  if (step.hover) {
    const plus = (cls, x, y, title) =>
      `<button class="quick-add ${cls}" style="left:${x}px;top:${y}px" title="${title}" tabindex="-1">${icon('plus', 12, 2.5)}</button>`;
    html += plus('', b.x - 13, b.y + b.h / 2, 'Insérer une étape avant');
    html += plus('', b.x + b.w + 13, b.y + b.h / 2, 'Insérer une étape après');
    // annex B 2.3: "dessus" and "dessous" only when there is a lane above or below
    if (step.lane > 0) html += plus('', b.x + b.w / 2, b.y - 13, 'Insérer une étape au-dessus');
    if (step.lane < LANES.length - 1) html += plus('', b.x + b.w / 2, b.y + b.h + 13, 'Insérer une étape en dessous');
  }
  return html;
}

function frameHtml(icon) {
  const boxes = FRAME.members.map((c) => box(byCode.get(c)));
  const x = Math.min(...boxes.map((b) => b.x)) - GRID.frameMargin;
  const y = Math.min(...boxes.map((b) => b.y)) - GRID.frameMarginTop;
  const r = Math.max(...boxes.map((b) => b.x + b.w)) + GRID.frameMargin;
  const bottom = Math.max(...boxes.map((b) => b.y + b.h)) + GRID.frameMargin;
  return (
    `<div class="dissolution-frame" style="left:${x}px;top:${y}px;width:${r - x}px;height:${bottom - y}px">` +
    `<button class="dissolution-label" tabindex="-1">${icon('inbox', 12, 2.25)}${FRAME.label}</button></div>`
  );
}

function laneHeads(icon, hoverLane) {
  let html = '<div class="lane-heads" role="group" aria-label="En-têtes des couloirs" style="top:' + GRID.viewY + 'px">';
  LANES.forEach((lane, i) => {
    const hover = i === hoverLane ? ' is-hover' : '';
    html +=
      // named group standing for the row header of the lane (name, code and type in the accessible name)
      `<div class="lane-head${hover}" role="group" aria-label="Couloir ${lane.name}, ${lane.code}, ${lane.typeLabel}" ` +
      `style="top:${laneTops[i]}px;height:${lane.rows * GRID.row}px;` +
      `--pastel:var(--lane-${lane.tint}-pastel);--ink:var(--lane-${lane.tint}-ink)" title="${lane.typeLabel}">` +
      `<div class="lane-head-name">${icon(LANE_ICON[lane.type], 16, 2)}<span>${lane.name}</span></div>` +
      `<code class="lane-head-code">${lane.code}</code>` +
      (i === hoverLane ? `<button class="lane-head-more" title="Menu du couloir" tabindex="-1">${icon('ellipsis', 16, 2)}</button>` : '') +
      '</div>';
  });
  if (hoverLane !== undefined) {
    // AR-27: the "+" lane buttons show on hover of the header layer only
    for (const y of [0, ...laneTops.slice(1), MAP_HEIGHT]) {
      html += `<button class="lane-add" style="top:${y}px" title="Ajouter un couloir" tabindex="-1">${icon('plus', 12, 2.5)}</button>`;
    }
  }
  return html + '</div>';
}

function minimap(viewWidth, viewHeight, viewX) {
  const width = 200;
  const total = GRID.header + MAP_WIDTH;
  const scale = width / total;
  const height = Math.round(MAP_HEIGHT * scale + 16);
  let svg = `<svg width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" aria-hidden="true"><g transform="translate(0,8)">`;
  LANES.forEach((lane, i) => {
    const y = laneTops[i] * scale;
    const h = lane.rows * GRID.row * scale;
    svg += `<rect class="mm-body ${i % 2 ? 'even' : 'odd'}" x="0" y="${y}" width="${width}" height="${h}"/>`;
    svg += `<rect x="0" y="${y}" width="${GRID.header * scale}" height="${h}" fill="var(--lane-${lane.tint}-pastel)"/>`;
  });
  for (const step of STEPS) {
    const b = box(step);
    svg += `<rect class="mm-step" x="${(GRID.header + b.x) * scale}" y="${b.y * scale}" width="${b.w * scale}" height="${b.h * scale}"/>`;
  }
  const shift = Math.max(0, (GRID.viewX - viewX) * scale);
  const vw = (viewWidth - (GRID.viewX - GRID.header)) * scale;
  svg += `<rect class="mm-view" x="${0.75 + shift}" y="${-GRID.viewY * scale}" width="${Math.min(vw, width - 1.5 - shift)}" height="${viewHeight * scale}"/>`;
  return `<div class="minimap" title="Mini-carte">${svg}</g></svg></div>`;
}

/**
 * Renders the map canvas.
 * @param {(name: string, size?: number, stroke?: number) => string} icon icon renderer
 * @param {{ width: number, height: number, hoverLane?: number, controls?: boolean, viewX?: number }} options canvas
 *   size in px; viewX: horizontal position of the view (the start view by default)
 */
export function renderMap(icon, { width, height, hoverLane, controls = true, viewX = GRID.viewX }) {
  const edges = EDGES(port);
  let lanes = '';
  LANES.forEach((lane, i) => {
    lanes +=
      `<div class="lane-body ${i % 2 ? 'lane-body--even' : 'lane-body--odd'}" ` +
      `style="left:${-GRID.header}px;top:${laneTops[i]}px;width:${GRID.header + MAP_WIDTH + 400}px;height:${lane.rows * GRID.row}px"></div>`;
  });
  let svg =
    `<svg class="edges" width="${MAP_WIDTH + 400}" height="${MAP_HEIGHT}" aria-hidden="true">` +
    '<defs><marker id="arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="9" markerHeight="9" ' +
    'markerUnits="userSpaceOnUse" orient="auto"><path d="M0 1 L10 5 L0 9 Z" class="arrow-head"/></marker></defs>';
  let labels = '';
  for (const edge of edges) {
    svg += `<path class="edge" d="${edge.loop ? loopPath(edge.points) : roundedPath(edge.points)}" marker-end="url(#arrow)"/>`;
    if (edge.label || edge.docs) {
      const [x, y] = labelPosition(edge);
      // annex B 3.4 rule 9: document icon and count of the documents carried, their names on hover
      const docs = edge.docs
        ? `<span class="edge-docs" title="${edge.docs.join(', ')}">${icon('file-text', 11, 2.25)}${edge.docs.length}` +
          `<span class="sr-only"> document${edge.docs.length > 1 ? 's' : ''} : ${edge.docs.join(', ')}</span></span>`
        : '';
      labels += `<span class="edge-label" style="left:${x}px;top:${y}px">${edge.label ?? ''}${docs}</span>`;
    }
  }
  svg += '</svg>';
  // AR-58: gateways whose bottom point is the start of an arrow
  const samePoint = (a, b) => a[0] === b[0] && a[1] === b[1];
  const bottomExits = new Set(
    STEPS.filter((s) => MARKER[s.type] && edges.some((e) => samePoint(e.points[0], port(s.code, 'b')))).map((s) => s.code),
  );
  const steps = STEPS.map((s) => stepHtml(s, icon, bottomExits.has(s.code))).join('');
  let html =
    `<div class="canvas" style="width:${width}px;height:${height}px;--view-x:${viewX}px;--view-y:${GRID.viewY}px">` +
    `<div class="viewport" style="transform:translate(${viewX}px,${GRID.viewY}px)">` +
    lanes + frameHtml(icon) + svg + steps + labels +
    '</div>' + laneHeads(icon, hoverLane);
  if (controls) {
    html +=
      '<div class="canvas-controls" role="group" aria-label="Zoom">' +
      `<button title="Zoom arrière" tabindex="-1">${icon('minus', 16, 2)}</button>` +
      '<span class="zoom-value">100 %</span>' +
      `<button title="Zoom avant" tabindex="-1">${icon('plus', 16, 2)}</button>` +
      '<span class="controls-sep"></span>' +
      `<button title="Revenir au début de la carte" tabindex="-1">${icon('arrow-left-to-line', 16, 2)}</button>` +
      '</div>' + minimap(width, height, viewX);
  }
  return html + '</div>';
}
