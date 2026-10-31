// pdfFonts.ts
// The faces of Inter for the PDF (AR-52): the TrueType files of the package inter-font, served with the application and read only when a PDF is made.

import boldAddress from 'inter-font/ttf/Inter-Bold.ttf?url';
import regularAddress from 'inter-font/ttf/Inter-Regular.ttf?url';
import type { PdfFonts } from './pdf.ts';

/** The bytes turned into characters by slices: one call per byte would overflow the stack of the arguments. */
const SLICE = 0x8000;

/** The bytes of a file as the binary string jsPDF keeps in its files: one character a byte. */
export function binaryString(bytes: Uint8Array): string {
  let text = '';
  for (let start = 0; start < bytes.length; start += SLICE) text += String.fromCharCode(...bytes.subarray(start, start + SLICE));
  return text;
}

/** A file of the application, from its own address (`connect-src 'self'` of the policy of the host). */
async function read(address: string): Promise<string> {
  const reply = await fetch(address);
  if (!reply.ok) throw new Error(`The font file cannot be read (${String(reply.status)}).`);
  return binaryString(new Uint8Array(await reply.arrayBuffer()));
}

/** Both faces, read when a PDF is made: the files are not loaded with the application (B-0-04). */
export async function pdfFonts(): Promise<PdfFonts> {
  const [regular, bold] = await Promise.all([read(regularAddress), read(boldAddress)]);
  return { regular, bold };
}
