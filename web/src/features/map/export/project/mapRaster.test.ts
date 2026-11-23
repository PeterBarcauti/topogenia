// mapRaster.test.ts
// A map drawn as an image for the PDF of the project, read back: the window is the whole viewBox of its image SVG, negative origin kept; its pixels are the room of the image on an A3 landscape page at 150 dpi, the proportions kept; the painter is asked once per map and its canvas is released; the image SVG is made without any editor.

import { i18next } from '../../../../shared/i18n/i18n.ts';
import { sampleMap } from '../../../../test/maps.ts';
import { svgText, windowPainter } from '../../state/actions/exports.ts';
import { imagePlacement, PDF_MARGIN } from '../pdf.ts';
import { PAPERS, WALL_DPI, type WallPage } from '../wallPrint.ts';
import { mapRaster, rasterPage, viewBoxOf } from './mapRaster.ts';

/** A white pixel. */
const PIXEL = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

/** An image SVG of a size and a viewBox. */
const svgOf = (viewBox: string, width = 1000, height = 500): string => `<?xml version="1.0"?>\n<svg xmlns="http://www.w3.org/2000/svg" width="${String(width)}" height="${String(height)}" viewBox="${viewBox}"><title>x</title></svg>`;

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('the window of a whole map', () => {
  it('is its own viewBox, negative origin kept', () => {
    expect(viewBoxOf(svgOf('-120 -40 1000 500'))).toEqual({ x: -120, y: -40, width: 1000, height: 500 });
    expect(rasterPage(svgOf('-120 -40.5 1000 500')).window).toEqual({ x: -120, y: -40.5, width: 1000, height: 500 });
    expect(viewBoxOf(svgOf('0 0 1000 500'))).toEqual({ x: 0, y: 0, width: 1000, height: 500 });
  });

  it('is refused for an image without a viewBox', () => {
    expect(() => viewBoxOf('<svg xmlns="http://www.w3.org/2000/svg"></svg>')).toThrow();
  });
});

describe('the pixels of a map', () => {
  it.each([['0 0 2000 500'], ['0 0 800 900'], ['-50 -50 1000 1000']])('are those of the room of the image on an A3 landscape page at 150 dpi, the proportions kept (%s)', (viewBox) => {
    const svg = svgOf(viewBox);
    const page = rasterPage(svg);
    const window = viewBoxOf(svg);
    const room = imagePlacement(PAPERS.a3, window);
    expect(page.area).toEqual(room);
    expect(page.pixels).toEqual({ width: Math.round((room.width / 25.4) * WALL_DPI), height: Math.round((room.height / 25.4) * WALL_DPI) });
    expect(page.pixels.width / page.pixels.height).toBeCloseTo(window.width / window.height, 1);
    expect(page.marks).toEqual([]);
  });

  it('fill the width of the room for a wide map and its height for a tall one', () => {
    const [wide, tall] = [rasterPage(svgOf('0 0 2000 500')), rasterPage(svgOf('0 0 500 2000'))];
    expect(wide.area.width).toBeCloseTo(PAPERS.a3.width - 2 * PDF_MARGIN, 5);
    expect(wide.area.height).toBeLessThan(wide.area.width / 3);
    expect(tall.area.height).toBeCloseTo(PAPERS.a3.height - (PDF_MARGIN + 17) - PDF_MARGIN - 6, 5);
    expect(tall.area.width).toBeLessThan(tall.area.height / 3);
  });
});

describe('the image of a map', () => {
  it('asks the painter once, with the page of the whole map, and gives its picture with the size of the map', async () => {
    const asked: WallPage[] = [];
    const image = await mapRaster(svgOf('0 0 1000 500'), (page) => {
      asked.push(page);
      return Promise.resolve(PIXEL);
    });
    expect(asked).toEqual([rasterPage(svgOf('0 0 1000 500'))]);
    expect(image).toEqual({ dataUrl: PIXEL, width: 1000, height: 500 });
  });

  it('is refused whole when the painter fails, nothing kept', async () => {
    await expect(mapRaster(svgOf('0 0 1000 500'), () => Promise.reject(new Error('broken')))).rejects.toThrow('broken');
  });

  it('is drawn by the painter of the poster, whose canvas is released once its picture is taken', async () => {
    vi.stubGlobal('Image', class { src = ''; decode = () => Promise.resolve(); });
    const drawn: { width: number; height: number }[] = [];
    const context = { fillStyle: '', fillRect: vi.fn(), drawImage: (_i: unknown, _x: number, _y: number, width: number, height: number) => { drawn.push({ width, height }); } };
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(context as unknown as CanvasRenderingContext2D);
    const canvases: HTMLCanvasElement[] = [];
    const sizes: number[] = [];
    vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockImplementation(function (this: HTMLCanvasElement) {
      canvases.push(this);
      sizes.push(this.width);
      return PIXEL;
    });
    const svg = svgOf('0 0 1000 500');
    const image = await mapRaster(svg, windowPainter(svg, '#fff'));
    expect(image.dataUrl).toBe(PIXEL);
    const { pixels } = rasterPage(svg);
    expect(sizes).toEqual([pixels.width]);
    expect(drawn).toEqual([pixels]);
    expect(canvases).toHaveLength(1);
    expect(canvases[0]?.width).toBe(0);
  });
});

describe('the image SVG without an editor', () => {
  it('is made from the map, the preferences and the rows given, and carries the lanes of the map', async () => {
    const map = sampleMap();
    const setup = { preferences: { showCodes: true, alwaysShowPictos: false, showRaci: false, showValueStream: false }, rows: {} };
    const { text, size } = await svgText(map, i18next.language, { title: 'VIS - Visites', caption: 'Classification : Interne', editable: false, analysis: false }, setup);
    expect(text).toContain('<svg');
    expect(text).toContain('<title>VIS - Visites</title>');
    expect(viewBoxOf(text)).toEqual({ x: 0, y: 0, width: size.width, height: size.height });
  });

  it('follows the preferences it is given: the codes of the steps are written or not', async () => {
    const map = sampleMap();
    const facts = { title: 'VIS', caption: 'c', editable: false, analysis: false };
    const rows = {};
    const first = map.steps[0]?.code ?? '';
    const withCodes = await svgText(map, i18next.language, facts, { preferences: { showCodes: true, alwaysShowPictos: false, showRaci: false, showValueStream: false }, rows });
    const without = await svgText(map, i18next.language, facts, { preferences: { showCodes: false, alwaysShowPictos: false, showRaci: false, showValueStream: false }, rows });
    expect(first).not.toBe('');
    expect(withCodes.text).toContain(`>${first}<`);
    expect(without.text).not.toContain(`>${first}<`);
  });
});
