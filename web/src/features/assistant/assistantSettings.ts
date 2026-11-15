// assistantSettings.ts
// The settings of the assistant as the dialog edits them: the families of the connectors, the form drawn from the settings read, the body it sends, whether it changed (spec 3.0 section 4, ADR 098).

import type { TFunction } from 'i18next';
import { api, type Schema } from '../../api/client.ts';
import type { Classification } from '../project/classification.ts';

export type AssistantSettingsView = Schema<'AssistantSettingsView'>;
export type AssistantTestOutcome = Schema<'AssistantTestOutcome'>;
type AssistantSettingsBody = Schema<'AssistantSettingsBody'>;

/** The families of the list, by where the data go (ADR 098), each with its connectors in the order of the list. */
export const FAMILIES: readonly { readonly family: string; readonly connectors: readonly string[] }[] = [
  // One choice without a connection since 3.2 (lot J10): « copy_paste », a choice of its own until 3.1, did what « none » does.
  { family: 'none', connectors: ['none'] },
  { family: 'machine', connectors: ['ollama', 'lm_studio'] },
  { family: 'cloud', connectors: ['bedrock', 'azure_open_ai'] },
  { family: 'american', connectors: ['anthropic', 'open_ai', 'gemini', 'grok'] },
  { family: 'european', connectors: ['mistral'] },
  { family: 'chinese', connectors: ['deep_seek', 'kimi', 'qwen'] },
];

/** The connectors that call a model, and need its model and its ceiling; the classifications are set whatever the connector (F-137). */
export const CALLS_A_MODEL: readonly string[] = FAMILIES.flatMap(({ connectors }) => connectors).filter((connector) => connector !== 'none');

/** The connector as it is shown: « copy_paste », which a host or an exchange may still name, is « none » (lot J10). */
export const shownConnector = (connector: string): string => (connector === 'copy_paste' ? 'none' : connector);

/** The connectors that take a key of an API: Bedrock reads an AWS profile, a model on this machine takes none. */
export const TAKES_A_KEY: readonly string[] = CALLS_A_MODEL.filter((connector) => !['bedrock', 'ollama', 'lm_studio'].includes(connector));

/** A model on this machine, reached at its port. */
export const ON_THIS_MACHINE: readonly string[] = ['ollama', 'lm_studio'];

/**
 * The models proposed for each connector, the first by default (spec 3.0 section 5; Bedrock: inference profiles); for the
 * other makers, the aliases they keep stable or the models their documentation named on 11/11/2026, none where it is
 * unsure: their console names them (ADR 098). A deployment of Azure and a model of this machine are named by their owner.
 */
export const MODELS: Readonly<Record<string, readonly string[]>> = {
  anthropic: ['claude-opus-5-5', 'claude-sonnet-5-5'],
  bedrock: ['global.anthropic.claude-sonnet-5-5', 'global.anthropic.claude-opus-5-5', 'global.anthropic.claude-haiku-5-5'],
  open_ai: ['gpt-5.6-sol', 'gpt-5.6-terra', 'gpt-5.6-luna'],
  gemini: ['gemini-3.6-flash'],
  mistral: ['mistral-medium-latest', 'mistral-large-latest'],
  deep_seek: ['deepseek-chat', 'deepseek-reasoner'],
  qwen: ['qwen-plus', 'qwen-max'],
};

/**
 * What the administrator edits; the key is typed only, never read back: empty keeps the key kept, unless it is to be
 * removed. The ceiling and the port are kept as typed, converted when sent: emptied, a field is empty (UX-MOY-05).
 */
export interface AssistantForm {
  readonly connector: string;
  readonly model: string;
  readonly region: string;
  readonly awsProfile: string;
  readonly maxOutputTokens: string;
  readonly allowed: readonly Classification[];
  readonly key: string;
  readonly removeKey: boolean;
  readonly resource: string;
  readonly port: string;
}

export function formOf(view: AssistantSettingsView): AssistantForm {
  return {
    connector: shownConnector(view.connector), model: view.model ?? '', region: view.region ?? '', awsProfile: view.aws_profile ?? '', maxOutputTokens: String(view.max_output_tokens),
    allowed: view.allowed_classifications as Classification[], key: '', removeKey: false, resource: view.resource ?? '', port: view.port === null || view.port === undefined ? '' : String(view.port),
  };
}

const orNull = (text: string): string | null => (text.trim() === '' ? null : text.trim());

/**
 * The body of the form: the fields of the connector chosen; the key null to keep it, empty to remove it, typed to replace
 * it; the ceiling and the port as numbers, null when empty (the default of the host).
 */
export function bodyOf(form: AssistantForm): AssistantSettingsBody {
  const calls = CALLS_A_MODEL.includes(form.connector);
  const bedrock = form.connector === 'bedrock';
  const port = orNull(form.port);
  const ceiling = orNull(form.maxOutputTokens);
  return {
    connector: form.connector,
    model: calls ? orNull(form.model) : null,
    region: bedrock ? orNull(form.region) : null,
    aws_profile: bedrock ? orNull(form.awsProfile) : null,
    max_output_tokens: ceiling === null ? null : Number(ceiling),
    allowed_classifications: [...form.allowed],
    secret: !TAKES_A_KEY.includes(form.connector) ? null : form.removeKey ? '' : orNull(form.key),
    resource: form.connector === 'azure_open_ai' ? orNull(form.resource) : null,
    port: ON_THIS_MACHINE.includes(form.connector) && port !== null ? Number(port) : null,
  };
}

/** Whether the form differs from the settings read: then Save is offered. */
export function changed(view: AssistantSettingsView, form: AssistantForm): boolean {
  const sent = bodyOf(form);
  const kept = bodyOf(formOf(view));
  return JSON.stringify({ ...sent, secret: null }) !== JSON.stringify({ ...kept, secret: null }) || sent.secret !== null;
}

/**
 * Where the data of an exchange go, as the panel says it: Bedrock in its region, Claude by its API, every other connector
 * by its name (ADR 098).
 */
export function destinationOf(t: TFunction, connector: string, region: string | null | undefined): string {
  if (connector === 'bedrock') return t('assistant.panel.destination_bedrock', { region: region ?? '' });
  if (connector === 'anthropic') return t('assistant.panel.destination_anthropic');
  return t(`assistant.connectors.${shownConnector(connector)}`);
}

export const readSettings = (signal?: AbortSignal) => api.get('/api/admin/assistant', { signal });

export const saveSettings = (form: AssistantForm, version: number) => api.put('/api/admin/assistant', { body: bodyOf(form), version });

export const testConnection = (form: AssistantForm) => api.post('/api/admin/assistant/test', { body: bodyOf(form) });
