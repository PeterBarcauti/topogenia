// map.test.ts
// The rules of map.css that carry the charter of the canvas: shapes and states of the steps, pictograms, gauge, handles, flows, lane headers, the image of an export (spec 9.3, 9.4, annex B 2.10, 2.14, 3).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { STEP_HEIGHT, STEP_WIDTH } from '../features/map/model/geometry/constants.ts';

const css = readFileSync(join(import.meta.dirname, 'map.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');

/** The declarations of every rule whose selector list holds the selector given, the last one winning. */
function declarations(selector: string): Map<string, string> {
  const found = new Map<string, string>();
  for (const match of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selectors = (match[1] ?? '').split(',').map((part) => part.trim().replace(/\s+/g, ' '));
    if (!selectors.includes(selector)) continue;
    for (const declaration of (match[2] ?? '').split(';')) {
      const colon = declaration.indexOf(':');
      if (colon > 0) found.set(declaration.slice(0, colon).trim(), declaration.slice(colon + 1).trim());
    }
  }
  return found;
}

const rule = (selector: string, property: string): string | undefined => declarations(selector).get(property);

describe('the steps (spec 9.4, B-3.2-04 to B-3.2-18)', () => {
  it('a white card, 1 px border, radius 10, shadow of a step, left stripe of 3 px in the ink of its lane', () => {
    expect([rule('.step', 'background'), rule('.step', 'border'), rule('.step', 'border-left'), rule('.step', 'border-radius'), rule('.step', 'box-shadow')])
      .toEqual(['var(--surface)', '1px solid var(--border)', '3px solid var(--ink)', 'var(--radius-step)', 'var(--shadow-step)']);
  });

  it('wait dashed, start and end pills, the end with a double border', () => {
    expect(rule('.step--wait', 'border-style')).toBe('dashed');
    expect(rule('.step--start', 'border-radius')).toBe('var(--radius-pill)');
    expect(rule('.step--end', 'border-radius')).toBe('var(--radius-pill)');
    expect(rule('.step--end', 'border-top')).toBe('3px double var(--edge)');
  });

  it('gateways: white diamond with a contour of 1.5 px in the ink, the marker in the ink, the parallel and inclusive labels muted', () => {
    expect([rule('.gateway-shape polygon', 'fill'), rule('.gateway-shape polygon', 'stroke'), rule('.gateway-shape polygon', 'stroke-width')]).toEqual(['var(--surface)', 'var(--ink)', '1.5']);
    expect(rule('.gateway-marker', 'color')).toBe('var(--ink)');
    expect([rule('.step--parallel .step-label', 'color'), rule('.step--inclusive .step-label', 'color')]).toEqual(['var(--text-muted)', 'var(--text-muted)']);
  });

  it('chosen: a ring of 2 px in --accent and the raised shadow; a chosen diamond takes --accent on its contour', () => {
    expect([rule('.step.is-selected', 'outline'), rule('.step.is-selected', 'box-shadow')]).toEqual(['2px solid var(--accent)', 'var(--shadow-raised)']);
    expect(rule('.step--gateway.is-selected .gateway-shape polygon', 'stroke')).toBe('var(--accent)');
  });

  it('label of 13 px on three lines at most, code of 10.5 px at fixed pitch with tabular figures in --text-muted (B-6-20)', () => {
    expect([rule('.step-label', 'font-size'), rule('.step-label', '-webkit-line-clamp')]).toEqual(['var(--font-size-step)', '3']);
    const code = (property: string) => rule('.step-code', property);
    expect([code('font-family'), code('font-size'), code('color'), code('font-variant-numeric')]).toEqual(['var(--font-mono)', 'var(--font-size-code)', 'var(--text-muted)', 'tabular-nums']);
  });

  it('appear by a fade of the base duration, without any translation (B-3.2-20)', () => {
    expect(rule('.react-flow__node-step', 'animation')).toBe('step-in var(--duration-base) ease-out');
    const keyframes = /@keyframes step-in\s*\{([\s\S]*?)\}\s*\}/.exec(css)?.[1] ?? '';
    expect(keyframes).toMatch(/opacity: 0/);
    expect(keyframes).not.toMatch(/transform|translate/);
  });

  it('a detailed step: a second card behind, the badge and the frequency (spec 9.4)', () => {
    expect([rule('.step-stack', 'z-index'), rule('.step-stack', 'background'), rule('.step-detailed', 'background'), rule('.step-frequency', 'background')])
      .toEqual(['-1', 'var(--surface)', 'var(--frame-label-bg)', 'var(--lane-6-pastel)']);
  });
});

