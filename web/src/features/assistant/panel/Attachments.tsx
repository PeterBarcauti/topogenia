// Attachments.tsx
// The files joined to the request of the panel of the assistant: the paperclip of the field, the files dropped on the field or pasted in it, their pills with their kind, size and removal; a text file dropped is read into the field (spec 3.0 section 3.4, lot 4).

import { useRef, useState, type ClipboardEvent, type DragEvent } from 'react';
import { FileImage, FileSpreadsheet, FileText, Paperclip, Workflow, X, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore, type StoreApi } from 'zustand';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { notify } from '../../../shared/ui/toasts.ts';
import { base64Of, choose, extensionOf, isBinary, kindOf, sizeOf, TEXT_EXTENSIONS, type FileKind, type FileLabel, type JoinedFile } from './attachments.ts';
import { threadFiles, type AssistantState } from './assistantStore.ts';

const ICONS: Readonly<Record<FileKind, LucideIcon>> = { pdf: FileText, image: FileImage, word: FileText, excel: FileSpreadsheet, visio: Workflow };

/**
 * Joins files to the next request: those the bounds of the connector admit, read in base64; each one refused said in a
 * message (its former format, its kind, its size, the number or the whole of the files of the conversation).
 */
export function useFileJoin(store: StoreApi<AssistantState>): (candidates: readonly File[]) => Promise<void> {
  const { t, i18n } = useTranslation();
  return async (candidates) => {
    const state = store.getState();
    if (state.bounds === null || candidates.length === 0) return;
    const { accepted, refusals } = choose(candidates, threadFiles(state), state.bounds);
    for (const refusal of refusals) {
      const max = refusal.max === undefined ? undefined : new Intl.NumberFormat(i18n.language).format(refusal.max);
      notify(t(`assistant.panel.files.refused.${refusal.reason}`, { name: refusal.name, format: refusal.format, max }), 'error');
    }
    const read = await Promise.all(accepted.map(async (file): Promise<Omit<JoinedFile, 'id'> | null> => {
      try {
        return { name: file.name, size: file.size, kind: kindOf(file.name) ?? 'pdf', data: await base64Of(file) };
      } catch {
        notify(t('assistant.panel.files.refused.unreadable', { name: file.name }), 'error');
        return null;
      }
    }));
    const joined = read.filter((file) => file !== null);
    if (joined.length > 0) store.getState().addFiles(joined);
  };
}

/**
 * The field as a place to drop files: a file of text (a prompt prepared) is read into the field, the others are joined;
 * the field says it takes them while they are carried over it.
 */
export function useFileDrop(join: (files: readonly File[]) => Promise<void>, readText: (file: File) => Promise<void>, disabled: boolean) {
  const [over, setOver] = useState(false);
  const carriesFiles = (event: DragEvent) => !disabled && Array.from(event.dataTransfer.types).includes('Files');
  return {
    over,
    onDragOver: (event: DragEvent) => {
      if (!carriesFiles(event)) return;
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
      setOver(true);
    },
    onDragLeave: (event: DragEvent) => {
      if (!(event.relatedTarget instanceof Node && event.currentTarget.contains(event.relatedTarget))) setOver(false);
    },
    onDrop: (event: DragEvent) => {
      if (!carriesFiles(event)) return;
      event.preventDefault();
      setOver(false);
      const files = Array.from(event.dataTransfer.files);
      const text = files.find((file) => TEXT_EXTENSIONS.includes(extensionOf(file.name)));
      if (text !== undefined) void readText(text);
      void join(files.filter((file) => file !== text));
    },
  };
}

/** A file pasted in the field (a capture of the screen) is joined; a text pasted stays a text. */
export function pastedFiles(join: (files: readonly File[]) => Promise<void>) {
  return (event: ClipboardEvent<HTMLTextAreaElement>) => {
    const files = Array.from(event.clipboardData.files);
    if (files.length === 0) return;
    event.preventDefault();
    void join(files);
  };
}

/** The paperclip of the bar of the field: the chooser of the files of the kinds the connector admits. */
export function AttachButton({ store, join, disabled }: { store: StoreApi<AssistantState>; join: (files: readonly File[]) => Promise<void>; disabled: boolean }) {
  const { t } = useTranslation();
  const bounds = useStore(store, (state) => state.bounds);
  const chooser = useRef<HTMLInputElement>(null);
  const label = t('assistant.panel.files.attach');
  return (
    <>
      <button type="button" className="composer-tool" data-testid="assistant-attach" title={label} aria-label={label} disabled={disabled || bounds === null}
        onClick={() => chooser.current?.click()}>
        <Icon icon={Paperclip} size={16} />
      </button>
      <input ref={chooser} type="file" hidden multiple accept={bounds?.extensions.join(',')} data-testid="assistant-attach-input" aria-label={label}
        onChange={(event) => {
          const files = Array.from(event.target.files ?? []);
          event.target.value = '';
          void join(files);
        }} />
    </>
  );
}

/** The size of a file, as a person reads it. */
function Size({ bytes }: { bytes: number }) {
  const { t, i18n } = useTranslation();
  const { unit, size } = sizeOf(bytes, i18n.language);
  return <span className="file-pill-size">{t(`assistant.panel.files.${unit}`, { size })}</span>;
}

/** The pills of the files: their kind, name and size; removed one by one before the request leaves (none once it is sent). */
export function FilePills({ files, onRemove }: { files: readonly (FileLabel & { readonly id?: number })[]; onRemove?: (id: number) => void }) {
  const { t } = useTranslation();
  if (files.length === 0) return null;
  return (
    <ul className="file-pills" data-testid="assistant-files" aria-label={t('assistant.panel.files.list')}>
      {files.map((file, index) => {
        const id = file.id;
        return (
          <li key={id ?? index} className={`file-pill is-${file.kind}`} data-testid="assistant-file-pill" title={file.name}>
            <Icon icon={ICONS[file.kind]} size={14} />
            <span className="file-pill-name">{file.name}</span>
            <Size bytes={file.size} />
            {onRemove !== undefined && id !== undefined && (
              <button type="button" data-testid="assistant-file-remove" aria-label={t('assistant.panel.files.remove', { name: file.name })}
                title={t('assistant.panel.files.remove', { name: file.name })} onClick={() => { onRemove(id); }}>
                <Icon icon={X} size={12} />
              </button>
            )}
          </li>
        );
      })}
    </ul>
  );
}

/** Without a model, the PDF and the images are named in the prompt copied: the person joins them in their AI. */
export function CopyPasteHint({ files }: { files: readonly FileLabel[] }) {
  const { t } = useTranslation();
  if (!files.some((file) => isBinary(file.kind))) return null;
  return <p className="composer-hint" data-testid="assistant-files-hint">{t('assistant.panel.files.copy_paste_hint')}</p>;
}
