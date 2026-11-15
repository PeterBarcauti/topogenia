// PresenterVoice.tsx
// The voice in the bar of a presentation (ADR 097, F-119): the button that reads the subtitles aloud, the question asked at its first use where online voices are offered (the voice of the computer or a natural one, said for where the text goes, essai J2 of 3.2), and beside it, once on, its settings: the voice among those of the language shown (the one chosen kept by the project for that language when a facilitator chooses it, lot J10), the online voices allowed or not, said for what they do with the text, the box off and saying why when the classification of the project keeps its texts on the computer (F-137). The question and the settings take the focus, and are closed by Échap or a click beside them.

import { useCallback, useEffect, useRef, type RefObject } from 'react';
import { SlidersHorizontal, Volume2, VolumeX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { useShortcutLabel } from '../../../shared/ui/useShortcutLabel.ts';
import type { MapEditor } from '../state/editor.ts';
import { useEscapeHeld } from './escapeKey.ts';
import type { PresenterVoice } from './usePresenterVoice.ts';
import { switchVoice, turnVoiceOn } from './voiceSwitch.ts';

/**
 * A panel above the bar, the question of the voice or its settings: the focus goes into it when it opens, on the panel
 * itself so that no key answers by chance, and back to the button that opened it, the first of its box, when it closes
 * (R6-01). Échap closes it, the presentation going on: the keys of the stage hear it (`usePresenterKeys`), held from
 * the browser in full screen where it can be; a click beside it closes it too (audit 3.2, BUG-06).
 */
function useVoicePanel(close: () => void, within: RefObject<HTMLSpanElement | null>): RefObject<HTMLDivElement | null> {
  const panel = useRef<HTMLDivElement>(null);
  useEscapeHeld();
  useEffect(() => {
    const opener = within.current?.querySelector<HTMLButtonElement>('button') ?? null;
    panel.current?.focus();
    return () => {
      opener?.focus();
    };
  }, [within]);
  useEffect(() => {
    const beside = (event: PointerEvent) => {
      if (!(event.target instanceof Node) || within.current?.contains(event.target) !== true) close();
    };
    document.addEventListener('pointerdown', beside);
    return () => {
      document.removeEventListener('pointerdown', beside);
    };
  }, [close, within]);
  return panel;
}

/** The question of the voice, above its button, once: the voice of the computer, nothing leaving it, or a natural online one, the text
 * leaving for the maker of the browser, offered only when the project allows it (F-137). Unanswered (Échap, a click beside), the voice
 * stays off and the question comes again. */
function VoiceQuestion({ editor, voice, language, within }: { editor: MapEditor; voice: PresenterVoice; language: string; within: RefObject<HTMLSpanElement | null> }) {
  const { t } = useTranslation();
  const close = useCallback(() => {
    editor.presenter.getState().ask(false);
  }, [editor]);
  const panel = useVoicePanel(close, within);
  const answer = (natural: boolean) => () => {
    turnVoiceOn(editor, voice, language, natural);
  };
  return (
    <div ref={panel} className="presenter-voice-panel" role="group" aria-label={t('presenter.voice.question')} tabIndex={-1} data-testid="presenter-voice-question">
      <p className="presenter-voice-question">{t('presenter.voice.question')}</p>
      {voice.local.length > 0 && (
        <button type="button" className="presenter-voice-answer" data-testid="presenter-voice-local" onClick={answer(false)}>
          <strong>{t('presenter.voice.local_choice')}</strong>
          <span>{t('presenter.voice.local_choice_help')}</span>
        </button>
      )}
      {voice.natural.length > 0 && (
        <button type="button" className="presenter-voice-answer" data-testid="presenter-voice-natural" onClick={answer(true)}>
          <strong>{t('presenter.voice.natural_choice')}</strong>
          <span>{t('presenter.voice.natural_choice_help')}</span>
        </button>
      )}
    </div>
  );
}

/** The settings of the voice, above the bar: the voice, the online ones allowed or not and what that means, the box off where the project
 * keeps its texts on the computer (F-137). Échap closes them first, the presentation going on, and a click beside them closes them too
 * (audit 3.2, BUG-06). */
function VoiceSettings({ editor, voice, within }: { editor: MapEditor; voice: PresenterVoice; within: RefObject<HTMLSpanElement | null> }) {
  const { t } = useTranslation();
  const online = useStore(editor.preferences, (state) => state.presenterOnlineVoices);
  const where = (local: boolean) => t(local ? 'presenter.voice.local' : 'presenter.voice.remote');
  const shown = voice.voice === null ? null : { name: voice.voice.name, local: voice.voice.localService };
  const close = useCallback(() => {
    editor.presenter.getState().tune(false);
  }, [editor]);
  const panel = useVoicePanel(close, within);
  return (
    <div ref={panel} className="presenter-voice-panel" role="group" aria-label={t('presenter.bar.voice_settings')} tabIndex={-1} data-testid="presenter-voice-panel">
      {voice.voices.length === 0
        ? <p className="presenter-voice-none" data-testid="presenter-voice-none">{t('presenter.voice.none')}</p>
        : (
          <label className="presenter-voice-field">
            <span>{t('presenter.voice.label')}</span>
            <select value={voice.voice?.name ?? ''} data-testid="presenter-voice-choice"
              onChange={(event) => {
                // For the session, kept by the browser, and by the project for this language when a facilitator chooses (lot J10).
                const picked = voice.voices.find((candidate) => candidate.name === event.target.value);
                if (picked !== undefined) voice.choose({ name: picked.name, local: picked.localService });
              }}>
              {voice.voices.map((candidate) => <option key={candidate.name} value={candidate.name}>{`${candidate.name} (${where(candidate.localService)})`}</option>)}
            </select>
          </label>
        )}
      {voice.keeps && shown !== null && (
        <label className="presenter-voice-check">
          <input type="checkbox" checked={voice.projectVoice?.name === shown.name && voice.projectVoice.local === shown.local} data-testid="presenter-voice-project"
            onChange={(event) => {
              // Ticked, the project keeps the voice shown, with no other to choose first; unticked, it keeps none for this language (third independent review of the lot J10).
              if (event.target.checked) voice.choose(shown);
              else voice.forget();
            }} />
          <span>{t('presenter.voice.project')}</span>
        </label>
      )}
      <label className="presenter-voice-check">
        <input type="checkbox" checked={online && voice.allowed} disabled={!voice.allowed} data-testid="presenter-voice-online"
          onChange={(event) => {
            editor.preferences.getState().set('presenterOnlineVoices', event.target.checked);
          }} />
        <span>{t('presenter.voice.online')}</span>
      </label>
      {!voice.allowed && <p className="presenter-voice-help" data-testid="presenter-voice-online-refused">{t('presenter.voice.online_refused')}</p>}
      <p className="presenter-voice-help">{t('presenter.voice.online_help')}</p>
    </div>
  );
}

/** The button of the voice, the question of its first use, and its settings once it is on (V on the keyboard does as the button). */
export function PresenterVoiceButtons({ editor, voice, language }: { editor: MapEditor; voice: PresenterVoice; language: string }) {
  const { t } = useTranslation();
  const shortcut = useShortcutLabel();
  const on = useStore(editor.preferences, (state) => state.presenterVoice);
  const asking = useStore(editor.presenter, (state) => state.asking);
  const tuning = useStore(editor.presenter, (state) => state.tuning);
  const box = useRef<HTMLSpanElement>(null);
  const asked = useRef<HTMLSpanElement>(null);
  const name = t('presenter.bar.voice');
  return (
    <>
      <span className="presenter-voice-settings" ref={asked}>
        <button type="button" className="presenter-button" title={`${name} (${shortcut('V')})`} aria-label={name} aria-pressed={on} aria-expanded={on ? undefined : asking}
          data-testid="presenter-voice"
          onClick={() => {
            switchVoice(editor, voice, language);
          }}>
          <Icon icon={on ? Volume2 : VolumeX} size={18} />
        </button>
        {asking && !on && <VoiceQuestion editor={editor} voice={voice} language={language} within={asked} />}
      </span>
      {on && (
        <span className="presenter-voice-settings" ref={box}>
          <button type="button" className="presenter-button" title={t('presenter.bar.voice_settings')} aria-label={t('presenter.bar.voice_settings')} aria-expanded={tuning}
            data-testid="presenter-voice-settings"
            onClick={() => {
              editor.presenter.getState().tune(!tuning);
            }}>
            <Icon icon={SlidersHorizontal} size={17} />
          </button>
          {tuning && <VoiceSettings editor={editor} voice={voice} within={box} />}
        </span>
      )}
    </>
  );
}
