// office.ts
// A Word file built in memory for the journeys of the files joined to the assistant: the least archive Office writes, its entries stored without compression (spec 3.0 section 3.4, lot 4).

import { crc32 } from 'node:zlib';

/** An archive of the entries given, stored: the local headers, the contents, the central directory and its end. */
function archive(entries: readonly { readonly name: string; readonly content: string }[]): Buffer {
  const locals: Buffer[] = [];
  const centrals: Buffer[] = [];
  let offset = 0;
  for (const entry of entries) {
    const name = Buffer.from(entry.name, 'utf8');
    const content = Buffer.from(entry.content, 'utf8');
    const crc = crc32(content);
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0x0800, 6);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(content.length, 18);
    local.writeUInt32LE(content.length, 22);
    local.writeUInt16LE(name.length, 26);
    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(0x0800, 8);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(content.length, 20);
    central.writeUInt32LE(content.length, 24);
    central.writeUInt16LE(name.length, 28);
    central.writeUInt32LE(offset, 42);
    locals.push(local, name, content);
    centrals.push(central, name);
    offset += local.length + name.length + content.length;
  }
  const directory = Buffer.concat(centrals);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(entries.length, 8);
  end.writeUInt16LE(entries.length, 10);
  end.writeUInt32LE(directory.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...locals, directory, end]);
}

const escape = (text: string) => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');

/** A Word file of the paragraphs given. */
export function docx(paragraphs: readonly string[]): Buffer {
  const namespace = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main';
  const relations = 'http://schemas.openxmlformats.org/package/2006/relationships';
  const officeDocument = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument';
  const body = paragraphs.map((text) => `<w:p><w:r><w:t>${escape(text)}</w:t></w:r></w:p>`).join('');
  return archive([
    { name: '_rels/.rels', content: `<Relationships xmlns="${relations}"><Relationship Id="rId1" Type="${officeDocument}" Target="word/document.xml"/></Relationships>` },
    { name: 'word/document.xml', content: `<w:document xmlns:w="${namespace}"><w:body>${body}</w:body></w:document>` },
  ]);
}
