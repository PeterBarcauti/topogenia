// download.ts
// Files given to the browser to save: the name of an exported file by the rule of the host, and the download itself (A-1.2-16).

const SHORT_NAME_MAX = 30;

/** The media type of an Excel workbook (`.xlsx`), built in the browser and read back. */
export const WORKBOOK_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

const LIGATURES: Readonly<Record<string, string>> = { 'œ': 'oe', 'Œ': 'OE', 'æ': 'ae', 'Æ': 'AE', 'ß': 'ss' };

/**
 * The code and the short name of a label, as the host names its exported files (`ExportFileName.ShortName`): letters and
 * digits without their accents, spaces, dashes and underscores as one dash, ligatures written out, any other character
 * dropped, 30 characters at most never ending with a dash; the code alone for an empty short name.
 */
export function shortName(code: string, label: string): string {
  let kept = '';
  for (const char of label.normalize('NFD')) {
    if (/\p{Mn}/u.test(char)) continue;
    if (/[A-Za-z0-9]/.test(char)) kept += char;
    else if (char === ' ' || char === '-' || char === '_') kept += '-';
    else kept += LIGATURES[char] ?? '';
  }
  let short = kept.replace(/-{2,}/g, '-').replace(/^-+|-+$/g, '');
  if (short.length > SHORT_NAME_MAX) short = short.slice(0, SHORT_NAME_MAX).replace(/-+$/, '');
  return short === '' ? code : `${code}_${short}`;
}

/** The name of an exported file: `CODE_Short-name_yyyy-mm-dd.ext`, the UTC day as the host writes it (D08). */
export function exportFileName(code: string, label: string, extension: string, at: Date = new Date()): string {
  return `${shortName(code, label)}_${at.toISOString().slice(0, 10)}${extension}`;
}

/** Gives a file to the browser to save under a name. */
export function download(content: Blob, name: string): void {
  const url = URL.createObjectURL(content);
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => {
    URL.revokeObjectURL(url);
  }, 10_000);
}
