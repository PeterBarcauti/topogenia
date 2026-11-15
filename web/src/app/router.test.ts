// router.test.ts
// The router: history of the browser, a path unchanged left alone, replacement, the four screens and the others (annex B 1.1, AR-30).

import { act, renderHook } from '@testing-library/react';
import { NAVIGATION_EVENT, navigate, paths, routeOf, usePath } from './router.ts';

const guid = '9D708A6E-1ACA-F111-AFA9-985F41535C4A';

afterEach(() => {
  window.history.replaceState(null, '', '/');
});

describe('the routes (B-1.1-04 to B-1.1-11)', () => {
  it('/ is the home page, with or without a final slash', () => {
    expect(routeOf('/')).toEqual({ screen: 'home' });
    expect(routeOf('')).toEqual({ screen: 'home' });
    expect(routeOf('//')).toEqual({ screen: 'home' });
  });

  it('/projects/new is the creation of a project, in English (B-1.1-07)', () => {
    expect(routeOf('/projects/new')).toEqual({ screen: 'new-project' });
    expect(routeOf('/projects/new/')).toEqual({ screen: 'new-project' });
    expect(routeOf('/projets/nouveau')).toEqual({ screen: 'not-found' });
  });

  it('/projects/{id}/members names the project in lower case (B-1.1-05, B-1.1-08)', () => {
    expect(routeOf(`/projects/${guid}/members`)).toEqual({ screen: 'members', projectId: guid.toLowerCase() });
    expect(routeOf(`/projects/${guid}/members/`)).toEqual({ screen: 'members', projectId: guid.toLowerCase() });
    expect(routeOf(`/projets/${guid}/membres`)).toEqual({ screen: 'not-found' });
  });

  it('/maps/{id} names the map in lower case (B-1.1-09)', () => {
    expect(routeOf(`/maps/${guid}`)).toEqual({ screen: 'map', mapId: guid.toLowerCase() });
    expect(routeOf(`/maps/${guid}/`)).toEqual({ screen: 'map', mapId: guid.toLowerCase() });
    expect(routeOf(`/cartes/${guid}`)).toEqual({ screen: 'not-found' });
  });

  it('any other path is unknown: a malformed identifier, a sub-path, an extension (B-1.1-10)', () => {
    for (const path of ['/maps/not-a-guid', `/maps/${guid}/x`, `/x/maps/${guid}`, `/maps/${guid}0`, '/projects', '/projects/new/x', '/index.html'])
      expect(routeOf(path)).toEqual({ screen: 'not-found' });
  });

  it('the trash, files, sheet, translations and profile have no route (B-1.1-11)', () => {
    for (const path of ['/trash', '/files', '/sheet', `/steps/${guid}/sheet`, '/translations', '/profile', '/me'])
      expect(routeOf(path)).toEqual({ screen: 'not-found' });
  });

  it('the paths given to the links lead back to their screens', () => {
    expect(routeOf(paths.home)).toEqual({ screen: 'home' });
    expect(routeOf(paths.newProject)).toEqual({ screen: 'new-project' });
    expect(routeOf(paths.members(guid))).toEqual({ screen: 'members', projectId: guid.toLowerCase() });
    expect(routeOf(paths.map(guid))).toEqual({ screen: 'map', mapId: guid.toLowerCase() });
  });
});

describe('the navigation (B-1.1-01 to B-1.1-03)', () => {
  it('navigate pushes an entry of the history and tells the subscribers', () => {
    const heard = vi.fn();
    window.addEventListener(NAVIGATION_EVENT, heard);
    const length = window.history.length;
    navigate('/projects/new');
    window.removeEventListener(NAVIGATION_EVENT, heard);
    expect(window.location.pathname).toBe('/projects/new');
    expect(window.history.length).toBe(length + 1);
    expect(heard).toHaveBeenCalledTimes(1);
  });

  it('navigate to the current path does nothing: no entry, no event (B-1.1-02)', () => {
    navigate('/projects/new');
    const heard = vi.fn();
    window.addEventListener(NAVIGATION_EVENT, heard);
    const length = window.history.length;
    navigate('/projects/new');
    window.removeEventListener(NAVIGATION_EVENT, heard);
    expect(heard).not.toHaveBeenCalled();
    expect(window.history.length).toBe(length);
  });

  it('navigate with replace changes the address without a new entry (B-1.1-03)', () => {
    const length = window.history.length;
    navigate(`/maps/${guid}`, true);
    expect(window.location.pathname).toBe(`/maps/${guid}`);
    expect(window.history.length).toBe(length);
  });

  it('usePath follows navigate and the back button of the browser (B-1.1-01)', () => {
    const { result } = renderHook(() => usePath());
    expect(result.current).toBe('/');
    act(() => {
      navigate('/projects/new');
    });
    expect(result.current).toBe('/projects/new');
    act(() => {
      window.history.replaceState(null, '', '/');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    expect(result.current).toBe('/');
  });
});
