// fileMenu.ts
// The four groups of the File menu opened in cascade (exports of the map, project, package of exchange, assistant) with their short labels, written once for the menu, the tests and the journeys (ADR 117).

import type { MenuSubmenu } from '../../../../shared/ui/menuBlocks.ts';

/**
 * The groups of the File menu: the commands each one holds (the identifiers of {@link FILE_COMMANDS}), in their order, and
 * the key of the short label the menu shows in the submenu (« PDF (A3 paysage) » for « Exporter en PDF (A3 paysage) »);
 * a command whose label is already short has none. The command bar and every other place keep the whole label.
 */
export const FILE_SUBMENUS: readonly MenuSubmenu[] = [
  {
    id: 'export', name: 'menu.file_cascade.export', ids: ['file.pdf', 'file.png', 'file.svg', 'file.xlsx', 'file.md', 'file.bpmn', 'file.wall'],
    texts: { 'file.pdf': 'menu.file_short.pdf', 'file.png': 'menu.file_short.png', 'file.svg': 'menu.file_short.svg', 'file.xlsx': 'menu.file_short.xlsx', 'file.md': 'menu.file_short.md', 'file.bpmn': 'menu.file_short.bpmn' },
  },
  {
    id: 'project', name: 'menu.file_cascade.project', ids: ['file.project-pdf', 'file.download', 'file.insert', 'file.duplicate', 'file.target'],
    texts: { 'file.project-pdf': 'menu.file_short.project_pdf' },
  },
  {
    id: 'package', name: 'menu.file_cascade.package', ids: ['file.export', 'file.import', 'file.replace'],
    texts: { 'file.import': 'menu.file_short.import', 'file.replace': 'menu.file_short.replace' },
  },
  {
    id: 'assistant', name: 'menu.file_cascade.assistant', ids: ['file.report', 'file.summaries', 'file.comments'],
    texts: { 'file.report': 'menu.file_short.report', 'file.comments': 'menu.file_short.comments' },
  },
];

/** The group of the File menu that holds a command; none for the commands at a click and the properties. */
export const fileGroupOf = (commandId: string): string | undefined => FILE_SUBMENUS.find((submenu) => submenu.ids.includes(commandId))?.id;
