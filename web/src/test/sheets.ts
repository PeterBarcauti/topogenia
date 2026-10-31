// sheets.ts
// Fictitious sheets as the API gives them (SheetView), with their documents, uses and pain points, for the tests of the sheet panel.

import type { Schema } from '../api/client.ts';
import { PROJECT_ID } from './maps.ts';

type SheetView = Schema<'SheetView'>;
type Document = Schema<'Document'>;

export const AUTHOR_ID = '0190a1b2-0000-7000-8000-00000000c001';
export const OTHER_ID = '0190a1b2-0000-7000-8000-00000000c002';
const AT = '2026-10-20T08:00:00Z';

export const contentOf = (overrides: Partial<Schema<'SheetContent'>> = {}): Schema<'SheetContent'> => ({
  actor: null, purpose: null, tools: null, frequency: null, duration_minutes: null, procedure: [], free_text: null, ...overrides,
});

/** A step as the sheet names a producer: its code and label. */
export const producerOf = (id: string, code: string, label: string): Schema<'Step'> => ({
  id, map_id: 'm1', lane_id: 'l1', code, label, source_language: 'fr', step_type: 'task', column: 0, row: 0, assignee_user_id: null,
  automation_status: 'manual', origin_operation_id: null, created_at: AT, created_by: AUTHOR_ID, updated_at: null, updated_by: null, deleted_at: null,
  deletion_batch_id: null, version: 1,
});

export const documentOf = (id: string, overrides: Partial<Document> = {}): Document => ({
  id, step_id: 's1', direction: 'output', name: `Document ${id}`, location: null, location_type: null, wait_hours: null, share_confirmed: false,
  source_language: 'fr', created_at: AT, created_by: AUTHOR_ID, updated_at: null, updated_by: null, version: 1, ...overrides,
});

export const documentUseOf = (documentId: string, stepId: string, overrides: Partial<Schema<'DocumentUse'>> = {}): Schema<'DocumentUse'> => ({
  document_id: documentId, step_id: stepId, wait_hours: null, percent_complete_accurate: null, pain_point_id: null, created_at: AT, created_by: AUTHOR_ID,
  updated_at: null, updated_by: null, version: 1, ...overrides,
});

export const painPointOf = (id: string, overrides: Partial<Schema<'PainPoint'>> = {}): Schema<'PainPoint'> => ({
  id, project_id: PROJECT_ID, step_id: 's1', code: `VTE-I00${id.slice(-1)}`, description: 'Ressaisie à la main', source_language: 'fr',
  lost_hours_per_month: null, status: 'reported', created_at: AT, created_by: AUTHOR_ID, updated_at: null, updated_by: null, version: 1, ...overrides,
});

/** An input of the sheet: produced by a step (near unless told otherwise), or external when the producer is null. */
export const inputOf = (document: Document, overrides: Partial<Schema<'ProposedInput'>> = {}): Schema<'ProposedInput'> => ({
  document, producer: producerOf('s1', 'VTE-E001', 'Demande reçue'), near: true, used: false, use: null, ...overrides,
});

/** The sheet of the step s2, empty, which its viewer may fill and validate unless told otherwise. */
export const sheetViewOf = (overrides: Partial<SheetView> = {}): SheetView => ({
  step_id: 's2', status: 'empty', completeness: 0, content: contentOf(), is_draft: true, validated: null, clarification_request: null, responsible: null,
  permissions: { fill: true, validate: true, request_clarification: false, assign: false }, suggestions: { frequency: null, tools: [] }, inputs: [],
  outputs: [], pain_points: [], updated_at: null, updated_by: null, version: null, ...overrides,
});

export const memberOf = (userId: string, name: string, role: Schema<'MemberRole'> = 'contributor'): Schema<'MemberView'> => ({
  user_id: userId, login: name.toLowerCase().replace(/\s+/g, '.'), display_name: name, role, is_sponsor: false, is_key_contributor: false, display_order: 0,
});
