// icon.mjs
// Writes the icon of the executable (src/Topogenia.Host/Topogenia.ico) from the logos of the Store package, the unplated ones of each size, as PNG images in an ICO file (F-69, ADR 090): the Explorer, the taskbar and the notification area show it.
// node build/store/icon.mjs [--check]

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const sizes = [16, 20, 24, 32, 40, 48, 64, 256];
const target = join(root, 'src', 'Topogenia.Host', 'Topogenia.ico');

/** The PNG of the logo of one size, checked: its signature and its width and height as written in its header. */
function logo(size) {
  const file = join(root, 'build', 'store', 'Assets', `Square44x44Logo.altform-unplated_targetsize-${size}.png`);
  const png = readFileSync(file);
  const signature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
  if (!png.subarray(0, 8).equals(signature) || png.readUInt32BE(16) !== size || png.readUInt32BE(20) !== size)
    throw new Error(`${file} is not a PNG of ${size} x ${size} pixels`);
  return png;
}

/** The ICO file: its header, one entry per image (a size of 256 is written 0), then the PNG images in the same order. */
function icon() {
  const images = sizes.map(logo);
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(images.length, 4);
  let offset = header.length + 16 * images.length;
  const entries = images.map((png, index) => {
    const entry = Buffer.alloc(16);
    entry.writeUInt8(sizes[index] % 256, 0);
    entry.writeUInt8(sizes[index] % 256, 1);
    entry.writeUInt16LE(1, 4);
    entry.writeUInt16LE(32, 6);
    entry.writeUInt32LE(png.length, 8);
    entry.writeUInt32LE(offset, 12);
    offset += png.length;
    return entry;
  });
  return Buffer.concat([header, ...entries, ...images]);
}

const written = icon();
if (process.argv.includes('--check')) {
  if (!readFileSync(target).equals(written)) {
    console.error(`${target} is not the icon of the logos: run node build/store/icon.mjs`);
    process.exit(1);
  }
  console.log(`${target} is the icon of the logos (${sizes.join(', ')} pixels)`);
} else {
  writeFileSync(target, written);
  console.log(`${target} written (${sizes.join(', ')} pixels, ${written.length} bytes)`);
}
