// MapScreen.test.tsx
// The screen of a map: its reading, its failure and « Try again », the banners, the empty map, the controls, the mini map, the dots, the route (annex B 2.1, 2.2, 7).

import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { ApiError } from '../../api/errors.ts';
import { App } from '../../app/App.tsx';
import { i18next } from '../../shared/i18n/i18n.ts';
import { memoryStorage, renderMapScreen, showMap } from '../../test/editor.tsx';
import { callsTo, fakeApi } from '../../test/fakeApi.ts';
import { MAP_ID, laneOf, mapOf, sampleMap } from '../../test/maps.ts';
import { fakeSource, profileOf } from '../../test/profiles.ts';
import type { MapSource } from './state/mapStore.ts';

const history: Schema<'HistoryView'> = { undo: [], redo: [] };

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the reading of the map', () => {
  it('shows « Loading » until the map is read, then its canvas with React Flow (B-2.1-38)', async () => {
    let answer: (reply: Schema<'MapReply'>) => void = () => undefined;
    const source: MapSource = () => new Promise((resolve) => (answer = resolve));
    renderMapScreen({ mapId: MAP_ID, source, storage: memoryStorage() });
    expect(screen.getByTestId('map-loading')).toHaveTextContent('Chargement de la carte…');
    answer({ map: sampleMap(), history });
    const canvas = await screen.findByTestId('map-canvas');
    expect(canvas.querySelector('.react-flow')).not.toBeNull();
    expect(canvas).toHaveAttribute('aria-label', 'Carte du processus');
  });

  it('shows the failure and reads the map again on « Réessayer »', async () => {
    const replies: Promise<Schema<'MapReply'>>[] = [Promise.reject(new ApiError(500, 'INTROUVABLE', 'Carte introuvable.', {})), Promise.resolve({ map: sampleMap(), history })];
    const source = vi.fn<MapSource>(() => replies.shift() ?? Promise.reject(new Error('no reply')));
    renderMapScreen({ mapId: MAP_ID, source, storage: memoryStorage() });
    expect(await screen.findByTestId('map-error')).toHaveTextContent('Carte introuvable.');
    await userEvent.click(screen.getByTestId('map-error-action'));
    expect(await screen.findByTestId('map-canvas')).toBeInTheDocument();
    expect(source).toHaveBeenCalledTimes(2);
  });

  it('is the screen of the route /maps/{id}, read from GET /api/maps/{id}', async () => {
    window.history.replaceState(null, '', `/maps/${MAP_ID}`);
    const sent = fakeApi({ [`GET /api/maps/${MAP_ID}`]: { json: { map: sampleMap(), history } } });
    render(<App source={fakeSource([profileOf()]).source} />);
    expect(await screen.findByTestId('map-canvas')).toBeInTheDocument();
    expect(callsTo(sent, 'GET', `/api/maps/${MAP_ID}`).length).toBeGreaterThan(0);
  });
});

describe('the banners (B-2.1-35, B-2.1-36)', () => {
  it('« Consultation » for a viewer who may not change the structure, nothing for the facilitator', async () => {
    await showMap(sampleMap({ rights: { structure: false, role: 'reader' } }));
    expect(screen.getByTestId('map-read-only')).toHaveTextContent('Consultation : cliquer sur une étape pour lire sa fiche.');
  });

  it('none for the facilitator of a map within the advised depth', async () => {
    await showMap(sampleMap());
    expect([screen.queryByTestId('map-read-only'), screen.queryByTestId('map-depth'), screen.queryByTestId('map-empty')]).toEqual([null, null, null]);
  });

  it('no depth alert at the advised depth itself', async () => {
    await showMap(sampleMap({ depth: 4, recommended_depth: 4 }));
    expect(screen.queryByTestId('map-depth')).toBeNull();
  });

  it('the depth alert past the advised depth', async () => {
    await showMap(sampleMap({ depth: 5, recommended_depth: 4 }));
    expect(screen.getByTestId('map-depth')).toHaveTextContent('Cette carte est au niveau 5. Au-delà de 4 niveaux, la lecture devient difficile.');
    expect(screen.getByTestId('map-depth')).toHaveClass('map-banner--warning');
  });
});

describe('the empty map (B-2.1-48)', () => {
  it('tells the facilitator how to add a step', async () => {
    await showMap(mapOf({ lanes: [laneOf('l1', 0)] }));
    expect(screen.getByTestId('map-empty')).toHaveTextContent('Cette carte est vide. Double-cliquer dans un couloir pour ajouter une étape.');
  });

  it('says nothing to a reader, nor on a map with steps', async () => {
    await showMap(mapOf({ lanes: [laneOf('l1', 0)], rights: { structure: false, role: 'reader' } }));
    expect(screen.queryByTestId('map-empty')).toBeNull();
  });
});

