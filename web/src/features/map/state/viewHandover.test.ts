// viewHandover.test.ts
// The place of the view handed between the map and its view in BPMN 2.0 (trial of the 3.3): the step nearest the middle, put back at the same point of the page at the same zoom, only between the two views of one map.

import { anchorOf, handedView, tellView, viewAt } from './viewHandover.ts';

const steps = [
  { code: 'A', box: { x: 0, y: 0, width: 100, height: 50 } },
  { code: 'B', box: { x: 400, y: 300, width: 100, height: 50 } },
];

describe('the place of the view handed between the two views of a map', () => {
  it('takes the step nearest the middle of the screen, where it stands on the page', () => {
    const anchor = anchorOf(steps, { x: 0, y: 0, zoom: 1 }, { width: 900, height: 650 }, { x: 10, y: 60 });
    expect(anchor).toEqual({ code: 'B', page: { x: 460, y: 385 }, zoom: 1 });
    expect(anchorOf(steps, { x: 0, y: 0, zoom: 1 }, { width: 0, height: 0 }, { x: 0, y: 0 })).toBeNull();
  });

  it('puts the step back at the same point of the page, at the same zoom, in a drawing placed otherwise', () => {
    const anchor = anchorOf(steps, { x: -120, y: 40, zoom: 0.5 }, { width: 800, height: 600 }, { x: 0, y: 100 });
    if (anchor === null) throw new Error('no anchor');
    const box = { x: 900, y: 500, width: 170, height: 64 };
    const view = viewAt(anchor, box, { x: 0, y: 140 });
    expect(view.zoom).toBe(0.5);
    expect([view.x + (box.x + box.width / 2) * view.zoom, 140 + view.y + (box.y + box.height / 2) * view.zoom]).toEqual([anchor.page.x, anchor.page.y]);
  });

  it('hands the view only to the other view of the same map', () => {
    const anchor = { code: 'A', page: { x: 1, y: 2 }, zoom: 1 };
    tellView('m1', 'map', () => anchor);
    expect([handedView('m1', 'bpmn'), handedView('m1', 'map'), handedView('m2', 'bpmn')]).toEqual([anchor, null, null]);
  });
});
