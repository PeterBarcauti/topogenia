// copyImage.ts
// The map, or the steps of a selection, copied to the clipboard as an image: the capture of the PNG export with its line of classification, bounded to 8 000 px a side, written in the gesture of the person; « Copie de l’image… », then copied, or the failure with the PNG export offered; nothing written, nothing to undo (ADR 109).

import { i18next } from '../../../../shared/i18n/i18n.ts';
import { copyImage } from '../../../../shared/ui/clipboard.ts';
import { EXPORT_TOAST_KEY } from '../../../../shared/ui/toasts.ts';
import type { MapEditor } from '../editor.ts';
import { exportMap, mapImage } from './exports.ts';

const t = (key: string): string => i18next.t(key);

/**
 * The longest side of a copied image, in pixels: the clipboard of Windows keeps a bitmap, 576 MB for the 12 000 px of the
 * export. A map under 4 000 px a side gives the image of the PNG export (twice its resolution).
 */
const COPY_MAXIMUM = 8_000;

/**
 * Copies the image of the map shown, or of the steps given, to the clipboard. The writing starts in the turn of the call,
 * before anything is awaited, so that the browser takes it as part of the click (ADR 109). Left alone while another
 * export runs, or with no canvas of the map shown (the view in BPMN 2.0).
 */
export async function copyMapImage(editor: MapEditor, steps?: readonly string[]): Promise<void> {
  const { map, exporting } = editor.store.getState();
  if (map === null || exporting !== null || editor.canvas.target() === null) return;
  editor.store.setState({ exporting: { format: 'png', caption: null, ...(steps === undefined ? {} : { steps }) } });
  editor.notify(t('export.copying'), undefined, undefined, EXPORT_TOAST_KEY);
  try {
    const copied = await copyImage(() => mapImage(editor, map, { ...(steps === undefined ? {} : { steps }), maximum: COPY_MAXIMUM }));
    if (copied) editor.notify(t('export.copied'), 'success', undefined, EXPORT_TOAST_KEY);
    else editor.notify(t('export.copy_failed'), 'error', { label: t('export.copy_as_png'), run: () => void exportMap(editor, 'png') }, EXPORT_TOAST_KEY);
  } finally {
    editor.store.setState({ exporting: null });
  }
}
