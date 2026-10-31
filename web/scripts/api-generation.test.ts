// api-generation.test.ts
// The files of src/api/generated are what the generator gives for the contract the host serves, and no type of the API is written by hand (D09, S-D09-02).

// The generator reads files and runs as a script of Node: @vitest-environment node

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CONTRACT_FILE, EXCHANGES_FILE, GENERATED_FOLDER, SCHEMA_FILE, exchangeOf, exchangesOf, generateApi } from './api-generation.ts';

const read = (file: string): string => readFileSync(new URL(file, GENERATED_FOLDER), 'utf8');
const contract = JSON.parse(read(CONTRACT_FILE)) as Parameters<typeof generateApi>[0] & { components: { schemas: Record<string, unknown> } };

describe('the files generated from the contract', () => {
  it('are exactly what the generator gives for openapi.json: run npm run api after the contract changes', async () => {
    const generated = await generateApi(contract);
    expect(read(SCHEMA_FILE)).toBe(generated.schema);
    expect(read(EXCHANGES_FILE)).toBe(generated.exchanges);
  });

  it('name each schema of the contract, the body of the errors included', () => {
    const schema = read(SCHEMA_FILE);
    const names = Object.keys(contract.components.schemas);
    expect(names).toContain('ErrorBody');
    expect(names.filter((name) => !schema.includes(`        ${name}: {`) && !schema.includes(`        ${name}: `))).toEqual([]);
  });

  it('list the six operations of the exchange whose body or reply is a file, and the two streams of the assistant (read by sendStream), and only them', () => {
    expect(exchangesOf(contract)).toEqual({
      'POST /api/assistant/exchanges': { reply: 'blob', accept: 'application/x-ndjson' },
      'POST /api/maps/{id}/assistant/exchanges': { reply: 'blob', accept: 'application/x-ndjson' },
      'GET /api/projects/{id}/pmap': { reply: 'text', accept: 'text/plain' },
      'POST /api/projects/import': { body: 'binary' },
      'POST /api/projects/import/preview': { body: 'binary' },
      'POST /api/projects/{id}/export': { reply: 'blob', accept: 'application/zip' },
      'POST /api/projects/{id}/import': { body: 'binary' },
      'POST /api/projects/{id}/import/preview': { body: 'binary' },
    });
  });
});

describe('what an operation exchanges', () => {
  it('is nothing to note for JSON, for a reply without content, or for a JSON body next to a binary one', () => {
    expect(exchangeOf({ responses: { '200': { content: { 'application/json': {} } } } })).toBeNull();
    expect(exchangeOf({ responses: { '204': {} } })).toBeNull();
    expect(exchangeOf({ requestBody: { content: { 'application/json': {}, 'application/octet-stream': {} } }, responses: { '204': {} } })).toBeNull();
  });

  it('is a text, a file with its media type, or a binary body; a refusal does not count', () => {
    expect(exchangeOf({ responses: { '200': { content: { 'text/csv': {} } } } })).toEqual({ reply: 'text', accept: 'text/csv' });
    expect(exchangeOf({ responses: { '200': { content: { 'image/png': {} } }, '400': { content: { 'application/json': {} } } } }))
      .toEqual({ reply: 'blob', accept: 'image/png' });
    expect(exchangeOf({ requestBody: { content: { 'application/octet-stream': {} } }, responses: { '201': { content: { 'application/json': {} } } } }))
      .toEqual({ body: 'binary' });
    expect(exchangeOf({ responses: { '503': { content: { 'text/plain': {} } } } })).toBeNull();
  });
});

const sourceFolder = fileURLToPath(new URL('../src/', import.meta.url));

function sources(folder: string): string[] {
  return readdirSync(folder).flatMap((name) => {
    const path = join(folder, name);
    if (statSync(path).isDirectory()) return name === 'generated' ? [] : sources(path);
    return /\.tsx?$/.test(name) ? [path] : [];
  });
}

describe('the types of the API', () => {
  it('are never written by hand: no source declares a type named as a schema of the contract, but as the generated one (D09)', () => {
    const names = Object.keys(contract.components.schemas).join('|');
    const declared = new RegExp(`\\b(?:interface|class|enum)\\s+(?:${names})\\b|\\btype\\s+(?:${names})\\s*=(?!\\s*(?:components\\['schemas'\\]|Schema<))`);
    expect(declared.test("export type ErrorBody = components['schemas']['ErrorBody'];")).toBe(false);
    expect(declared.test('export interface ProfileView { id: string }')).toBe(true);
    expect(declared.test('type ErrorBody = { code: string };')).toBe(true);
    const offenders = sources(sourceFolder).filter((file) => declared.test(readFileSync(file, 'utf8')));
    expect(offenders).toEqual([]);
  });
});
