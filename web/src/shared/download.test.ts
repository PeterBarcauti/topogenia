// download.test.ts
// The name of an exported file follows the rule of the host (A-1.2-16, the cases of ExportFileNameTests), and the file is given to the browser.

import { captureDownloads } from '../test/downloads.ts';
import { download, exportFileName, shortName } from './download.ts';
import { PACKAGE_EXTENSION } from './i18n/i18n.ts';

afterEach(() => {
  vi.useRealTimers();
});

describe('the name of an exported file (A-1.2-16, B-1.6-24)', () => {
  it.each([
    ['CONGES-E017', 'Tableau récapitulatif', 'CONGES-E017_Tableau-recapitulatif'],
    ['CONGES-E001', 'Relevé des soldes : congés / RTT', 'CONGES-E001_Releve-des-soldes-conges-RTT'],
    ['CONGES-E002', 'Complétion du fichier des absences de l\'équipe', 'CONGES-E002_Completion-du-fichier-des-abse'],
    ['CONGES-E003', 'Œuvre sociale', 'CONGES-E003_OEuvre-sociale'],
    ['CONGES-E004', '!!!', 'CONGES-E004'],
    ['CONGES-E005', '  _-Straße - Ærø-_  ', 'CONGES-E005_Strasse-AEr'],
  ])('%s « %s » gives the short name of the host', (code, label, expected) => {
    expect(shortName(code, label)).toBe(expected);
  });

  it('cuts the short name at thirty characters, never on a dash', () => {
    expect(shortName('CONGES', 'Demande de congés annuels - suite')).toBe('CONGES_Demande-de-conges-annuels-suit');
    expect(shortName('CONGES', `${'a'.repeat(29)} b`)).toBe(`CONGES_${'a'.repeat(29)}`);
  });

  it('ends with the UTC day and the extension', () => {
    expect(exportFileName('CONGES', 'Demande de congés', PACKAGE_EXTENSION, new Date('2026-10-25T23:59:00Z'))).toBe(`CONGES_Demande-de-conges_2026-10-25${PACKAGE_EXTENSION}`);
    expect(exportFileName('VTE', 'Ventes', '.pmap', new Date('2026-01-02T00:30:00+02:00'))).toBe('VTE_Ventes_2026-01-01.pmap');
  });
});

describe('the download', () => {
  it('gives the file to the browser under its name, leaves no link in the page, then frees it', () => {
    vi.useFakeTimers();
    const capture = captureDownloads();
    try {
      const file = new Blob(['{}']);
      download(file, 'VTE_Ventes_2026-10-28.pmap');
      expect(capture.files).toEqual([{ name: 'VTE_Ventes_2026-10-28.pmap', content: file }]);
      expect(document.querySelector('a')).toBeNull();
      expect(capture.revoked).toEqual([]);
      vi.advanceTimersByTime(10_000);
      expect(capture.revoked).toEqual(['blob:fake/1']);
    } finally {
      capture.restore();
    }
  });
});
