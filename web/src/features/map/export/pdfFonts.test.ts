// pdfFonts.test.ts
// The faces of Inter read for the PDF: the two TrueType files of the package, read from the application when a PDF is made, as the binary strings of jsPDF (AR-52).

import { binaryString, pdfFonts } from './pdfFonts.ts';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the faces of Inter for the PDF (AR-52)', () => {
  it('turns the bytes into one character a byte, past the slices of 32 768', () => {
    const bytes = Uint8Array.from({ length: 70_000 }, (_, index) => index % 256);
    const text = binaryString(bytes);
    expect(text.length).toBe(70_000);
    expect(Array.from(text.slice(0, 256), (character) => character.charCodeAt(0))).toEqual(Array.from({ length: 256 }, (_, index) => index));
    expect([text.charCodeAt(32_768), text.charCodeAt(69_999)]).toEqual([32_768 % 256, 69_999 % 256]);
  });

  it('reads the regular and the bold faces of the package from the application, once a PDF is asked', async () => {
    const asked: string[] = [];
    vi.stubGlobal('fetch', (address: string) => {
      asked.push(address);
      return Promise.resolve(new Response(new Uint8Array([0, 1, 0, 0, address.includes('Bold') ? 2 : 1])));
    });
    const fonts = await pdfFonts();
    expect(asked.map((address) => address.split('?')[0]?.split('/').slice(-3).join('/'))).toEqual(['inter-font/ttf/Inter-Regular.ttf', 'inter-font/ttf/Inter-Bold.ttf']);
    expect([fonts.regular, fonts.bold]).toEqual([String.fromCharCode(0, 1, 0, 0, 1), String.fromCharCode(0, 1, 0, 0, 2)]);
  });

  it('a file that cannot be read stops the PDF', async () => {
    vi.stubGlobal('fetch', () => Promise.resolve(new Response('', { status: 404 })));
    await expect(pdfFonts()).rejects.toThrow('The font file cannot be read (404).');
  });
});