describe('the pictograms, the gauge, the warning (annex B 2.10, spec 9.4)', () => {
  it('pictograms of 10 px in the bottom right corner, in --text-muted, the pain points in muted danger', () => {
    expect([rule('.step-pictos', 'right'), rule('.step-pictos', 'bottom'), rule('.step-pictos', 'font-size'), rule('.step-pictos', 'color')])
      .toEqual(['9px', '5px', 'var(--font-size-micro)', 'var(--text-muted)']);
    expect(rule('.step-pictos .picto-irritant', 'color')).toBe('var(--danger-muted)');
    expect(rule('.step--gateway .step-pictos', 'background')).toBe('var(--surface)');
  });

  it('on hover only with the preference: hidden, shown on hover of the step (B-2.10-12)', () => {
    expect(rule('.pictos-on-hover .step-pictos', 'opacity')).toBe('0');
    expect(rule('.pictos-on-hover .react-flow__node:hover .step-pictos', 'opacity')).toBe('1');
  });

  it('always shown, the label moves up by 12 px (B-2.10-14); centred at the bottom of a gateway (B-2.10-15)', () => {
    expect(rule('.map-canvas:not(.pictos-on-hover) .step.has-pictos', 'padding-bottom')).toBe('calc(2px + var(--pictos-rise))');
    expect([rule('.step--gateway .step-pictos', 'left'), rule('.step--gateway .step-pictos', 'transform')]).toEqual(['50%', 'translateX(-50%)']);
  });

  it('the opacity of the handles and the pictograms changes in 150 ms, the width of the gauge in 200 ms (B-6-51, B-6-52)', () => {
    expect([rule('.react-flow__handle.step-handle', 'transition'), rule('.step-pictos', 'transition')]).toEqual(['opacity var(--duration-fast)', 'opacity var(--duration-fast)']);
    expect(rule('.step-gauge span', 'transition')).toBe('width var(--duration-base)');
  });

  it('a gauge of 3 px at the bottom: draft --warning, validated --success, to review --danger', () => {
    expect([rule('.step-gauge', 'height'), rule('.step-gauge', 'bottom')]).toEqual(['3px', '0']);
    expect([rule('.gauge--draft', 'background'), rule('.gauge--validated', 'background'), rule('.gauge--needs_review', 'background')])
      .toEqual(['var(--warning-graphic)', 'var(--success)', 'var(--danger)']);
  });

  it('the warning in the top left corner, in the warning colour', () => {
    expect([rule('.step-warning', 'left'), rule('.step-warning', 'top'), rule('.step-warning', 'background')]).toEqual(['-10px', '-9px', 'var(--warning-graphic)']);
  });

  it('on a gateway, the warning sits on the upper left side of the diamond, under the code written above the shape (B-2.10-16, spec 9.4)', () => {
    const px = (selector: string, property: string) => Number.parseFloat(rule(selector, property) ?? 'NaN');
    const size = px('.step-warning', 'width');
    const centre = { x: px('.step--gateway .step-warning', 'left') + size / 2, y: px('.step--gateway .step-warning', 'top') + size / 2 };
    // The side runs from the left point (0, height / 2) to the top point (width / 2, 0) of the box of the step.
    expect(Math.abs((STEP_HEIGHT / 2) * (1 - centre.x / (STEP_WIDTH / 2)) - centre.y)).toBeLessThanOrEqual(1);
    // The code stands above the box: the badge keeps inside it, so it never covers the code.
    expect(rule('.step--gateway .step-code', 'bottom')).toBe('calc(100% + 1px)');
    expect(centre.y - size / 2).toBeGreaterThanOrEqual(0);
  });
});

