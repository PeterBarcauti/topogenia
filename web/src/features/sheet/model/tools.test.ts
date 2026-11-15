// tools.test.ts
// The tools of a sheet as pills (essai J10): the tint of a known software and the one drawn from a name, the tools a typing ends, a tool never kept twice, the tools settled while one is typed.

import { describe, expect, it } from 'vitest';
import { distinctTools, joinTools, settledTools, toolTint, typedTools } from './tools.ts';

describe('the tint of a tool', () => {
  it('is the one of the office software its name cites, whatever its case and the words around it', () => {
    expect(['Excel', 'fichier EXCEL de suivi', 'Word', 'PowerPoint', 'Power Point', 'Outlook', 'Teams', 'PDF', 'SharePoint', 'Power BI'].map(toolTint))
      .toEqual([4, 4, 3, 5, 5, 8, 2, 1, 8, 6]);
  });

  it('is drawn from the name for any other tool: the same for the same tool whatever its case, one of the eight tints', () => {
    expect(toolTint('Portail du fournisseur')).toBe(toolTint('  portail du FOURNISSEUR '));
    expect(['Tablette', 'Portail', 'Messagerie', 'Registre papier', 'Outil de gestion des temps'].map(toolTint).every((tint) => tint >= 1 && tint <= 8)).toBe(true);
    // A word that only holds the name of a software is not that software.
    expect(toolTint('Excellence')).toBe(toolTint('excellence'));
  });
});

describe('the tools typed', () => {
  it('are ended by a comma, a semicolon or a line break, what follows the last one still being typed', () => {
    expect(typedTools('Excel, extraction SIG R205')).toEqual({ ended: ['Excel'], typing: ' extraction SIG R205' });
    expect(typedTools('Excel;ERP\n')).toEqual({ ended: ['Excel', 'ERP'], typing: '' });
    expect(typedTools('Tabl')).toEqual({ ended: [], typing: 'Tabl' });
    expect(typedTools(' , ,')).toEqual({ ended: [], typing: '' });
  });

  it('are never kept twice, whatever the case, the first writing kept', () => {
    expect(distinctTools(['ERP', 'Excel', 'erp', 'EXCEL', 'Outlook'])).toEqual(['ERP', 'Excel', 'Outlook']);
  });

  it('are written with a comma between two', () => {
    expect([joinTools(['Excel', 'ERP']), joinTools([])]).toEqual(['Excel, ERP', '']);
  });
});

describe('the tools settled', () => {
  it('are those of the field but the one being typed, its last one', () => {
    expect(settledTools('Excel, ERP, Outl', 'Outl')).toEqual(['Excel', 'ERP']);
    expect(settledTools('Excel; ERP\nOutlook', '')).toEqual(['Excel', 'ERP', 'Outlook']);
    // The same name typed again after a pill: the pill stays, the typing is the last one.
    expect(settledTools('Excel, Excel', ' Excel')).toEqual(['Excel']);
    expect(settledTools('', '')).toEqual([]);
  });
});
