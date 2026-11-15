// textSize.test.ts
// The sizes of the label box of a flow and of the label under a gateway, as the routing keeps them clear (B-3.4-35, B-3.4-38, AR-57).

import { EDGE_LABEL, GATEWAY_LABEL } from './geometry/constants.ts';
import { canvasMeasure, edgeLabelSize, estimateMeasure, gatewayLabelSize, wrapLines, type TextMeasure } from './textSize.ts';

/** Six pixels a character, whatever the font: sizes that can be computed by hand. */
const six: TextMeasure = (text) => text.length * 6;
const font = { size: 11, weight: 500 };

describe('the wrapping of a text', () => {
  it('goes to the next line before a word that would pass the width', () => {
    expect(wrapLines('Valider la demande du client final', 96, six, font)).toEqual(['Valider la', 'demande du', 'client final']);
  });

  it('goes to the next line as soon as one pixel passes the width, and keeps a line that just holds', () => {
    expect(wrapLines('abcdefgh ijklmnop', 101, six, font)).toEqual(['abcdefgh', 'ijklmnop']);
    expect(wrapLines('abcdefgh ijklmnop', 102, six, font)).toEqual(['abcdefgh ijklmnop']);
  });

  it('cuts a word wider than the width, as the style sheet does', () => {
    expect(wrapLines('abcdefghij', 30, six, font)).toEqual(['abcde', 'fghij']);
  });

  it('gives no line for a blank text', () => {
    expect(wrapLines('   ', 96, six, font)).toEqual([]);
  });
});

describe('the label box of a flow (B-3.4-35, B-3.4-38)', () => {
  it('holds its text with the padding and the border, one line of 14 px', () => {
    expect(edgeLabelSize('oui', 0, six)).toEqual({ width: 14 + 18 + 1, height: 14 + 1 + 2 + 2 });
  });

  it('adds the document icon and its count after the text', () => {
    expect(edgeLabelSize('oui', 2, six)).toEqual({ width: 14 + 18 + 6 + 11 + 3 + 6 + 1, height: 19 });
    expect(edgeLabelSize('', 3, six)).toEqual({ width: 14 + 11 + 3 + 6 + 1, height: 19 });
  });

  it('is 110 px wide at most: a long text goes on several lines', () => {
    const size = edgeLabelSize('Valider la demande du client final', 0, six);
    expect(size).toEqual({ width: 14 + 72 + 1, height: 3 * 14 + 5 });
    const long = edgeLabelSize('Une étiquette très longue qui ne tient jamais sur une seule ligne de la boîte', 4, six);
    expect(long?.width).toBeLessThanOrEqual(EDGE_LABEL.maxWidth);
  });

  it('has no box without a text nor documents', () => {
    expect(edgeLabelSize('  ', 0, six)).toBeNull();
  });
});

describe('the label written under a gateway (AR-57)', () => {
  it('holds its text and its side padding, on lines of 15 px', () => {
    expect(gatewayLabelSize('Conforme ?', six)).toEqual({ width: 60 + 8 + 1, height: 15 });
  });

  it('keeps two lines at most, 200 px wide at most', () => {
    const size = gatewayLabelSize('Le dossier est-il complet et signé par toutes les personnes concernées par la demande initiale ?', six);
    expect(size?.height).toBe(GATEWAY_LABEL.lines * GATEWAY_LABEL.lineHeight);
    expect(size?.width).toBeLessThanOrEqual(GATEWAY_LABEL.maxWidth);
  });

  it('has nothing to keep clear without a label', () => {
    expect(gatewayLabelSize('', six)).toBeUndefined();
  });
});

describe('the measure of the browser', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('writes the text in the font of the page on a canvas, once for each text', () => {
    const measureText = vi.fn((text: string) => ({ width: text.length * 7 }) as TextMetrics);
    const pen = { font: '', measureText };
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(pen as unknown as CanvasRenderingContext2D);
    const measure = canvasMeasure();
    expect(measure('abc', font)).toBe(21);
    expect(measure('abc', font)).toBe(21);
    expect(measureText).toHaveBeenCalledTimes(1);
    expect(pen.font).toMatch(/^500 11px 'Inter Variable'/);
  });

  it('estimates the width where no canvas is available', () => {
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
    expect(canvasMeasure()).toBe(estimateMeasure);
    expect(estimateMeasure('abcd', { size: 10, weight: 500 })).toBeCloseTo(22.4);
  });
});
