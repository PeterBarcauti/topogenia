// ShortcutsDialog.tsx
// The panel of the keyboard shortcuts: the sheet of the keys of the map by use, each key in the language of the interface, the section Changing the map marked the facilitator's; a modal dialog, as in the known tools (lot 1 of the 3.4, ADR 109).

import { Fragment, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { SHORTCUT_SHEET, keysOf, labelOf, lineId, type SheetSection } from '../features/map/state/commands/shortcutSheet.ts';
import { Dialog } from '../shared/ui/Dialog.tsx';
import { useShortcutLabel } from '../shared/ui/useShortcutLabel.ts';

/** One section: its name, its note, a table of what each key does and the keys, « or » between two. */
function Section({ section }: { section: SheetSection }) {
  const { t } = useTranslation();
  const shortcut = useShortcutLabel();
  const titleId = useId();
  return (
    <section className="shortcuts-section" aria-labelledby={titleId}>
      <h3 id={titleId}>{t(`shortcuts.sections.${section.id}`)}</h3>
      {section.note !== undefined && <p className="shortcuts-note">{t(section.note)}</p>}
      <table className="shortcuts-table" aria-labelledby={titleId}>
        <tbody>
          {section.lines.map((line) => (
            <tr key={lineId(line)} data-testid={`shortcut-${lineId(line)}`}>
              <td>
                {t(labelOf(line))}
                {'note' in line && line.note !== undefined && <small className="shortcuts-line-note">{t(line.note)}</small>}
              </td>
              <td className="shortcuts-keys">
                {'shown' in line && line.shown !== undefined ? <kbd>{t(line.shown)}</kbd> : keysOf(line).map((chord, index) => (
                  <Fragment key={chord}>
                    {index > 0 && ` ${t('shortcuts.or')} `}
                    <kbd>{shortcut(chord)}</kbd>
                  </Fragment>
                ))}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

export function ShortcutsDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  return (
    <Dialog title={t('shortcuts.title')} testId="shortcuts" onClose={onClose} wide>
      {SHORTCUT_SHEET.map((section) => <Section key={section.id} section={section} />)}
    </Dialog>
  );
}
