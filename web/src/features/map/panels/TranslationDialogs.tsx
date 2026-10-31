// TranslationDialogs.tsx
// The dialogs of the translations over the map: the assistant and the workbook of the project, the map read again after an import, and the manual translations of a step, a flow or a lane (annex B 5, ADR 083).

import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { ownName } from '../../../shared/i18n/languageNames.ts';
import { ManualTranslationsDialog } from '../../translations/ManualTranslationsDialog.tsx';
import { TranslationsDialog } from '../../translations/TranslationsDialog.tsx';
import { translateObject, translatedOf } from '../state/actions/translations.ts';
import type { MapEditor } from '../state/editor.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
import type { TranslatedObject } from '../state/floating.ts';

/**
 * The whole process asked of the assistant in a language (ADR 083), from its root map: the panel opens with the request,
 * whose proposal takes only the translations into that language.
 */
async function translateWithAssistant(editor: MapEditor, map: Schema<'MapView'>, language: string, request: string): Promise<void> {
  const panel = editor.assistant.getState();
  panel.setOpen(true);
  if (panel.status === null) await panel.loadStatus();
  const root = map.breadcrumb[0]?.map_id ?? map.id;
  await editor.assistant.getState().send(request, 'translate', [], { language, mapId: root });
}

/**
 * The assistant and the workbook of the project; an import reaches every map, the one shown is read again by the queue
 * of the editor (B-5-24).
 */
export function ProjectTranslations({ map, assistant, onClose }: { map: Schema<'MapView'>; assistant?: string | undefined; onClose: () => void }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const shown = useMapState((state) => state.shown?.language);
  return (
    <TranslationsDialog project={{ id: map.project_id, code: map.project_code }} languages={editor.languages} coverage={map.languages}
      preselected={assistant ?? (shown === map.languages.origin ? undefined : shown)} onClose={onClose} onImported={() => void editor.reload()}
      onAssistant={(language) => {
        onClose();
        void translateWithAssistant(editor, map, language, t('process_language.request', { language: ownName(language) }));
      }} />
  );
}

/** The manual translations of the object of a menu; nothing when it is gone or has no name. */
export function ObjectTranslations({ map, object, onClose }: { map: Schema<'MapView'>; object: TranslatedObject; onClose: () => void }) {
  const editor = useEditor();
  const shown = useMapState((state) => state.shown?.language);
  const named = translatedOf(map, object);
  if (named === null) return null;
  // The fields at once: the language shown and the languages the project is translated into (ADR 083), not all eighteen.
  const preferred = editor.languages.filter((language) => language === shown || (map.languages.written[language] ?? 0) > 0);
  return (
    <ManualTranslationsDialog named={named} languages={editor.languages} preferred={preferred} onClose={onClose}
      onSave={(changes) => translateObject(editor, object, changes)} />
  );
}
