// MapScreen.tsx
// The screen of a map: its reading and its failure, the project declared to the header, the language it is shown in and the lock of its version of origin, the ribbon, the banners of a reader, of a translation, of a deep map and of the link mode, the canvas with its side panel, one docked at a time, the floating element, the frame watched (annex B 2.1, 2.9, B 4, B-7-13, B-7-16, ADR 083).

import { useEffect, useState } from 'react';
import { Eye, Lock, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../api/client.ts';
import { ErrorPage } from '../../app/ErrorPage.tsx';
import { useShownLanguage } from '../../app/ProcessLanguageMenu.tsx';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { ownName } from '../../shared/i18n/languageNames.ts';
import { openProject } from '../../shared/state/openProject.ts';
import { chooseProcessLanguage } from '../../shared/state/processLanguage.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Icon } from '../../shared/ui/Icon.tsx';
import { Canvas } from './canvas/Canvas.tsx';
import { Ribbon } from './chrome/Ribbon.tsx';
import { untranslatedOn } from './model/mapLayout.ts';
import { MapFloating } from './panels/MapFloating.tsx';
import { MapSidePanel } from './panels/MapSidePanel.tsx';
import { watchFrame } from './state/actions/dissolution.ts';
import { watchSidePanel } from './state/actions/sheet.ts';
import { watchWorkTime } from './state/actions/valueStream.ts';
import { createMapEditor, type MapEditor } from './state/editor.ts';
import { EditorProvider, useEditor, useMapState } from './state/EditorContext.tsx';
import type { MapSource, PreviewedTranslation, Shown } from './state/mapStore.ts';
import type { PreferenceStorage } from './state/preferences.ts';

/**
 * The banner of a map shown in another language than its own (ADR 083): the version of origin locked, a name corrected
 * written into this translation (for who may translate), the texts of the map still to translate, the way back to the
 * original and to a translation by the assistant.
 */
function TranslationBanner({ map, shown }: { map: Schema<'MapView'>; shown: Shown }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const translating = useMapState((state) => state.translating);
  const missing = untranslatedOn(map, shown.language);
  const names = { language: ownName(shown.language), origin: ownName(shown.origin) };
  return (
    <div className="map-banner map-banner--translation" role="status" data-testid="map-translation">
      <span className="translation-lock"><Icon icon={Lock} size={15} /></span>
      <p>
        {t(translating ? 'process_language.locked_banner' : 'process_language.locked_reader', names)}
        {missing > 0 && <span className="translation-missing" data-testid="map-translation-missing"> {t('process_language.untranslated_count', { count: missing })}</span>}
      </p>
      <div className="translation-actions">
        <Button testId="map-translation-origin" onClick={() => { chooseProcessLanguage(map.project_id, shown.origin); }}>
          {t('process_language.show_origin', names)}
        </Button>
        {translating && missing > 0 && editor.languages.length > 1 && (
          <Button testId="map-translation-assistant" variant="primary" icon={Sparkles}
            onClick={() => { editor.floating.getState().open({ kind: 'translations', assistant: shown.language }); }}>
            {t('process_language.translate')}
          </Button>
        )}
      </div>
    </div>
  );
}

/**
 * The banner of a translation of the assistant previewed (ADR 083): the map shows its texts in place, in its language,
 * and nothing is written before « Appliquer »; the texts it leaves untranslated stay marked and counted.
 */
function PreviewBanner({ map, previewed }: { map: Schema<'MapView'>; previewed: PreviewedTranslation }) {
  const { t } = useTranslation();
  const missing = untranslatedOn(map, previewed.language);
  return (
    <div className="map-banner map-banner--translation map-banner--preview" role="status" data-testid="map-translation-preview">
      <span className="translation-lock"><Icon icon={Eye} size={15} /></span>
      <p>
        {t('process_language.preview_banner', { language: ownName(previewed.language) })}
        {missing > 0 && <span className="translation-missing" data-testid="map-translation-missing"> {t('process_language.untranslated_count', { count: missing })}</span>}
      </p>
    </div>
  );
}

/**
 * « Read only » for a viewer who may not change the structure (B-2.1-35), the translation shown instead in another
 * language than the one of origin, or the one of the assistant previewed (ADR 083); the depth alert past the advised
 * depth (B-2.1-36); the help of the link mode, with the code of the source once chosen (B-2.1-37).
 */
function MapBanners({ map }: { map: Schema<'MapView'> }) {
  const { t } = useTranslation();
  const linking = useMapState((state) => state.mode === 'link');
  const shown = useMapState((state) => state.shown);
  const previewed = useMapState((state) => state.previewed);
  const source = useMapState((state) => map.steps.find((step) => step.id === state.linkSource)?.code ?? null);
  return (
    <>
      {previewed !== null
        ? <PreviewBanner map={map} previewed={previewed} />
        : shown?.locked === true
          ? <TranslationBanner map={map} shown={shown} />
          : !map.rights.structure && <p className="map-banner" data-testid="map-read-only">{t('map.read_only')}</p>}
      {map.depth > map.recommended_depth && (
        <p className="map-banner map-banner--warning" data-testid="map-depth">{t('map.depth', { level: map.depth, max: map.recommended_depth })}</p>
      )}
      {linking && <p className="map-banner map-banner--link" data-testid="map-link">{source === null ? t('map.link_help') : t('map.link_source', { code: source })}</p>}
    </>
  );
}

/**
 * Declares the project of the map shown to the header (B-7-13): its code, the breadcrumb, the right to change it, the
 * File menu, which the editor opens under its button, and the translations when another language is configured (B-5-01);
 * cleared when the screen leaves.
 */
function useOpenProject(editor: MapEditor, map: Schema<'MapView'> | null): void {
  const translating = useStore(editor.store, (state) => state.translating);
  useEffect(() => {
    if (map === null) return;
    openProject.setState({
      project: {
        id: map.project_id, code: map.project_code, canEdit: map.rights.structure || translating, languages: map.languages,
        breadcrumb: map.breadcrumb.map((crumb) => ({ mapId: crumb.map_id, name: crumb.name, sourceLanguage: crumb.source_language, translations: crumb.translations })),
        openFileMenu: (at) => {
          editor.floating.getState().open({ kind: 'menu', target: { kind: 'file' }, at });
        },
        ...(editor.languages.length > 1 && (map.rights.structure || translating)
          ? { openTranslations: () => { editor.floating.getState().open({ kind: 'translations' }); } }
          : {}),
      },
    });
  }, [editor, map, translating]);
  useEffect(() => () => {
    openProject.setState({ project: null });
  }, []);
}

/** Tells the editor the language the project is shown in: the lock of the version of origin follows (ADR 083). */
function useShown(editor: MapEditor, map: Schema<'MapView'> | null): void {
  const language = useShownLanguage(map === null ? null : { id: map.project_id, languages: map.languages });
  const origin = map?.languages.origin ?? null;
  useEffect(() => {
    if (language !== null && origin !== null) editor.store.getState().show({ language, origin, locked: language !== origin });
  }, [editor, language, origin]);
}

export interface MapScreenProps {
  readonly mapId: string;
  /** The user is an administrator: the entries [admin] of the File menu (B-2.4-53, B-2.4-55). */
  readonly admin?: boolean;
  /** The languages of the configuration, read with the profile: the translations of the names (annex B 5). */
  readonly languages?: readonly string[];
  /** Where the map is read and the preferences kept: the host and the browser, fakes in the tests. */
  readonly source?: MapSource;
  readonly storage?: PreferenceStorage | null;
  /** The editor of the map, made by the screen; given by the tests that read its state. */
  readonly editor?: MapEditor;
}

/** The editor keeps its stores for the map it shows; a new map mounts a new editor (B-1.1-09). */
export function MapScreen({ mapId, admin = false, languages = [], source, storage, editor: given }: MapScreenProps) {
  const { t } = useTranslation();
  const [editor] = useState(() => given ?? createMapEditor({ admin, languages, ...(source === undefined ? {} : { source }), ...(storage === undefined ? {} : { storage }) }));
  const map = useStore(editor.store, (state) => state.map);
  useOpenProject(editor, map);
  useShown(editor, map);
  const error = useStore(editor.store, (state) => state.error);
  const showCodes = useStore(editor.preferences, (state) => state.showCodes);
  const alwaysShowPictos = useStore(editor.preferences, (state) => state.alwaysShowPictos);
  useEffect(() => {
    void editor.store.getState().load(mapId);
    return () => {
      editor.store.getState().cancel();
    };
  }, [editor, mapId]);
  useEffect(() => watchFrame(editor), [editor]);
  useEffect(() => watchSidePanel(editor), [editor]);
  useEffect(() => watchWorkTime(editor), [editor]);
  if (map === null && error !== null)
    return <ErrorPage testId="map-error" message={messageOf(error)} actionLabel={t('app.retry')} onAction={() => void editor.store.getState().load(mapId)} />;
  if (map === null) return <p className="page-loading" data-testid="map-loading">{t('map.loading')}</p>;
  return (
    <EditorProvider editor={editor}>
      <div className="map-screen" data-testid="map-screen">
        <Ribbon />
        <MapBanners map={map} />
        <div className="map-workspace">
          <Canvas map={map} showCodes={showCodes} alwaysShowPictos={alwaysShowPictos} />
          <MapSidePanel />
        </div>
        <MapFloating />
      </div>
    </EditorProvider>
  );
}