describe('the handles and the flows (annex B 3.3, 3.4)', () => {
  it('handles: dots of 10 px bordered with white, shown only on hover or when the step is chosen', () => {
    const handle = (property: string) => rule('.react-flow__handle.step-handle', property);
    expect([handle('width'), handle('background-color'), handle('border'), handle('opacity')]).toEqual(['var(--handle-size)', 'var(--accent)', '2px solid var(--surface)', '0']);
    expect(rule('.react-flow__node:hover .step-handle', 'opacity')).toBe('1');
    expect(rule('.step.is-selected .step-handle', 'opacity')).toBe('1');
  });

  it('flows of 1.6 in --edge, the chosen one in --accent, the arrow in --edge (B-3.4-29 to B-3.4-31)', () => {
    expect([rule('.map-canvas .react-flow__edge-path', 'stroke'), rule('.map-canvas .react-flow__edge-path', 'stroke-width')]).toEqual(['var(--edge)', 'var(--edge-stroke)']);
    expect(rule('.map-canvas .react-flow__edge.selected .react-flow__edge-path', 'stroke')).toBe('var(--accent)');
    expect(rule('.map-arrow polyline', 'fill')).toBe('var(--edge)');
  });

  it('a label: white bordered box of 110 px at most in the text of the geometry (B-3.4-35)', () => {
    expect([rule('.edge-label', 'background'), rule('.edge-label', 'border'), rule('.edge-label', 'font-size')]).toEqual(['var(--surface)', '1px solid var(--border)', 'var(--edge-label-font-size)']);
  });
});

describe('the lanes and the controls (spec 9.3, annex B 2.1, 2.12)', () => {
  it('headers in the fixed margin: pastel of the lane, stripe of 4 px in the ink, name of 13 px semi-bold on two lines, code of 11 px', () => {
    expect([rule('.lane-heads', 'width'), rule('.lane-head', 'background'), rule('.lane-head', 'border-right'), rule('.lane-head', 'color')])
      .toEqual(['var(--lane-header-width)', 'var(--pastel)', '4px solid var(--ink)', 'var(--ink)']);
    expect([rule('.lane-head-name', 'font-size'), rule('.lane-head-name', 'font-weight'), rule('.lane-head-name span', '-webkit-line-clamp')]).toEqual(['var(--font-size-lane-name)', '600', '2']);
    expect(rule('.lane-head-code', 'font-size')).toBe('var(--font-size-lane-code)');
  });

  it('lane bodies white and --lane-even in alternation, separated by --lane-separator', () => {
    expect([rule('.lane-body--odd', 'background'), rule('.lane-body--even', 'background'), rule('.lane-body', 'border-bottom')]).toEqual(['var(--lane-odd)', 'var(--lane-even)', '1px solid var(--lane-separator)']);
  });

  it('the rectangle of selection in --accent on its soft background (B-6-25)', () => {
    expect([rule('.map-canvas .react-flow__selection', 'background'), rule('.map-canvas .react-flow__selection', 'border')]).toEqual(['var(--accent-soft)', '1px dashed var(--accent)']);
  });

  it('the zoom controls left of the mini map by the offset of the geometry (B-2.1-41)', () => {
    expect(rule('.map-canvas .react-flow__panel.map-controls', 'right')).toBe('var(--controls-right)');
    expect(rule('.map-canvas .react-flow__panel.map-minimap', 'width')).toBe('var(--minimap-width)');
  });
});

