// archive-dates.mjs
// Gives every entry of a ZIP or MSIX package the same conventional date, the release day of the product at midnight UTC, in its local and central headers and in their time fields, so that no date of an archive tells the hour nor the time zone of the machine of the build (pre-audit 68, P2-04); only the bytes of the dates change.
// node build/archive-dates.mjs <archive> [--check]: rewrites the dates, or (--check) writes nothing and fails when an entry has another date.

import { readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));

const EOCD = 0x06054b50;
const ZIP64_LOCATOR = 0x07064b50;
const ZIP64_EOCD = 0x06064b50;
const CENTRAL = 0x02014b50;
const LOCAL = 0x04034b50;
/** The fields of time an extra block may carry: the extended timestamp (Unix seconds), NTFS (FILETIME), Info-ZIP Unix. */
const EXTENDED_TIME = 0x5455;
const NTFS = 0x000a;
const UNIX = 0x5855;
const ZIP64 = 0x0001;
const FILETIME_OF_1970 = 116444736000000000n;

/** The conventional date: the release day of web/package.json, at midnight UTC. */
export function releaseDay() {
  const day = JSON.parse(readFileSync(join(root, 'web', 'package.json'), 'utf8')).releaseDate;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day ?? '')) throw new Error(`web/package.json has no releaseDate of the form YYYY-MM-DD (${day})`);
  return new Date(`${day}T00:00:00Z`);
}

/** The date in the form of a DOS header: the day and 00:00:00, which tells no zone (the field has none). */
function dosOf(date) {
  const year = date.getUTCFullYear();
  if (year < 1980 || year > 2107) throw new Error(`A ZIP date cannot hold the year ${year}`);
  return { time: 0, day: ((year - 1980) << 9) | ((date.getUTCMonth() + 1) << 5) | date.getUTCDate() };
}

/** The end of the central directory: where it starts and how many entries it has, through the ZIP64 records when the package has them. */
function directoryOf(bytes) {
  const lowest = Math.max(0, bytes.length - 22 - 0xffff);
  let at = bytes.length - 22;
  while (at >= lowest && bytes.readUInt32LE(at) !== EOCD) at -= 1;
  if (at < lowest) throw new Error('No end of central directory: not a ZIP archive');
  let count = bytes.readUInt16LE(at + 10);
  let start = bytes.readUInt32LE(at + 16);
  const locator = at - 20;
  if (locator >= 0 && bytes.readUInt32LE(locator) === ZIP64_LOCATOR) {
    const record = Number(bytes.readBigUInt64LE(locator + 8));
    if (bytes.readUInt32LE(record) !== ZIP64_EOCD) throw new Error('The ZIP64 locator does not point to its record');
    count = Number(bytes.readBigUInt64LE(record + 32));
    start = Number(bytes.readBigUInt64LE(record + 48));
  }
  return { count, start };
}

/** The offset of the local header of a central entry, read in its ZIP64 block when its field is full. */
function localOffsetOf(bytes, entry, extra, extraLength) {
  const offset = bytes.readUInt32LE(entry + 42);
  if (offset !== 0xffffffff) return offset;
  let at = extra;
  while (at + 4 <= extra + extraLength) {
    const id = bytes.readUInt16LE(at);
    const size = bytes.readUInt16LE(at + 2);
    if (id === ZIP64) {
      // The ZIP64 block holds, in order, only the fields full in the header: sizes first, then the offset.
      let field = at + 4;
      if (bytes.readUInt32LE(entry + 24) === 0xffffffff) field += 8;
      if (bytes.readUInt32LE(entry + 20) === 0xffffffff) field += 8;
      return Number(bytes.readBigUInt64LE(field));
    }
    at += 4 + size;
  }
  throw new Error('A full offset of a local header without its ZIP64 block');
}

/**
 * Each field of time of the extra blocks of a header, visited: `visit(kind, offset)` with `unix` (4 bytes of seconds) or
 * `filetime` (8 bytes). The blocks keep their size; only the values of time are read or written.
 */
function timesOf(bytes, extra, length, visit) {
  let at = extra;
  while (at + 4 <= extra + length) {
    const id = bytes.readUInt16LE(at);
    const size = bytes.readUInt16LE(at + 2);
    const body = at + 4;
    if (id === EXTENDED_TIME && size >= 1) {
      for (let field = body + 1; field + 4 <= body + size; field += 4) visit('unix', field);
    } else if (id === UNIX && size >= 8) {
      visit('unix', body);
      visit('unix', body + 4);
    } else if (id === NTFS) {
      let tag = body + 4;
      while (tag + 4 <= body + size) {
        const tagSize = bytes.readUInt16LE(tag + 2);
        if (bytes.readUInt16LE(tag) === 1) for (let field = tag + 4; field + 8 <= tag + 4 + tagSize; field += 8) visit('filetime', field);
        tag += 4 + tagSize;
      }
    }
    at += 4 + size;
  }
}

