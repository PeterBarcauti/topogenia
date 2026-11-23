// siteLinks.test.ts
// The links to the website and to the page of the Store (ADR 111): the page of the versions in the language of the interface, the version installed and the kind of installation in the fragment, which the browser keeps to itself; the page of the Store for the Store version; nothing without the links the host gives; a new tab apart from the window of the application, the page itself asking nothing of the network.

import { TEST_LINKS, profileOf } from '../test/profiles.ts';
import { galleryAddress, openSite, updateAddress } from './siteLinks.ts';

/** The administrator, the only one « Check for updates » serves: in desktop mode the person of the computer is. */
const admin = (overrides: Parameters<typeof profileOf>[0] = {}) => profileOf({ is_admin: true, ...overrides });

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  Reflect.deleteProperty(navigator, 'sendBeacon');
});

describe('the links to the site (ADR 111)', () => {
  it('the page of the versions is in French for French and in English for any other language, the version and the kind in the fragment', () => {
    const fragment = `#v=${__APP_VERSION__}&d=portable`;
    expect(['fr', 'fr-CA', 'en', 'de', 'zh'].map((language) => updateAddress(admin({ desktop: true }), language))).toEqual([
      `http://localhost/site/versions/${fragment}`, `http://localhost/site/versions/${fragment}`,
      `http://localhost/site/en/versions/${fragment}`, `http://localhost/site/en/versions/${fragment}`, `http://localhost/site/en/versions/${fragment}`,
    ]);
    const server = updateAddress(admin({ desktop: false }), 'fr') ?? '';
    expect(server).toBe(`http://localhost/site/versions/#v=${__APP_VERSION__}&d=server`);
    // What the browser asks the site for stops before the fragment: the site sees an ordinary visit, no version, no kind.
    const asked = new URL(server);
    expect([`${asked.origin}${asked.pathname}${asked.search}`, asked.hash]).toEqual(['http://localhost/site/versions/', `#v=${__APP_VERSION__}&d=server`]);
  });

  it('the Store version leads to its page of the Store, in any language', () => {
    const store = admin({ desktop: true, store: true });
    expect(['fr', 'en', 'de'].map((language) => updateAddress(store, language))).toEqual([TEST_LINKS.store_page, TEST_LINKS.store_page, TEST_LINKS.store_page]);
  });

  it('no link without the links given by the host', () => {
    expect([updateAddress(admin({ desktop: true, links: null }), 'fr'), updateAddress(admin({ links: undefined }), 'en'), updateAddress(admin({ desktop: true, store: true, links: null }), 'fr')])
      .toEqual([null, null, null]);
  });

  it('no link for a user who is not administrator: he can install nothing', () => {
    expect([updateAddress(profileOf({ is_admin: false }), 'fr'), updateAddress(profileOf({ is_admin: false, desktop: true, store: true }), 'en')]).toEqual([null, null]);
  });

  it('a link opens a new tab without the window of the application and asks nothing itself', () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    const fetch = vi.fn();
    const socket = vi.fn();
    const events = vi.fn();
    const beacon = vi.fn();
    vi.stubGlobal('fetch', fetch);
    vi.stubGlobal('WebSocket', socket);
    vi.stubGlobal('EventSource', events);
    Object.defineProperty(navigator, 'sendBeacon', { value: beacon, configurable: true });
    const request = vi.spyOn(XMLHttpRequest.prototype, 'open');
    const address = updateAddress(admin({ desktop: true }), 'fr') ?? '';
    openSite(address);
    expect([open.mock.calls, fetch.mock.calls, request.mock.calls, socket.mock.calls, events.mock.calls, beacon.mock.calls])
      .toEqual([[[address, '_blank', 'noopener,noreferrer']], [], [], [], [], []]);
  });
});

describe('the gallery of templates (ADR 111)', () => {
  it('is in French for French and in English for any other language, without fragment', () => {
    expect(['fr', 'fr-CA', 'en', 'de'].map((language) => galleryAddress(admin(), language))).toEqual([
      'http://localhost/site/modeles/', 'http://localhost/site/modeles/', 'http://localhost/site/en/templates/', 'http://localhost/site/en/templates/',
    ]);
  });

  it('is none without the links given by the host', () => {
    expect([galleryAddress(admin({ links: null }), 'fr'), galleryAddress(admin({ links: undefined }), 'en'), galleryAddress(null, 'fr')]).toEqual([null, null, null]);
  });
});
