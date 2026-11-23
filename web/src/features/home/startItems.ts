// startItems.ts
// The lines of the menu of the starts of the home page (ADR 111): sample project, gallery of templates, blank map, describe with the assistant, open a file; each only when it is possible for the user.

import { FolderOpen, LayoutTemplate, Plus, Sparkles } from 'lucide-react';
import type { CommandItem } from '../../shared/commands/commands.ts';
import type { Profile } from '../../app/profile/profileStore.ts';

/** The ids of the lines, which the home page runs. */
export type StartId = 'sample' | 'gallery' | 'blank' | 'describe' | 'open';

const line = (id: StartId, label: string, icon: CommandItem['icon'], title?: string): CommandItem =>
  ({ id, label, icon, shortcut: undefined, disabled: false, ...(title === undefined ? {} : { title }) });

/**
 * The starts offered to a user, in the order sample, gallery, blank map, describe, open a file; « New project » puts the
 * blank map first (the usual gesture of Office and Visio). Nothing for a user who is not administrator (he creates no
 * project); no sample outside the desktop mode; no gallery without the links the host gives (SiteLinks false).
 */
export function startItems(profile: Profile | null, { blankFirst }: { blankFirst: boolean }): CommandItem[] {
  if (profile === null || !profile.is_admin) return [];
  const blank = line('blank', 'home.start_blank', Plus);
  const items = [
    profile.desktop === true ? line('sample', 'home.start_sample', Sparkles) : null,
    profile.links == null ? null : line('gallery', 'home.start_gallery', LayoutTemplate, 'home.start_gallery_help'),
    blankFirst ? null : blank,
    line('describe', 'assistant.panel.describe_button', Sparkles),
    line('open', 'files.open', FolderOpen),
  ].filter((item): item is CommandItem => item !== null);
  return blankFirst ? [blank, ...items] : items;
}