describe('the states and the gestures of the edition (annex B 2.3, 2.7, 2.12, 3.2)', () => {
  it('a step cut or outside the frame: the card alone at --step-cut-opacity, the text whole in --text-muted, the cut dashed (AR-53, B-2.7-09, B-2.9-19, B-3.2-16, B-3.2-17)', () => {
    for (const state of ['is-cut', 'is-outside']) {
      expect([rule(`.step.${state}`, 'opacity'), rule(`.step.${state}`, 'isolation'), rule(`.step.${state}`, 'border-color'), rule(`.step.${state}`, 'box-shadow')]).toEqual([undefined, 'isolate', 'transparent', 'none']);
      expect([rule(`.step.${state}::before`, 'opacity'), rule(`.step.${state}::before`, 'z-index'), rule(`.step.${state}::before`, 'background')]).toEqual(['var(--step-cut-opacity)', '-1', 'var(--surface)']);
      expect([rule(`.step.${state} .step-label`, 'color'), rule(`.step--gateway.${state} .gateway-shape`, 'opacity')]).toEqual(['var(--text-muted)', 'var(--step-cut-opacity)']);
    }
    expect([rule('.step.is-cut::before', 'border-style'), rule('.step.is-outside::before', 'border-style')]).toEqual(['dashed', undefined]);
  });

  it('the source of the link mode: a ring of its token (B-3.2-15)', () => {
    expect(rule('.step.is-link-source', 'outline')).toBe('2px solid var(--link-source-ring)');
    expect(rule('.step--gateway.is-link-source .gateway-shape polygon', 'stroke')).toBe('var(--link-source-ring)');
  });

  it('the frame of a dissolution: dashes of --frame, its label on --frame-label-bg, the pointer through the frame but its label (B-2.9-17, B-2.3-08, B-6-26)', () => {
    expect([rule('.dissolution-frame', 'border'), rule('.dissolution-frame', 'border-radius'), rule('.react-flow__node-frame', 'pointer-events')]).toEqual(['1.5px dashed var(--frame)', 'var(--radius-large)', 'none']);
    expect([rule('.dissolution-label', 'background'), rule('.dissolution-label', 'color'), rule('.dissolution-label', 'border'), rule('.dissolution-label', 'pointer-events')])
      .toEqual(['var(--frame-label-bg)', 'var(--accent-strong)', '1px solid var(--frame)', 'auto']);
  });

  it('the RACI badges: under the step at the offset of the layout, one ground a letter, « A ? » in the warning colours (B-2.11-05, B-2.11-07)', () => {
    expect([rule('.raci-badges', 'top'), rule('.raci-badges', 'pointer-events')]).toEqual(['calc(100% + var(--raci-top))', 'none']);
    expect(['A', 'S', 'C', 'I'].map((letter) => rule(`.raci-badge--${letter}`, 'background'))).toEqual(['var(--raci-a-bg)', 'var(--raci-s-bg)', 'var(--raci-c-bg)', 'var(--raci-i-bg)']);
    expect([rule('.raci-badge--missing', 'background'), rule('.raci-badge--missing', 'border-color'), rule('.raci-badge--missing', 'color')]).toEqual(['var(--warning-soft)', 'var(--warning-graphic)', 'var(--warning-text)']);
  });

  it('the RACI panel and matrix: the lane R and its cells on --raci-r-bg, the letter given in --accent-strong, the headers of the table on --table-header-bg (B-2.11-10, B-6-30)', () => {
    expect([rule('.raci-letter--R', 'background'), rule('.raci-cell--R', 'background'), rule('.raci-letter.is-active', 'background')]).toEqual(['var(--raci-r-bg)', 'var(--raci-r-bg)', 'var(--accent-strong)']);
    expect([rule('.raci-matrix thead th', 'background'), rule('.raci-matrix thead th', 'position'), rule('.raci-matrix-code', 'font-family')]).toEqual(['var(--table-header-bg)', 'sticky', 'var(--font-mono)']);
    expect([rule('.raci-panel', 'position'), rule('.raci-panel', 'box-shadow')]).toEqual(['fixed', 'var(--shadow-menu)']);
  });

  it('the canvas shares the workspace with the panel docked on its right (spec 9.5)', () => {
    expect([rule('.map-workspace', 'display'), rule('.map-canvas', 'flex'), rule('.map-canvas', 'min-width')]).toEqual(['flex', '1', '0']);
  });

  it('the « + » of the quick add: hidden until the step is hovered, held by a bridge of 30 px that exists only while it is hovered (B-2.3-41, B-2.3-45)', () => {
    expect([rule('.quick-add', 'opacity'), rule('.react-flow__node:hover .quick-add', 'opacity'), rule('.quick-add', 'width')]).toEqual(['0', '1', 'var(--quick-add-size)']);
    expect(rule('.quick-add-bridge', 'inset')).toBe('-30px');
    // Out of the hover, the bridge would take the pointer from the flows and the rectangle of selection (P6-T10, B-8.3-07, B-8.3-49).
    expect([rule('.quick-add-bridge', 'display'), rule('.react-flow__node:hover .quick-add-bridge', 'display')]).toEqual(['none', 'block']);
  });

  it('a lane header dragged carries the raised shadow, the lane aimed is underlined in dashes (B-2.12-17, B-2.12-18)', () => {
    expect(rule('.lane-head.is-dragging', 'box-shadow')).toBe('var(--shadow-raised)');
    expect(rule('.lane-head.is-drop-target', 'border-bottom')).toBe('2px dashed var(--ink)');
    expect(rule('.lane-heads:hover .lane-add', 'opacity')).toBe('1');
  });
});

