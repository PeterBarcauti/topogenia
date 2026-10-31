// generate-api.mjs
// Writes src/api/generated/schema.ts and exchanges.ts from openapi.json, the contract the host serves (D09): npm run api.

import { readFileSync, writeFileSync } from 'node:fs';
import { CONTRACT_FILE, EXCHANGES_FILE, GENERATED_FOLDER, SCHEMA_FILE, generateApi } from './api-generation.ts';

const contract = JSON.parse(readFileSync(new URL(CONTRACT_FILE, GENERATED_FOLDER), 'utf8'));
const generated = await generateApi(contract);

function current(target) {
  try {
    return readFileSync(target, 'utf8');
  } catch {
    return null;
  }
}

for (const [file, text] of [[SCHEMA_FILE, generated.schema], [EXCHANGES_FILE, generated.exchanges]]) {
  const target = new URL(file, GENERATED_FOLDER);
  const upToDate = current(target) === text;
  if (!upToDate) writeFileSync(target, text);
  console.log(upToDate ? `${file} already up to date` : `${file} written`);
}
