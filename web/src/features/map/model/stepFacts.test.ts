// stepFacts.test.ts
// What a step shows besides its label: readable duration, sheet gauge, tint of its lane, detailed step, warnings (annex B 2.10, spec 9.4).

import { LANE_TINTS } from '../../../shared/theme/laneTints.ts';
import { gaugeOf, isMacro, readableDuration, tintNumber, warningsByStep } from './stepFacts.ts';

describe('the readable duration (B-2.10-10)', () => {
  it('writes « 45 min », « 2 h », « 1 h 30 »', () => {
    expect([readableDuration(45), readableDuration(120), readableDuration(90), readableDuration(65), readableDuration(59.6)]).toEqual(['45 min', '2 h', '1 h 30', '1 h 05', '1 h']);
  });
});

describe('the gauge of the sheet (B-2.10-21 to B-2.10-25, spec 9.4)', () => {
  it('is absent for an empty sheet', () => {
    expect(gaugeOf({ status: 'empty', completeness: 0 })).toBeNull();
  });

  it('is as wide as the completeness, 4 % at least, 100 % at most', () => {
    expect(gaugeOf({ status: 'draft', completeness: 55 })).toEqual({ status: 'draft', width: 55 });
    expect(gaugeOf({ status: 'draft', completeness: 1 })?.width).toBe(4);
    expect(gaugeOf({ status: 'validated', completeness: 130 })?.width).toBe(100);
  });

  it('keeps the state of the sheet as its code: draft, validated, needs_review (the 1.x classes missed two)', () => {
    expect(['draft', 'validated', 'needs_review'].map((status) => gaugeOf({ status: status as 'draft', completeness: 50 })?.status)).toEqual(['draft', 'validated', 'needs_review']);
  });
});

describe('the tint of a lane (spec 9.2)', () => {
  it('is the number of the tint whose pastel the colour is, whatever its case', () => {
    expect(LANE_TINTS.map((tint) => tintNumber(tint.pastel))).toEqual([1, 2, 3, 4, 5, 6, 7, 8]);
    expect(tintNumber(LANE_TINTS[2]?.pastel.toLowerCase() ?? '')).toBe(3);
  });

  it('is the nearest tint for another colour, the first one for an unreadable colour', () => {
    const nearSky = `#${['D8', 'E8', 'FB'].join('')}`;
    expect(tintNumber(nearSky)).toBe(3);
    expect(tintNumber('blue')).toBe(1);
  });
});

describe('a detailed step and the warnings', () => {
  it('a step is detailed when its sub-map holds steps', () => {
    expect([isMacro({ sub_map_id: 'm', sub_map_empty: false }), isMacro({ sub_map_id: 'm', sub_map_empty: true }), isMacro({ sub_map_id: null, sub_map_empty: true })]).toEqual([true, false, false]);
  });

  it('the warnings of the layout are grouped by step in their order (B-2.10-16)', () => {
    const byStep = warningsByStep([{ step_id: 'a', code: 'ETAPE_ISOLEE' }, { step_id: 'b', code: 'DEBUT_LIEN_ENTRANT' }, { step_id: 'a', code: 'FIN_LIEN_SORTANT' }]);
    expect([...byStep]).toEqual([['a', ['ETAPE_ISOLEE', 'FIN_LIEN_SORTANT']], ['b', ['DEBUT_LIEN_ENTRANT']]]);
  });
});
