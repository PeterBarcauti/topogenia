// setup.ts
// Shared set-up of the unit tests: DOM matchers of Testing Library, the languages of the interface loaded as main.tsx does, the stand-ins of the canvas.

import '@testing-library/jest-dom/vitest';

// The tests of the scripts run under Node, without a page to show a language in nor a canvas to draw.
if (typeof document !== 'undefined') {
  const { ready } = await import('../shared/i18n/i18n.ts');
  await ready;
  const { installFlowDom } = await import('./flowDom.ts');
  installFlowDom();
}