describe('the image of an export (annex B 2.14, B-2.9-20, B-2.12-25, S-4.1-08)', () => {
  const exporting = (selector: string) => `.map-canvas.is-exporting ${selector}`;

  it('nothing moves: every animation and transition of the canvas is cut, pseudo-elements included (B-2.14-08)', () => {
    for (const selector of ['*', '*::before', '*::after']) expect([rule(exporting(selector), 'animation-name'), rule(exporting(selector), 'transition-property')]).toEqual(['none', 'none']);
  });

  it('the selection is neutral: a step, a gateway and a flow chosen look as the others, no rectangle nor box of the selection (B-2.14-09)', () => {
    expect([rule(exporting('.step.is-selected'), 'outline'), rule(exporting('.step.is-selected'), 'box-shadow')]).toEqual(['0', rule('.step', 'box-shadow')]);
    expect(rule(exporting('.step--gateway.is-selected'), 'box-shadow')).toBe('none');
    expect([rule(exporting('.step--gateway.is-selected .gateway-shape polygon'), 'stroke'), rule(exporting('.step--gateway.is-selected .gateway-shape polygon'), 'stroke-width')])
      .toEqual([rule('.gateway-shape polygon', 'stroke'), rule('.gateway-shape polygon', 'stroke-width')]);
    expect(rule(exporting('.react-flow__edge.selected .react-flow__edge-path'), 'stroke')).toBe(rule('.map-canvas .react-flow__edge-path', 'stroke'));
    expect([rule(exporting('.react-flow__nodesselection'), 'display'), rule(exporting('.react-flow__selection'), 'display')]).toEqual(['none', 'none']);
  });

  it('the frame of a dissolution is hidden; the margin of the lanes, hidden on the page under the headers, is drawn (B-2.14-10, B-2.9-20, B-2.12-25)', () => {
    expect(rule(exporting('.react-flow__node-frame'), 'display')).toBe('none');
    expect([rule('.lane-body-margin', 'visibility'), rule(exporting('.lane-body-margin'), 'visibility')]).toEqual(['hidden', 'visible']);
  });

  it('the classification: one line of small semi-bold text, placed by the view (S-4.1-08)', () => {
    expect([rule('.export-caption', 'position'), rule('.export-caption', 'font-size'), rule('.export-caption', 'font-weight'), rule('.export-caption', 'color'), rule('.export-caption', 'white-space')])
      .toEqual(['absolute', 'var(--font-size-small)', '600', 'var(--text)', 'nowrap']);
  });
});
