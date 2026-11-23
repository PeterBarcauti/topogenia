// StartMenu.tsx
// The menu of the starts of the home page, under the main button of an administrator without a project and under « New project » (ADR 111): sample project, gallery of templates, blank map, describe with the assistant, open a file.

import type { MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../app/profile/ProfileProvider.tsx';
import { navigate, paths } from '../../app/router.ts';
import { galleryAddress, openSite } from '../../app/siteLinks.ts';
import { Menu } from '../../shared/ui/Menu.tsx';
import { MENU_WIDTH } from '../../shared/ui/menuPlacement.ts';
import { startItems, type StartId } from './startItems.ts';

/** Where a menu opens: under the button, by its left side or by its right side (the button at the right of its column). */
export interface StartAnchor {
  readonly x: number;
  readonly y: number;
  readonly blankFirst: boolean;
}

export function anchorOf(event: MouseEvent<HTMLButtonElement>, side: 'left' | 'right', blankFirst: boolean): StartAnchor {
  const box = event.currentTarget.getBoundingClientRect();
  return { x: side === 'left' ? box.left : box.right - MENU_WIDTH, y: box.bottom + 4, blankFirst };
}

export interface StartMenuProps {
  readonly anchor: StartAnchor;
  /** Creates the sample project. */
  readonly onSample: () => void;
  /** Opens the dialog of a start that has one. */
  readonly onDialog: (dialog: 'pmap' | 'describe') => void;
  readonly onClose: () => void;
}

export function StartMenu({ anchor, onSample, onDialog, onClose }: StartMenuProps) {
  const { i18n } = useTranslation();
  const profile = useProfile((state) => state.profile);
  const run = (id: string) => {
    switch (id as StartId) {
      case 'sample': onSample(); break;
      case 'gallery': {
        const address = galleryAddress(profile, i18n.language);
        if (address !== null) openSite(address);
        break;
      }
      case 'blank': navigate(paths.newProject); break;
      case 'describe': onDialog('describe'); break;
      case 'open': onDialog('pmap'); break;
    }
  };
  return <Menu label="home.start_menu" testId="home-start-menu" items={startItems(profile, { blankFirst: anchor.blankFirst })} at={anchor} underButton onClose={onClose} onRun={run} />;
}
