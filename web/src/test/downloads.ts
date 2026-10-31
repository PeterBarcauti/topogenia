// downloads.ts
// Captures the files the interface gives the browser to save, for the tests (jsdom saves nothing and has no object URLs).

export interface Downloaded {
  readonly name: string;
  readonly content: Blob;
}

/** Records each download (its name and content) until the returned function restores the browser; revoked URLs too. */
export function captureDownloads(): { readonly files: Downloaded[]; readonly revoked: string[]; readonly restore: () => void } {
  const files: Downloaded[] = [];
  const revoked: string[] = [];
  const blobs = new Map<string, Blob>();
  const properties = [[URL, 'createObjectURL'], [URL, 'revokeObjectURL'], [HTMLAnchorElement.prototype, 'click']] as const;
  const original = properties.map(([owner, name]) => Object.getOwnPropertyDescriptor(owner, name));
  URL.createObjectURL = (content: Blob | MediaSource) => {
    const url = `blob:fake/${blobs.size + 1}`;
    // The body of a fetch reply is a Blob of Node, not of jsdom: kept whatever its class.
    blobs.set(url, content as Blob);
    return url;
  };
  URL.revokeObjectURL = (url: string) => {
    revoked.push(url);
  };
  HTMLAnchorElement.prototype.click = function click(this: HTMLAnchorElement) {
    const content = blobs.get(this.href);
    if (content !== undefined) files.push({ name: this.download, content });
  };
  return {
    files,
    revoked,
    restore: () => {
      properties.forEach(([owner, name], index) => {
        const descriptor = original[index];
        if (descriptor === undefined) Reflect.deleteProperty(owner, name);
        else Object.defineProperty(owner, name, descriptor);
      });
    },
  };
}
