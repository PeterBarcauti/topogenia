// mapRaster.ts
// A map drawn as an image for the PDF of the project: the window is the whole viewBox of its image SVG, its pixels the room of the image on an A3 landscape page at 150 dpi, the proportions kept; the painter, injected, is asked once per map (ADR 113).

import { imagePlacement, type MapImage } from '../pdf.ts';
import type { WindowPainter } from '../wallPdf.ts';
import { PAPERS, pixelsOf, type Box, type WallPage } from '../wallPrint.ts';

/** The viewBox of an image SVG: its window on the map, in the units of the map, its origin kept even when negative. */
export function viewBoxOf(svg: string): Box {
  const root = /<svg\b[^>]*>/.exec(svg)?.[0] ?? '';
  const values = (/\sviewBox="([^"]*)"/.exec(root)?.[1] ?? '').trim().split(/[\s,]+/).map(Number);
  const [x, y, width, height] = values;
  if (values.length !== 4 || x === undefined || y === undefined || width === undefined || height === undefined || values.some((value) => !Number.isFinite(value)) || width <= 0 || height <= 0) {
    throw new Error('The image has no window.');
  }
  return { x, y, width, height };
}

/**
 * The single page a whole map is drawn on: its window the whole viewBox, its area the room of the image on an A3 landscape
 * page, its pixels that room at the resolution of the print; no cut mark. It is the page the painter of the poster is asked.
 */
export function rasterPage(svg: string): WallPage {
  const window = viewBoxOf(svg);
  const area = imagePlacement(PAPERS.a3, window);
  const pixels = { width: pixelsOf(area.width), height: pixelsOf(area.height) };
  return { name: 'A1', row: 0, column: 0, number: 1, window, area: { ...area }, pixels, marks: [] };
}

/** The image of a map: the picture the painter gives for its whole window, and the size of the map for the fit on the page. */
export async function mapRaster(svg: string, paint: WindowPainter): Promise<MapImage> {
  const page = rasterPage(svg);
  return { dataUrl: await paint(page), width: page.window.width, height: page.window.height };
}