/** Every header of the archive: the central one of each entry and its local one, each with its fields of date. */
function headersOf(bytes) {
  const { count, start } = directoryOf(bytes);
  const headers = [];
  let entry = start;
  for (let index = 0; index < count; index += 1) {
    if (bytes.readUInt32LE(entry) !== CENTRAL) throw new Error(`Entry ${index} of the central directory is not one`);
    const nameLength = bytes.readUInt16LE(entry + 28);
    const extraLength = bytes.readUInt16LE(entry + 30);
    const commentLength = bytes.readUInt16LE(entry + 32);
    const name = bytes.toString('utf8', entry + 46, entry + 46 + nameLength);
    const centralExtra = entry + 46 + nameLength;
    const local = localOffsetOf(bytes, entry, centralExtra, extraLength);
    if (bytes.readUInt32LE(local) !== LOCAL) throw new Error(`The local header of ${name} is not one`);
    const localExtra = local + 30 + bytes.readUInt16LE(local + 26);
    headers.push({ name, dos: entry + 12, extra: centralExtra, extraLength });
    headers.push({ name, dos: local + 10, extra: localExtra, extraLength: bytes.readUInt16LE(local + 28) });
    entry = centralExtra + extraLength + commentLength;
  }
  return headers;
}

/** The entries whose date is not the one given, by name, in order. */
export function otherDates(bytes, date) {
  const { time, day } = dosOf(date);
  const seconds = Math.floor(date.getTime() / 1000);
  const filetime = BigInt(seconds) * 10000000n + FILETIME_OF_1970;
  const names = new Set();
  for (const header of headersOf(bytes)) {
    if (bytes.readUInt16LE(header.dos) !== time || bytes.readUInt16LE(header.dos + 2) !== day) names.add(header.name);
    timesOf(bytes, header.extra, header.extraLength, (kind, at) => {
      if (kind === 'unix' ? bytes.readUInt32LE(at) !== seconds : bytes.readBigUInt64LE(at) !== filetime) names.add(header.name);
    });
  }
  return [...names];
}

/** The archive with every date of its headers set to the one given; nothing else changes. */
export function withDate(bytes, date) {
  const copy = Buffer.from(bytes);
  const { time, day } = dosOf(date);
  const seconds = Math.floor(date.getTime() / 1000);
  const filetime = BigInt(seconds) * 10000000n + FILETIME_OF_1970;
  for (const header of headersOf(copy)) {
    copy.writeUInt16LE(time, header.dos);
    copy.writeUInt16LE(day, header.dos + 2);
    timesOf(copy, header.extra, header.extraLength, (kind, at) => {
      if (kind === 'unix') copy.writeUInt32LE(seconds, at);
      else copy.writeBigUInt64LE(filetime, at);
    });
  }
  return copy;
}

/** The date in the form of an HTTP header (RFC 9110), as the SDK writes `Last-Modified`: the release day, at midnight. */
export const httpDateOf = (date) => date.toUTCString();

/** Each value of a `Last-Modified` header in the endpoints of the static assets the SDK writes (`*.staticwebassets.endpoints.json`). */
const LAST_MODIFIED = /("Name"\s*:\s*"Last-Modified"\s*,\s*"Value"\s*:\s*")([^"]*)(")/g;

/**
 * The endpoints of the static assets with every `Last-Modified` set to the date given: the SDK writes there the hour each
 * file of wwwroot was built, which would tell the hour of the build (contre-visite 68, CV-P2-02); the ETag, made from the
 * content, still names each version of a file.
 */
export const withLastModified = (text, date) => text.replace(LAST_MODIFIED, `$1${httpDateOf(date)}$3`);

/** The values of `Last-Modified` that are not the date given. */
export const otherLastModified = (text, date) => [...text.matchAll(LAST_MODIFIED)].map((match) => match[2]).filter((value) => value !== httpDateOf(date));

/** Rewrites the dates of an archive in place, then reads it again: every entry must have the date. */
export function normalize(file, date = releaseDay()) {
  writeFileSync(file, withDate(readFileSync(file), date));
  const left = otherDates(readFileSync(file), date);
  if (left.length > 0) throw new Error(`${file}: entries keep another date: ${left.slice(0, 5).join(', ')}`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [file, option] = process.argv.slice(2);
  if (!file || (option !== undefined && option !== '--check')) {
    console.error('node build/archive-dates.mjs <archive> [--check]');
    process.exit(2);
  }
  const date = releaseDay();
  if (option === '--check') {
    const left = otherDates(readFileSync(file), date);
    if (left.length > 0) {
      console.error(`${file}: ${left.length} entries are not dated ${date.toISOString()}: ${left.slice(0, 10).join(', ')}`);
      process.exit(1);
    }
    console.log(`${file}: every entry dated ${date.toISOString()}`);
  } else {
    normalize(file, date);
    console.log(`${file}: every entry dated ${date.toISOString()}`);
  }
}
