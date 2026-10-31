// assistantSettings.ts
// The settings of the assistant as the dialog edits them: the form drawn from the settings read, the body it sends, whether it changed (spec 3.0 section 4).

import { api, type Schema } from '../../api/client.ts';
import type { Classification } from '../project/classification.ts';

export type AssistantSettingsView = Schema<'AssistantSettingsView'>;
export type AssistantTestOutcome = Schema<'AssistantTestOutcome'>;
type AssistantSettingsBody = Schema<'AssistantSettingsBody'>;

/** The connectors that call a model, and need its model, its ceiling and the classifications that may go to it. */
export const CALLS_A_MODEL: readonly string[] = ['anthropic', 'bedrock'];

/** The models proposed for each connector, the first by default (spec 3.0 section 5; Bedrock: inference profiles). */
export const MODELS: Readonly<Record<string, readonly string[]>> = {
  anthropic: ['claude-opus-5-5', 'claude-sonnet-5-5'],
  bedrock: ['global.anthropic.claude-sonnet-5-5', 'global.anthropic.claude-opus-5-5', 'global.anthropic.claude-haiku-5-5'],
};

/** What the administrator edits; the key is typed only, never read back: empty keeps the key kept, unless it is to be removed. */
export interface AssistantForm {
  readonly connector: string;
  readonly model: string;
  readonly region: string;
  readonly awsProfile: string;
  readonly maxOutputTokens: number;
  readonly allowed: readonly Classification[];
  readonly key: string;
  readonly removeKey: boolean;
}

export function formOf(view: AssistantSettingsView): AssistantForm {
  return {
    connector: view.connector, model: view.model ?? '', region: view.region ?? '', awsProfile: view.aws_profile ?? '', maxOutputTokens: view.max_output_tokens,
    allowed: view.allowed_classifications as Classification[], key: '', removeKey: false,
  };
}

const orNull = (text: string): string | null => (text.trim() === '' ? null : text.trim());

/** The body of the form: the fields of the connector chosen; the key null to keep it, empty to remove it, typed to replace it. */
export function bodyOf(form: AssistantForm): AssistantSettingsBody {
  const calls = CALLS_A_MODEL.includes(form.connector);
  const bedrock = form.connector === 'bedrock';
  return {
    connector: form.connector,
    model: calls ? orNull(form.model) : null,
    region: bedrock ? orNull(form.region) : null,
    aws_profile: bedrock ? orNull(form.awsProfile) : null,
    max_output_tokens: form.maxOutputTokens,
    allowed_classifications: [...form.allowed],
    secret: form.connector !== 'anthropic' ? null : form.removeKey ? '' : orNull(form.key),
  };
}

/** Whether the form differs from the settings read: then Save is offered. */
export function changed(view: AssistantSettingsView, form: AssistantForm): boolean {
  const sent = bodyOf(form);
  const kept = bodyOf(formOf(view));
  return JSON.stringify({ ...sent, secret: null }) !== JSON.stringify({ ...kept, secret: null }) || sent.secret !== null;
}

export const readSettings = (signal?: AbortSignal) => api.get('/api/admin/assistant', { signal });

export const saveSettings = (form: AssistantForm, version: number) => api.put('/api/admin/assistant', { body: bodyOf(form), version });

export const testConnection = (form: AssistantForm) => api.post('/api/admin/assistant/test', { body: bodyOf(form) });
