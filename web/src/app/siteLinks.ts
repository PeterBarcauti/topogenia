// siteLinks.ts
// The links to the website and to the page of the Store (ADR 111): the addresses the host gives in the profile (`links`, none when the operator hides them), the pages of the site built on them in the language of the help, the version installed and the kind of installation written in the fragment, which the browser never sends; opened by the browser in a new tab, the page itself asking nothing of the network.

import { RefreshCw } from 'lucide-react';
import type { CommandItem } from '../shared/commands/commands.ts';
import { formatDay } from '../shared/i18n/formatDate.ts';
import { servedInFrench } from './help.ts';
import type { Profile } from './profile/profileStore.ts';

type Links = NonNullable<Profile['links']>;

/** The id of the line « Check for updates » in the Help menu and in the menu of the account. */
export const UPDATE_COMMAND = 'update';

/**
 * The page of the versions of the site, in French for an interface in French and in English for any other (the rule of
 * the help); the fragment carries the version installed (`v`) and the kind of installation (`d`: `portable` for the
 * desktop mode outside the Store, `server`), which the page reads in the browser.
 */
function versionsAddress(links: Links, language: string, kind: 'portable' | 'server'): string {
  const fragment = new URLSearchParams({ v: __APP_VERSION__, d: kind }).toString();
  return `${links.site}/${servedInFrench(language) ? '' : 'en/'}versions/#${fragment}`;
}

/**
 * What « Check for updates » opens: the page of the Store for the Store version, which updates by itself, else the page
 * of the versions; null when it is not offered: no links given by the host (SiteLinks false), or a user who is not
 * administrator, who can install nothing.
 */
export function updateAddress(profile: Profile | null, language: string): string | null {
  const links = profile?.links;
  if (profile === null || links === null || links === undefined || !profile.is_admin) return null;
  return profile.store === true ? links.store_page : versionsAddress(links, language, profile.desktop === true ? 'portable' : 'server');
}

/** The line « Check for updates » of a menu, its tooltip saying the version installed and its day; null when it is not offered. */
export function updateItem(profile: Profile | null, language: string): CommandItem | null {
  if (updateAddress(profile, language) === null) return null;
  return {
    id: UPDATE_COMMAND, label: 'app.update_check', icon: RefreshCw, shortcut: undefined, disabled: false,
    title: profile?.store === true ? 'app.update_check_store_help' : 'app.update_check_help',
    labelValues: { version: __APP_VERSION__, date: formatDay(__APP_RELEASE_DATE__, language) },
  };
}

/** Opens an address of the site in a new tab of the browser, which is given neither the window of the application nor where it comes from. */
export function openSite(address: string): void {
  window.open(address, '_blank', 'noopener,noreferrer');
}

/** Runs « Check for updates »: the address it opens, when it is offered. */
export function openUpdate(profile: Profile | null, language: string): void {
  const address = updateAddress(profile, language);
  if (address !== null) openSite(address);
}

/** The gallery of templates of the site, in French for an interface in French and in English for any other; null without the links the host gives. */
export function galleryAddress(profile: Profile | null, language: string): string | null {
  const links = profile?.links;
  if (links === null || links === undefined) return null;
  return `${links.site}/${servedInFrench(language) ? 'modeles/' : 'en/templates/'}`;
}
