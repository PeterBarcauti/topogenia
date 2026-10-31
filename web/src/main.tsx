// main.tsx
// Starts the interface: languages, global styles, then, once the first language is read, StrictMode, the application and its shell in #root (B-0-11, B-0-12, ADR 083).

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@fontsource-variable/inter';
import '@xyflow/react/dist/base.css';
import { ready } from './shared/i18n/i18n.ts';
import './styles/tokens.css';
import './styles/base.css';
import './styles/components.css';
import './styles/pages.css';
import './styles/map.css';
import './styles/sheet.css';
import './styles/assistant.css';
import { App } from './app/App.tsx';

const root = document.getElementById('root');
// A language that cannot be read leaves the default one: the page starts all the same.
if (root) void ready.finally(() => { createRoot(root).render(<StrictMode><App /></StrictMode>); });
