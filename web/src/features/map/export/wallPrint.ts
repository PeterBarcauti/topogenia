// wallPrint.ts
// The map printed large for a wall (Makigami M9, plan 3.1): the poster cut into pages A4 or A3 landscape, the number of pages across chosen and the height following, 10 mm shared by two neighbours, each page named A1, A2…, B1… (its row, its column), its window on the map, the cut marks at the corners of its printed area and the ticks where its neighbours start; pure functions, nothing drawn.

/** The sheets of paper, landscape, in millimetres. */
export const PAPERS = { a4: { width: 297, height: 210 }, a3: { width: 420, height: 297 } } as const;

export type Paper = keyof typeof PAPERS;

/** The margin left round the printed area, which every office printer prints within, in millimetres. */
export const WALL_MARGIN = 10;

/** The band under the printed area for the name of the page, of the map and the classification (S-4.1-08). */
export const WALL_FOOTER = 6;

/** What two neighbour pages share, in millimetres: the strip one covers of the other once glued. */
export const WALL_OVERLAP = 10;

/** The pages across that may be chosen. */
export const ACROSS_MIN = 1;
export const ACROSS_MAX = 10;

/** The most pages a print holds: beyond, fewer pages across are asked. */
export const WALL_PAGES_MAX = 100;

/** A cut mark: its length and its gap from the corner it marks, in the margin, in millimetres. */
export const CUT_MARK = { length: 4, gap: 1 } as const;

/** Millimetres in a pixel of the map shown at 100 % (a CSS pixel, 1/96 inch). */
export const MM_PER_PIXEL = 25.4 / 96;

/** The resolution of the image of each page, in dots per inch: sharp from a step away. */
export const WALL_DPI = 150;

/** A size, in the unit its use says. */
export interface Size {
  readonly width: number;
  readonly height: number;
}

/** A box: on the map in its own units, or on the page in millimetres. */
export interface Box extends Size {
  readonly x: number;
  readonly y: number;
}

/** A line on the page, in millimetres. */
export interface Segment {
  readonly x1: number;
  readonly y1: number;
  readonly x2: number;
  readonly y2: number;
}

/** A page of the poster. */
export interface WallPage {
  /** « A1 »: the letter of its row, the number of its column. */
  readonly name: string;
  readonly row: number;
  readonly column: number;
  /** Its rank in the reading order, from 1. */
  readonly number: number;
  /** The part of the map it prints, in the units of the map. */
  readonly window: Box;
  /** Where it prints it on the page, in millimetres; the last row and column print only what is left of the map. */
  readonly area: Box;
  /** The size of its image in pixels, at the resolution of the print. */
  readonly pixels: Size;
  /** The cut marks at the corners of its area and the ticks where its right and lower neighbours start. */
  readonly marks: readonly Segment[];
}

/** The poster: its paper, its pages across and down, its scale, its size on the wall, its pages in the reading order. */
export interface WallPlan {
  readonly paper: Paper;
  readonly across: number;
  readonly down: number;
  /** Millimetres of paper for a unit of the map. */
  readonly scale: number;
  /** The scale against the map shown at 100 %, in percent, rounded. */
  readonly percent: number;
  /** The size of the poster once assembled, in millimetres. */
  readonly wall: Size;
  readonly pages: readonly WallPage[];
  /** More pages than a print holds (WALL_PAGES_MAX): no page is laid out, fewer pages across are asked. */
  readonly tooMany: boolean;
}

/** The printed area of a page: the sheet without its margins and the band of its footer. */
export function printedArea(paper: Paper): Size {
  const sheet = PAPERS[paper];
  return { width: sheet.width - 2 * WALL_MARGIN, height: sheet.height - 2 * WALL_MARGIN - WALL_FOOTER };
}

/** The pages a length of the poster takes, each sharing its overlap with the next one. */
export function pagesFor(length: number, room: number): number {
  return length <= room ? 1 : Math.ceil((length - WALL_OVERLAP) / (room - WALL_OVERLAP) - 1e-9);
}

/** The letters of a row as a spreadsheet names its columns: A to Z, then AA, AB… */
export function rowName(index: number): string {
  let name = '';
  for (let rest = index + 1; rest > 0; rest = Math.floor((rest - 1) / 26)) name = String.fromCharCode(65 + ((rest - 1) % 26)) + name;
  return name;
}

