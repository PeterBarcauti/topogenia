// LaneHeaders.test.tsx
// The lane headers in the fixed margin and the lane bodies behind the steps: name, type, code, colours, height and place on the screen (annex B 2.12, spec 9.3).

import { render, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, showMap } from '../../../test/editor.tsx';
import { laneOf, mapOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { layoutMap } from '../model/mapLayout.ts';
import { estimateMeasure } from '../model/textSize.ts';
import { createMapEditor } from '../state/editor.ts';
import { EditorProvider } from '../state/EditorContext.tsx';
import { LaneHeaders } from './LaneHeaders.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the lane headers (B-2.12-01 to B-2.12-10)', () => {
  it('one a lane, in their order, with the name in the language shown, the code, and the type named for assistive technologies', async () => {
    await i18next.changeLanguage('en');
    await showMap(sampleMap());
    const heads = within(screen.getByTestId('lane-headers')).getAllByRole('group');
    expect(heads.map((head) => head.getAttribute('aria-label'))).toEqual(['Lane Sales, VTE-C01, Person or role', 'Lane Comptabilité, VTE-C02, Department']);
    expect(heads[1]).toHaveAttribute('title', 'Department');
    expect(heads[1]?.querySelector('svg.i')).not.toBeNull();
  });

  it('take the pastel and the ink of the tint of their lane', async () => {
    await showMap(sampleMap());
    const head = screen.getByTestId('lane-head-VTE-C02');
    expect([head.style.getPropertyValue('--pastel'), head.style.getPropertyValue('--ink')]).toEqual(['var(--lane-2-pastel)', 'var(--lane-2-ink)']);
  });

  it('stand at the top of their band in the view and are as high as their rows (view at the start: y 16, zoom 1)', async () => {
    const map = sampleMap();
    await showMap({ ...map, steps: [...map.steps, stepOf('s5', 'l1', 4, { row: 1 })] });
    const place = (code: string) => [screen.getByTestId(`lane-head-${code}`).style.top, screen.getByTestId(`lane-head-${code}`).style.height];
    expect(place('VTE-C01')).toEqual(['16px', '240px']);
    expect(place('VTE-C02')).toEqual(['256px', '120px']);
  });

  it('follow the vertical scroll and the zoom, not the horizontal scroll; their width does not depend on the zoom', () => {
    const layout = layoutMap(sampleMap(), 'fr', estimateMeasure);
    render(<EditorProvider editor={createMapEditor({ storage: memoryStorage() })}><LaneHeaders lanes={layout.lanes} view={{ x: -900, y: -40, zoom: 0.5 }} showCodes /></EditorProvider>);
    const head = screen.getByTestId('lane-head-VTE-C02');
    expect([head.style.top, head.style.height, head.style.left, head.style.width]).toEqual(['20px', '60px', '', '']);
    expect(screen.getByTestId('lane-headers').style.width).toBe('');
  });
});

describe('the lane bodies (B-3.1-20, B-2.12-25, spec 9.3)', () => {
  it('alternate white and grey from the first lane, and repeat name and type in the margin under the headers', async () => {
    await showMap(mapOf({ lanes: [laneOf('l1', 0, { name: 'Achats' }), laneOf('l2', 1), laneOf('l3', 2, { type: 'system', name: 'ERP' })] }));
    expect(['VTE-C01', 'VTE-C02', 'VTE-C03'].map((code) => screen.getByTestId(`lane-body-${code}`).className)).toEqual([
      'lane-body lane-body--odd', 'lane-body lane-body--even', 'lane-body lane-body--odd',
    ]);
    const margin = screen.getByTestId('lane-body-VTE-C03').querySelector('.lane-body-margin');
    expect([margin?.textContent, margin?.getAttribute('aria-hidden')]).toEqual(['ERPSystème', 'true']);
  });
});
