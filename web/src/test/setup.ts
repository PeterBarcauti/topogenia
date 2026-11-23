// setup.ts
// Shared set-up of the unit tests: DOM matchers of Testing Library, the languages of the interface loaded as main.tsx does, the stand-ins of the canvas, a version released today.

import '@testing-library/jest-dom/vitest';

// The tests of the scripts run under Node, without a page to show a language in nor a canvas to draw.
if (typeof document !== 'undefined') {
  const { ready } = await import('../shared/i18n/i18n.ts');
  await ready;
  const { installFlowDom } = await import('./flowDom.ts');
  installFlowDom();

  // The reminder of the home page comes 90 days after the release: a test that sets no clock of its own sees a version released today, so that none changes its result with the calendar (ADR 111).
  beforeEach(() => {
    const today = new Date();
    const two = (value: number) => String(value).padStart(2, '0');
    vi.stubGlobal('__APP_RELEASE_DATE__', `${String(today.getFullYear())}-${two(today.getMonth() + 1)}-${two(today.getDate())}`);
  });
}
