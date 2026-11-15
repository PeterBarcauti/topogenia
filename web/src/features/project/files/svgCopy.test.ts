// svgCopy.test.ts
// The copy an image SVG carries (plan 3.1, L7): written in base64 of UTF-8 in its metadata, read back whole whatever its letters and its length; a SVG without it, of another tool or not readable refused plainly; a SVG with a script read without anything of it run nor put in the page.

import { base64Of, COPY_ID, copyMetadata, pmapOfSvg } from './svgCopy.ts';
import { FORMAT, TableError } from './tableImport.ts';

const svgOf = (inside: string): string => `<?xml version="1.0" encoding="UTF-8"?>\n<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">${inside}<rect width="10" height="10"/></svg>`;

describe('the copy an image carries (plan 3.1, L7)', () => {
  it('is read back whole: its accents, its letters of every plane, its quotes and signs of XML, its length', () => {
    const pmap = JSON.stringify({ format: FORMAT, project: { name: 'Achats « été » \u{1F600} 中文 <a> & "b"' }, filler: 'x'.repeat(200_000) });
    expect(pmapOfSvg(svgOf(copyMetadata(pmap)))).toBe(pmap);
    expect(base64Of('é')).toBe('w6k=');
    expect(copyMetadata('{}')).toBe(`<metadata id="${COPY_ID}">e30=</metadata>`);
  });

  it('refuses plainly a SVG without a copy, with a copy of another tool or damaged, and a file that is no SVG', () => {
    const refusals = [
      svgOf(''), svgOf('<metadata id="autre">e30=</metadata>'), svgOf(`<metadata id="${COPY_ID}">pas du base64 !</metadata>`), svgOf(`<metadata id="${COPY_ID}">/w==</metadata>`),
      '<svg xmlns="http://www.w3.org/2000/svg"><rect', '<html><body>x</body></html>',
    ].map((text) => {
      try {
        pmapOfSvg(text);
        return null;
      } catch (failure) {
        return failure instanceof TableError ? failure.key : String(failure);
      }
    });
    expect(refusals).toEqual(['files.svg.no_copy', 'files.svg.no_copy', 'files.svg.no_copy', 'files.svg.no_copy', 'files.svg.unreadable', 'files.svg.unreadable']);
  });

  it('reads a SVG that carries a script without running anything of it, nor putting it in the page', () => {
    const before = document.body.innerHTML;
    const text = svgOf(`<script>window.__ran = true;</script><image href="x" onerror="window.__ran = true"/>${copyMetadata('{"a":1}')}`).replace('<svg ', '<svg onload="window.__ran = true" ');
    expect(pmapOfSvg(text)).toBe('{"a":1}');
    expect([(window as unknown as { __ran?: boolean }).__ran, document.body.innerHTML]).toEqual([undefined, before]);
  });
});