describe('the controls of the canvas (B-2.1-41 to B-2.1-47)', () => {
  it('zoom in, zoom out and back to the start, no fit view nor lock', async () => {
    const canvas = await showMap(sampleMap());
    const controls = canvas.querySelector('.react-flow__controls') as HTMLElement;
    expect(controls).toHaveClass('map-controls', 'bottom', 'right');
    expect(within(controls).getAllByRole('button').map((button) => button.getAttribute('title'))).toEqual(['Zoom avant', 'Zoom arrière', 'Revenir au début de la carte']);
    expect([controls.querySelector('.react-flow__controls-fitview'), controls.querySelector('.react-flow__controls-interactive')]).toEqual([null, null]);
  });

  // 32 real clicks: some 2 s alone, past the 5 s of a test when the whole suite runs at once; the time allowed is widened, the checks stay.
  it('zoom out down to 0.2 and in up to 2, the view kept bounded at the start (B-2.2-01, B-2.2-08)', { timeout: 15_000 }, async () => {
    const canvas = await showMap(sampleMap());
    const viewport = canvas.querySelector('.react-flow__viewport') as HTMLElement;
    const zoomOut = within(canvas).getByTitle('Zoom arrière');
    for (let click = 0; click < 12; click++) await userEvent.click(zoomOut);
    expect(viewport.style.transform).toBe('translate(192px,16px) scale(0.2)');
    expect(zoomOut).toBeDisabled();
    const zoomIn = within(canvas).getByTitle('Zoom avant');
    for (let click = 0; click < 20; click++) await userEvent.click(zoomIn);
    expect(viewport.style.transform).toBe('translate(192px,16px) scale(2)');
    expect(zoomIn).toBeDisabled();
  });

  it('a drag of the background past the start of the map comes back to the bound, a drag the other way scrolls (B-2.2-06, B-2.2-07)', async () => {
    const canvas = await showMap(sampleMap());
    const pane = canvas.querySelector('.react-flow__pane') as HTMLElement;
    const viewport = canvas.querySelector('.react-flow__viewport') as HTMLElement;
    // The zoom library follows the pointer on the window of the event (`view`), which jsdom will not take at construction.
    const mouse = (target: EventTarget, type: string, clientX: number, clientY: number) => {
      const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, clientX, clientY });
      Object.defineProperty(event, 'view', { value: window });
      target.dispatchEvent(event);
    };
    const drag = (dx: number, dy: number) => {
      mouse(pane, 'mousedown', 500, 300);
      mouse(window, 'mousemove', 500 + dx / 2, 300 + dy / 2);
      mouse(window, 'mousemove', 500 + dx, 300 + dy);
      mouse(window, 'mouseup', 500 + dx, 300 + dy);
    };
    await act(async () => {
      drag(300, 120);
      await Promise.resolve();
    });
    expect(viewport.style.transform).toBe('translate(192px,16px) scale(1)');
    await act(async () => {
      drag(-250, 0);
      await Promise.resolve();
    });
    expect(viewport.style.transform).toBe('translate(-58px,16px) scale(1)');
  });

  it('a double click on the background does not zoom (B-2.2-03)', async () => {
    const canvas = await showMap(sampleMap());
    const pane = canvas.querySelector('.react-flow__pane') as HTMLElement;
    const event = new MouseEvent('dblclick', { bubbles: true, cancelable: true, button: 0, clientX: 500, clientY: 300 });
    Object.defineProperty(event, 'view', { value: window });
    pane.dispatchEvent(event);
    // The zoom of a double click would run in a transition of 250 ms.
    await act(() => new Promise((resolve) => setTimeout(resolve, 400)));
    expect((canvas.querySelector('.react-flow__viewport') as HTMLElement).style.transform).toBe('translate(192px,16px) scale(1)');
  });

  it('a mini map named for assistive technologies, its steps in a token of the charter', async () => {
    const canvas = await showMap(sampleMap());
    const minimap = canvas.querySelector('.react-flow__minimap') as HTMLElement;
    expect(within(minimap).getByTitle('Mini-carte')).toBeInTheDocument();
    const fills = [...minimap.querySelectorAll('.react-flow__minimap-node')].map((node) => (node as SVGElement).style.fill);
    expect(fills.filter((fill) => fill === 'var(--minimap-node)')).toHaveLength(4);
  });

  it('dots every 24 px in the colour of the token --canvas-dot', async () => {
    const canvas = await showMap(sampleMap());
    const background = canvas.querySelector('svg.react-flow__background') as SVGElement;
    expect(background.querySelector('pattern')?.getAttribute('width')).toBe('24');
    expect(background.style.getPropertyValue('--xy-background-pattern-color-props')).toBe('var(--canvas-dot)');
  });
});

describe('the view (B-3.1-09, B-7-09)', () => {
  it('opens at the start of the map, x 192 and y 16 at zoom 1, and writes nothing in the browser', async () => {
    const storage = memoryStorage();
    const canvas = await showMap(sampleMap(), storage);
    expect((canvas.querySelector('.react-flow__viewport') as HTMLElement).style.transform).toBe('translate(192px,16px) scale(1)');
    expect(storage.written.size).toBe(0);
  });
});
