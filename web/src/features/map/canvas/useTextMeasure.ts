// useTextMeasure.ts
// The measure of the texts of the canvas, made again once the fonts of the page are loaded, so that the label boxes fit the text drawn.

import { useEffect, useState } from 'react';
import { canvasMeasure, type TextMeasure } from '../model/textSize.ts';

export function useTextMeasure(): TextMeasure {
  const [measure, setMeasure] = useState<TextMeasure>(() => canvasMeasure());
  useEffect(() => {
    if (!('fonts' in document)) return undefined;
    let live = true;
    void document.fonts.ready.then(() => {
      if (live) setMeasure(() => canvasMeasure());
    });
    return () => {
      live = false;
    };
  }, []);
  return measure;
}
