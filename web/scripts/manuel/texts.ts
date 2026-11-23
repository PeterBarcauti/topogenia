// texts.ts
// The language of the captures of the manual (French by default, English with MANUEL_LANG=en), the texts of the interface read by their keys, the codes of the projects, and the purchase process and the analysis pasted back as the replies of the AI of the person.

import { readFileSync } from 'node:fs';

/** The name of the .pmap format: the name of the product, read where it is written (D13). */
export const PRODUCT = (JSON.parse(readFileSync(new URL('../../src/shared/i18n/locales/fr.json', import.meta.url), 'utf-8')) as { app: { name: string } }).app.name.toLowerCase();
/** The language of the captures: French by default, English with MANUEL_LANG=en. */
export const LANGUAGE = process.env.MANUEL_LANG ?? 'fr';
if (LANGUAGE !== 'fr' && LANGUAGE !== 'en') throw new Error('MANUEL_LANG is fr or en.');
export const FRENCH = LANGUAGE === 'fr';
type Messages = { [key: string]: string | Messages };
const MESSAGES = JSON.parse(readFileSync(new URL(`../../src/shared/i18n/locales/${LANGUAGE}.json`, import.meta.url), 'utf-8')) as Messages;
/** A text of the interface in the language of the captures, by its key: the names the gestures look for. */
export function label(key: string): string {
  const found = key.split('.').reduce<string | Messages | undefined>((node, part) => (typeof node === 'object' ? node[part] : undefined), MESSAGES);
  if (typeof found !== 'string') throw new Error(`No text ${key} in ${LANGUAGE}.json.`);
  return found;
}
/** The sample offered in the language of the interface (ADR 088): CONGES written in French, LEAVE written in English. */
export const SAMPLE = FRENCH ? 'CONGES' : 'LEAVE';
/** The name of the small project of purchases, whose code is the one proposed from it (ADR 111), and the target state made from the sample. */
export const PURCHASE_NAME = FRENCH ? 'Demande d’achat' : 'Purchase request';
export const TARGET = FRENCH ? 'CIBLE' : 'TARGET';
export const code = (number: number): string => `${SAMPLE}-E${String(number).padStart(3, '0')}`;

/** A small purchase process, pasted back as the reply of the AI of the person: the desired state of the empty map. */
const PROPOSAL_FR = {
  format: PRODUCT, version: '1.2', project: { name: 'Demande d’achat' },
  map: {
    lanes: [{ id: 'c1', name: 'Demandeur' }, { id: 'c2', name: 'Acheteur', type: 'department' }, { id: 'c3', name: 'Direction', type: 'department' }],
    steps: [
      { id: 'e1', lane: 'c1', label: 'Besoin identifié', type: 'start' }, { id: 'e2', lane: 'c1', label: 'Saisir la demande d’achat' },
      { id: 'e3', lane: 'c2', label: 'Montant > 5 000 € ?', type: 'decision' }, { id: 'e4', lane: 'c3', label: 'Valider la dépense' },
      { id: 'e5', lane: 'c2', label: 'Passer la commande' }, { id: 'e6', lane: 'c1', label: 'Commande passée', type: 'end' }],
    flows: [
      { source: 'e1', target: 'e2' }, { source: 'e2', target: 'e3' }, { source: 'e3', target: 'e4', label: 'oui' },
      { source: 'e3', target: 'e5', label: 'non' }, { source: 'e4', target: 'e5' }, { source: 'e5', target: 'e6' }],
  },
};

/** What the person asks the assistant on the sample, and the reply of their AI pasted back. */
const ANALYSIS_FR = {
  request: 'Qu’est-ce qui ralentit le traitement d’une demande ?',
  reply: {
    form: 'findings', message: 'Trois points à regarder sur la demande de congés.', cited: [],
    findings: [
      { kind: 'loop', title: 'Boucle de correction', detail: 'Une demande non conforme repart en saisie, sans délai ni limite de tours.',
        objects: [code(8), code(2)], suggestion: 'Fixer un délai de correction et prévenir le manager au-delà.' },
      { kind: 're_entry', title: 'Solde ressaisi', detail: 'Le solde est lu dans la paie, puis comparé à la main à la demande.',
        objects: [code(13), code(14)], suggestion: 'Afficher le solde dans le formulaire de demande.' },
      { kind: 'break', title: 'Rupture de support', detail: 'La demande arrive par le portail, l’avis du manager repart par courriel.',
        objects: [code(4)], suggestion: 'Répondre dans le portail, là où la demande est saisie.' }],
  },
};

/** The same purchase process, written in English. */
const PROPOSAL_EN = {
  format: PRODUCT, version: '1.2', project: { name: 'Purchase request' },
  map: {
    lanes: [{ id: 'c1', name: 'Requester' }, { id: 'c2', name: 'Buyer', type: 'department' }, { id: 'c3', name: 'Management', type: 'department' }],
    steps: [
      { id: 'e1', lane: 'c1', label: 'Need identified', type: 'start' }, { id: 'e2', lane: 'c1', label: 'Enter the purchase request' },
      { id: 'e3', lane: 'c2', label: 'Amount > €5,000?', type: 'decision' }, { id: 'e4', lane: 'c3', label: 'Approve the expense' },
      { id: 'e5', lane: 'c2', label: 'Place the order' }, { id: 'e6', lane: 'c1', label: 'Order placed', type: 'end' }],
    flows: [
      { source: 'e1', target: 'e2' }, { source: 'e2', target: 'e3' }, { source: 'e3', target: 'e4', label: 'yes' },
      { source: 'e3', target: 'e5', label: 'no' }, { source: 'e4', target: 'e5' }, { source: 'e5', target: 'e6' }],
  },
};

/** The same analysis of the leave request, written in English. */
const ANALYSIS_EN = {
  request: 'What slows down the handling of a request?',
  reply: {
    form: 'findings', message: 'Three points to look at in the leave request.', cited: [],
    findings: [
      { kind: 'loop', title: 'Correction loop', detail: 'A request that is not compliant goes back to be entered again, with no deadline and no limit on the rounds.',
        objects: [code(8), code(2)], suggestion: 'Set a deadline for the correction and warn the manager beyond it.' },
      { kind: 're_entry', title: 'Balance entered again', detail: 'The balance is read in the payroll, then compared by hand with the request.',
        objects: [code(13), code(14)], suggestion: 'Show the balance in the request form.' },
      { kind: 'break', title: 'Media break', detail: 'The request arrives through the portal, the opinion of the manager goes back by email.',
        objects: [code(4)], suggestion: 'Reply in the portal, where the request is entered.' },
    ],
  },
};

export const PROPOSAL = FRENCH ? PROPOSAL_FR : PROPOSAL_EN;
export const ANALYSIS = FRENCH ? ANALYSIS_FR : ANALYSIS_EN;
