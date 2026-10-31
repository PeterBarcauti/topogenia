// api.ts
// The real API called by the journeys to lay out and read their data, with the identity of the browser: anti-CSRF header on writes, If-Match from a version.

import type { APIRequestContext, Page } from '@playwright/test';
import type { components } from '../../src/api/generated/schema.ts';

type Schemas = components['schemas'];
export type MapRead = Schemas['MapView'];
export type StepRead = Schemas['StepView'];
export type FlowRead = Schemas['FlowView'];
export type ProjectRead = Schemas['ProjectDetail'];
type Structure = Schemas['StructureReply'];

/** One exchange with the host: what the journeys send, what they read back. */
export type Transport = (method: string, path: string, headers: Record<string, string>, body?: unknown) => Promise<{ status: number; text: string }>;

/** Desktop mode: the request of Playwright, which carries the session cookie of the launch. */
export const requestTransport = (request: APIRequestContext): Transport => async (method, path, headers, body) => {
  const response = await request.fetch(path, { method, headers, ...(body === undefined ? {} : { data: body }) });
  return { status: response.status(), text: await response.text() };
};

/** Server mode: only the browser carries the Windows identity (Negotiate), so the calls leave from a page of the application. */
export const pageTransport = (page: Page): Transport => (method, path, headers, body) =>
  page.evaluate(async ([verb, address, sent, content]) => {
    const response = await fetch(address, {
      method: verb,
      credentials: 'same-origin',
      headers: content === undefined ? sent : { ...sent, 'Content-Type': 'application/json' },
      ...(content === undefined ? {} : { body: JSON.stringify(content) }),
    });
    return { status: response.status, text: await response.text() };
  }, [method, path, headers, body] as const);

/** The calls of the journeys; a refusal is thrown with its status and body, so that a broken set-up says why. */
export class Api {
  constructor(private readonly transport: Transport) {}

  async get<T>(path: string): Promise<T> {
    return this.send<T>('GET', path);
  }

  async send<T>(method: string, path: string, body?: unknown, version?: number): Promise<T> {
    const headers: Record<string, string> = {};
    if (method !== 'GET') headers['X-Requested-With'] = 'XMLHttpRequest';
    if (version !== undefined) headers['If-Match'] = `"${version}"`;
    const { status, text } = await this.transport(method, path, headers, body);
    if (status < 200 || status > 299) throw new Error(`${method} ${path}: ${status} ${text}`);
    return (text === '' ? undefined : JSON.parse(text)) as T;
  }

  /** A new project with its root map; `members` empty, every other optional field left out unless given (A-1.2-08). */
  async project(code: string, name: string, sourceLanguage = 'fr', classification: string | null = null): Promise<{ id: string; code: string; mapId: string }> {
    const created = await this.send<Schemas['CreatedProject']>('POST', '/api/projects', {
      code, name, source_language: sourceLanguage, translations: null, description: null, shared_folder: null,
      classification, sharing_group_label: null, members: [],
    });
    return { id: created.id, code: created.code, mapId: created.root_map_id };
  }

  /** The map as the host reads it (`GET /api/maps/{id}` answers the map and its history). */
  async map(mapId: string): Promise<MapRead> {
    return (await this.get<Schemas['MapReply']>(`/api/maps/${mapId}`)).map;
  }

  async lane(mapId: string, name: string, type = 'department'): Promise<string> {
    const reply = await this.send<Structure>('POST', `/api/maps/${mapId}/lanes`, {
      name, type, color: null, sort_order: null, actor_user_id: null, translations: null,
    });
    return reply.id ?? '';
  }

  async step(mapId: string, laneId: string, column: number, label: string, type = 'task', row: number | null = null): Promise<string> {
    const reply = await this.send<Structure>('POST', `/api/maps/${mapId}/steps`, { lane_id: laneId, column, label, type, row });
    return reply.id ?? '';
  }

  async flow(mapId: string, source: string, target: string, label: string | null = null): Promise<string> {
    const reply = await this.send<Structure>('POST', `/api/maps/${mapId}/flows`, { source_step_id: source, target_step_id: target, label });
    return reply.id ?? '';
  }

  /** The profile back in French, the language of the journeys (a journey may switch the interface to another one). */
  async resetLanguage(): Promise<void> {
    const me = await this.get<Schemas['ProfileView']>('/api/me');
    if (me.language !== 'fr') await this.send('PATCH', '/api/me', { language: 'fr' }, me.version);
  }

  /** The settings of the assistant back to no connector, which removes the key kept (spec 3.0 section 4). */
  async resetAssistant(): Promise<void> {
    const settings = await this.get<Schemas['AssistantSettingsView']>('/api/admin/assistant');
    const allowed = settings.allowed_classifications.join(',');
    if (settings.connector === 'none' && !settings.has_secret && allowed === 'internal' && settings.max_output_tokens === 64000) return;
    await this.send('PUT', '/api/admin/assistant', {
      connector: 'none', model: null, region: null, aws_profile: null, max_output_tokens: 64000, allowed_classifications: ['internal'], secret: null,
    }, settings.version);
  }

  /** Every project to the trash, then out of it for good: the journeys start from an empty base (1.x `purger.sql`). */
  async clearProjects(): Promise<void> {
    for (const item of await this.get<Schemas['ProjectListItem'][]>('/api/projects')) {
      const project = await this.get<ProjectRead>(`/api/projects/${item.id}`);
      await this.send('DELETE', `/api/projects/${item.id}?confirmation=${encodeURIComponent(project.code)}`, undefined, project.version);
    }
    const trash = await this.get<Schemas['TrashView']>('/api/trash');
    for (const item of trash.projects) await this.send('DELETE', `/api/trash/${item.id}`);
  }
}
