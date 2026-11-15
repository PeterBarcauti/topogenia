// fileCommands.ts
// The commands of the File menu of the map: open a .pmap file, import a package as a new project, download a copy, duplicate the project, export the map as a PDF, a PNG or a workbook, print it large for a wall (Makigami M9), export the project, replace it by a package, its properties (annex B 2.4, 2.14, spec 4.1, ADR 058, ADR 066, AR-154).

import { CopyPlus, Crosshair, Download, FileCode, FileImage, FileSpreadsheet, FileText, FileUp, FolderOpen, LayoutGrid, MessageSquareText, Package, ScrollText, SlidersHorizontal, VectorSquare } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import { PACKAGE_EXTENSION } from '../../../../shared/i18n/i18n.ts';
import { openProjectProperties } from '../../../project/properties/projectProperties.ts';
import { askReport, exportMap } from '../actions/exports.ts';
import { askSummaries } from '../actions/summaries.ts';
import { askReplacement, downloadCopy } from '../actions/files.ts';
import type { Floating } from '../floating.ts';
import type { MapContext } from './context.ts';

const admin = (context: MapContext) => context.editor.admin;
const facilitator = (context: MapContext) => context.canEdit;
const extension = () => ({ extension: PACKAGE_EXTENSION });

const opens = (floating: Floating) => (context: MapContext) => {
  context.editor.floating.getState().open(floating);
};

/**
 * [admin] Ouvrir un fichier…, [admin] Importer un paquet {extension}… (a new project, as on the home page, AR-154),
 * Télécharger une copie, [admin] Dupliquer le projet… (B-2.4-53 to B-2.4-55), [admin] Créer l'état cible… (Makigami M6, P3e); the exports of the map as a PDF A3, a PNG
 * and a workbook, for every reader (B-2.4-56 to B-2.4-58); the package of exchange for the facilitator, exported or
 * replacing the project (AR-113, ADR 052, ADR 058); the properties of the project for every member, the dialog the header
 * shows reading them only for who may not change them (spec 4.1).
 */
export const FILE_COMMANDS: readonly Command<MapContext>[] = [
  { id: 'file.open', label: 'files.open', icon: FolderOpen, enabled: () => true, visible: admin, run: opens({ kind: 'open-pmap' }) },
  { id: 'file.import', label: 'package.import', labelValues: extension, icon: FileUp, enabled: () => true, visible: admin, run: opens({ kind: 'import-package' }) },
  { id: 'file.download', label: 'files.download', icon: Download, enabled: () => true, run: (context) => void downloadCopy(context.editor, context.map) },
  { id: 'file.duplicate', label: 'files.duplicate', icon: CopyPlus, enabled: () => true, visible: admin, run: opens({ kind: 'duplicate' }) },
  { id: 'file.target', label: 'files.target', icon: Crosshair, enabled: () => true, visible: admin, run: opens({ kind: 'target' }) },
  { id: 'file.pdf', label: 'export.pdf', icon: FileText, enabled: () => true, run: (context) => void exportMap(context.editor, 'pdf') },
  // The map on pages A4 or A3 to assemble on a wall, for every reader as the other exports (Makigami M9).
  { id: 'file.wall', label: 'export.wall', icon: LayoutGrid, enabled: () => true, run: opens({ kind: 'wall-print' }) },
  { id: 'file.png', label: 'export.png', icon: FileImage, enabled: () => true, run: (context) => void exportMap(context.editor, 'png') },
  { id: 'file.svg', label: 'export.svg', icon: VectorSquare, enabled: () => true, run: opens({ kind: 'export-svg' }) },
  { id: 'file.xlsx', label: 'export.xlsx', icon: FileSpreadsheet, enabled: () => true, run: (context) => void exportMap(context.editor, 'xlsx') },
  { id: 'file.md', label: 'export.md', icon: FileCode, enabled: () => true, run: (context) => void exportMap(context.editor, 'md') },
  { id: 'file.report', label: 'export.report', icon: ScrollText, enabled: () => true, run: (context) => void askReport(context.editor) },
  // The summaries of the maps, written in the structure: the facilitator's (plan 3.1, lot R).
  { id: 'file.summaries', label: 'summary.menu', icon: MessageSquareText, enabled: () => true, visible: facilitator, run: (context) => void askSummaries(context.editor) },
  { id: 'file.export', label: 'package.export', labelValues: extension, icon: Package, enabled: () => true, visible: facilitator, run: opens({ kind: 'export-package' }) },
  { id: 'file.replace', label: 'package.replace', labelValues: extension, icon: FileUp, enabled: () => true, visible: facilitator,
    run: (context) => void askReplacement(context.editor, context.map) },
  { id: 'file.properties', label: 'project.properties', icon: SlidersHorizontal, enabled: () => true,
    run: () => {
      openProjectProperties();
    } },
];