/** The pages across, bounded to those that may be chosen and made whole. */
export const boundedAcross = (across: number): number => Math.min(ACROSS_MAX, Math.max(ACROSS_MIN, Math.round(Number.isFinite(across) ? across : ACROSS_MIN)));

/** Pixels of the print for a length in millimetres. */
const pixelsOf = (millimetres: number): number => Math.max(1, Math.round((millimetres / 25.4) * WALL_DPI));

/** The cut marks of an area: at each corner, a line along each side, in the margin, a gap away from the corner. */
function cutMarks(area: Box): Segment[] {
  const { length, gap } = CUT_MARK;
  const [left, top, right, bottom] = [area.x, area.y, area.x + area.width, area.y + area.height];
  return [
    { x1: left - gap - length, y1: top, x2: left - gap, y2: top }, { x1: left, y1: top - gap - length, x2: left, y2: top - gap },
    { x1: right + gap, y1: top, x2: right + gap + length, y2: top }, { x1: right, y1: top - gap - length, x2: right, y2: top - gap },
    { x1: left - gap - length, y1: bottom, x2: left - gap, y2: bottom }, { x1: left, y1: bottom + gap, x2: left, y2: bottom + gap + length },
    { x1: right + gap, y1: bottom, x2: right + gap + length, y2: bottom }, { x1: right, y1: bottom + gap, x2: right, y2: bottom + gap + length },
  ];
}

/** The ticks in the margins where the right neighbour and the lower one start, so that their edges are laid on them. */
function neighbourTicks(area: Box, right: boolean, below: boolean): Segment[] {
  const { length, gap } = CUT_MARK;
  const ticks: Segment[] = [];
  if (right) {
    const x = area.x + area.width - WALL_OVERLAP;
    ticks.push({ x1: x, y1: area.y - gap - length, x2: x, y2: area.y - gap }, { x1: x, y1: area.y + area.height + gap, x2: x, y2: area.y + area.height + gap + length });
  }
  if (below) {
    const y = area.y + area.height - WALL_OVERLAP;
    ticks.push({ x1: area.x - gap - length, y1: y, x2: area.x - gap, y2: y }, { x1: area.x + area.width + gap, y1: y, x2: area.x + area.width + gap + length, y2: y });
  }
  return ticks;
}

/**
 * The poster of a map of a size (in its units) on a paper, a number of pages across: the width of the poster is those
 * pages less what they share, the scale follows from it and the pages down from the height; each page prints its window
 * from the top left corner of its printed area.
 */
export function wallPlan(map: Size, paper: Paper, across: number): WallPlan {
  const columns = boundedAcross(across);
  const room = printedArea(paper);
  const width = columns * room.width - (columns - 1) * WALL_OVERLAP;
  const scale = width / Math.max(1, map.width);
  const wall = { width, height: Math.max(1, map.height) * scale };
  const rows = pagesFor(wall.height, room.height);
  const [stepX, stepY] = [room.width - WALL_OVERLAP, room.height - WALL_OVERLAP];
  const percent = Math.round((scale / MM_PER_PIXEL) * 100);
  // Beyond what a print holds, the counts alone: the dialog asks fewer pages across.
  if (rows * columns > WALL_PAGES_MAX) return { paper, across: columns, down: rows, scale, percent, wall, pages: [], tooMany: true };
  const pages: WallPage[] = [];
  for (let row = 0; row < rows; row++) {
    for (let column = 0; column < columns; column++) {
      const [left, top] = [column * stepX, row * stepY];
      const area = { x: WALL_MARGIN, y: WALL_MARGIN, width: Math.min(room.width, wall.width - left), height: Math.min(room.height, wall.height - top) };
      pages.push({
        name: `${rowName(row)}${String(column + 1)}`, row, column, number: pages.length + 1,
        window: { x: left / scale, y: top / scale, width: area.width / scale, height: area.height / scale },
        area, pixels: { width: pixelsOf(area.width), height: pixelsOf(area.height) },
        marks: [...cutMarks(area), ...neighbourTicks(area, column < columns - 1, row < rows - 1)],
      });
    }
  }
  return { paper, across: columns, down: rows, scale, percent, wall, pages, tooMany: false };
}

/** The pages across offered first: the fewest that print the map at least as large as it is shown at 100 %, ten at most. */
export function defaultAcross(map: Size, paper: Paper): number {
  for (let across = ACROSS_MIN; across <= ACROSS_MAX; across++) if (wallPlan(map, paper, across).percent >= 100) return across;
  return ACROSS_MAX;
}
