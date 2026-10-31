// eslint.config.mjs
// Lint rules of the interface: recommended JS and TypeScript rules (type-aware) and React hooks rules.

import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';

export default tseslint.config(
  { ignores: ['node_modules', 'test-results', 'playwright-report', 'src/api/generated'] },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  {
    languageOptions: {
      globals: { ...globals.browser, ...globals.node },
      parserOptions: { projectService: { allowDefaultProject: ['eslint.config.mjs'] }, tsconfigRootDir: import.meta.dirname },
    },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
      // Spec 3.2 and 3.5: files of 600 lines at most, functions of 80 lines at most
      'max-lines': ['error', { max: 600 }],
      'max-lines-per-function': ['error', { max: 80 }],
    },
  },
  // Spec 3.5: React components of 250 lines at most
  { files: ['src/**/*.tsx'], ignores: ['src/**/*.test.tsx'], rules: { 'max-lines': ['error', { max: 250 }] } },
  { files: ['eslint.config.mjs', 'scripts/**/*.mjs', 'e2e/**/*.mjs'], ...tseslint.configs.disableTypeChecked },
);
