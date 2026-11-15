// svgCopy.ts
// The copy of a map that its image SVG may carry (plan 3.1, L7, the practice of draw.io): the text of its .pmap in base64 of UTF-8 in a metadata of its own, written at the export, read back at the opening by DOMParser as a document of image/svg+xml, never put in the page, nothing of it run.

import { FORMAT, TableError } from './tableImport.ts';

/** The identifier of the metadata that carries the copy: the marker of the files of the application. */
export const COPY_ID = FORMAT;

/** A text in base64 of its UTF-8, made by pieces: a long text never passed whole as arguments. */
export function base64Of(text: string): string {
  const bytes = new TextEncoder().encode(text);
  let binary = '';
  for (let start = 0; start < bytes.length; start += 0x8000) binary += String.fromCharCode(...bytes.subarray(start, start + 0x8000));
  return btoa(binary);
}

/** The metadata of the copy, to put in the SVG: its base64 needs no escaping in XML. */
export const copyMetadata = (pmap: string): string => `<metadata id="${COPY_ID}">${base64Of(pmap)}</metadata>`;

/**
 * The text of the .pmap an image of the application carries: the SVG read as a document of its own, only its metadata read.
 * TableError for a SVG not readable, or for one without a copy (an image exported without the option, or of another tool).
 */
export function pmapOfSvg(text: string): string {
  const document = new DOMParser().parseFromString(text, 'image/svg+xml');
  if (document.getElementsByTagName('parsererror').length > 0 || document.documentElement.localName !== 'svg') throw new TableError('files.svg.unreadable');
  const metadata = [...document.getElementsByTagName('metadata')].find((element) => element.getAttribute('id') === COPY_ID);
  const data = (metadata?.textContent ?? '').replace(/\s+/g, '');
  if (data === '') throw new TableError('files.svg.no_copy');
  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(Uint8Array.from(atob(data), (char) => char.charCodeAt(0)));
  } catch (failure) {
    throw new TableError('files.svg.no_copy', {}, { cause: failure });
  }
}
