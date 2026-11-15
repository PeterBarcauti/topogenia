// PresenterTitle.tsx
// Where a presentation stands (essai J10, asked by the project owner): at the top left of the stage, the name of the process on its main map, and in a detail the step it details, the levels above it paler before it; in the language shown, as the breadcrumb of the editor the stage covers.

import { Fragment } from 'react';
import type { Schema } from '../../../api/client.ts';
import { localizedName } from '../../../shared/i18n/localizedName.ts';
import type { Presentation } from './usePresentation.ts';

type Crumb = Schema<'BreadcrumbView'>;

/**
 * The levels a room reads: the main map by its name; a detail by the steps that lead to it, without the name of the
 * process, which the room has read on the main map.
 */
export function titleLevels(breadcrumb: readonly Crumb[]): readonly Crumb[] {
  return breadcrumb.length > 1 ? breadcrumb.slice(1) : breadcrumb;
}

/** The title of the map presented: the last level plain, those before it paler, a chevron between two; nothing without a level. */
export function PresenterTitle({ presentation }: { presentation: Presentation }) {
  const { map, language } = presentation;
  const levels = titleLevels(map.breadcrumb);
  if (levels.length === 0) return null;
  const last = levels.length - 1;
  return (
    <p className="presenter-title" lang={language} data-testid="presenter-title">
      {levels.map((level, index) => (
        <Fragment key={level.map_id}>
          {index > 0 && <span className="presenter-title-chevron" aria-hidden="true">›</span>}
          <span className={index === last ? 'presenter-title-level is-current' : 'presenter-title-level'} data-testid={`presenter-title-level-${String(index)}`}>
            {localizedName({ name: level.name, source_language: level.source_language, name_translations: level.translations }, language)}
          </span>
        </Fragment>
      ))}
    </p>
  );
}
