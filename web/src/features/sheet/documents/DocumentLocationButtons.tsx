// DocumentLocationButtons.tsx
// The two commands of the location of a document, at the end of its line: copy its path (its link, or its name when it has none) and, on the desktop mode, show the folder that holds it (ADR 104).

import { useState } from 'react';
import { Copy, FolderOpen } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { copyText } from '../../../shared/ui/clipboard.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { useSheet } from '../SheetContext.tsx';
import { copiedTextOf, copyKindOf, showFolder, showsFolder } from './documentLocation.ts';

/**
 * Copy and show, offered to every reader of the sheet. The copy button says what it copies: the path, the link, or the
 * name of a document without a location; a notification confirms it, or says that the browser refused. The folder
 * button exists on the desktop mode only and for a path: the host shows the folder that holds the document and never
 * opens the document itself; its refusal (a folder not found, no access, a server that does not answer) is said as the
 * host wrote it, and the copy button stays at hand.
 */
export function DocumentLocationButtons({ document }: { document: Schema<'Document'> }) {
  const { t } = useTranslation();
  const { props } = useSheet();
  const [showing, setShowing] = useState(false);
  const kind = copyKindOf(document);
  const copyLabel = t(`sheet.document.copy_${kind}`, { name: document.name });
  const folderLabel = t('sheet.document.open_folder', { name: document.name });
  const copy = async () => {
    const done = await copyText(copiedTextOf(document));
    props.notify(t(done ? `sheet.document.copied_${kind}` : 'sheet.document.copy_refused'), done ? 'success' : 'error');
  };
  const show = async () => {
    setShowing(true);
    try {
      await showFolder(document);
      props.notify(t('sheet.document.folder_shown'), 'success');
    } catch (error) {
      props.notify(messageOf(error), 'error');
    } finally {
      setShowing(false);
    }
  };
  return (
    <>
      <button type="button" className="icon-button" aria-label={copyLabel} title={copyLabel} data-testid={`document-copy-${document.id}`} onClick={() => void copy()}>
        <Icon icon={Copy} size={14} />
      </button>
      {showsFolder(document, props.desktop === true) && (
        <button type="button" className="icon-button" aria-label={folderLabel} title={folderLabel} disabled={showing} data-testid={`document-folder-${document.id}`}
          onClick={() => void show()}>
          <Icon icon={FolderOpen} size={14} />
        </button>
      )}
    </>
  );
}
