// arrival.test.ts
// The object a mention of the control names on the map it leads to (F-64): kept for that map only, given once, a mention that names nothing forgetting it; the steps named, a step or the steps of a lane.

import { sampleMap } from '../../../test/maps.ts';
import { focusOnArrival, stepsNamed, takeArrival } from './arrival.ts';

describe('the object named on the map a mention leads to (F-64)', () => {
  it('is kept for that map only, given once, and a mention that names nothing forgets it', () => {
    focusOnArrival({ mapId: 'detail', stepId: 's9', laneId: null });
    expect(takeArrival('other')).toBeNull();
    expect(takeArrival('detail')).toEqual({ mapId: 'detail', stepId: 's9', laneId: null });
    expect(takeArrival('detail')).toBeNull();
    focusOnArrival({ mapId: 'detail', stepId: 's9', laneId: null });
    focusOnArrival({ mapId: 'detail', stepId: null, laneId: null });
    expect(takeArrival('detail')).toBeNull();
  });

  it('names a step of the map, or the steps of a lane; nothing for a step or a lane not there', () => {
    const map = sampleMap();
    expect(stepsNamed(map, { stepId: 's3', laneId: null })).toEqual(['s3']);
    expect(stepsNamed(map, { stepId: null, laneId: 'l1' })).toEqual(['s1', 's2', 's3']);
    expect(stepsNamed(map, { stepId: 'gone', laneId: null })).toEqual([]);
    expect(stepsNamed(map, { stepId: null, laneId: null })).toEqual([]);
  });
});
