// formatDate.test.ts
// The day of the browser (ADR 112 point 4): the calendar day of the person at the instant given, never the day in universal time.

import { formatLongDate, localDay } from './formatDate.ts';

const original = process.env.TZ;
afterEach(() => {
  if (original === undefined) delete process.env.TZ;
  else process.env.TZ = original;
});

/** The day written for an instant seen from a time zone. */
function dayIn(zone: string, instant: string): string {
  process.env.TZ = zone;
  return localDay(new Date(instant));
}

describe('the day of the browser is the local day, not the day in universal time', () => {
  it('is the next day at 0:30 east of Greenwich, where universal time still says the day before', () => {
    expect(dayIn('Pacific/Kiritimati', '2026-11-20T10:30:00Z')).toBe('2026-11-21');
  });

  it('is the same day at 23:30 west of Greenwich, where universal time already says the next one', () => {
    expect(dayIn('Pacific/Pago_Pago', '2026-11-21T10:30:00Z')).toBe('2026-11-20');
  });

  it('follows the change of hour: 0:30 in Berlin on the night summer time begins, and on the night it ends', () => {
    expect(dayIn('Europe/Berlin', '2026-03-29T22:30:00Z')).toBe('2026-03-30');
    expect(dayIn('Europe/Berlin', '2026-10-24T22:30:00Z')).toBe('2026-10-25');
    expect(dayIn('Europe/Berlin', '2026-10-25T22:30:00Z')).toBe('2026-10-25');
  });

  it('writes a month and a day of one digit with a zero', () => {
    expect(dayIn('Europe/Berlin', '2026-01-05T12:00:00Z')).toBe('2026-01-05');
  });
});

describe('the long form of a day, in the language of the interface', () => {
  it('writes the day with the month in letters, in French and in English', () => {
    expect(formatLongDate(new Date(2026, 10, 21, 12), 'fr')).toBe('21 novembre 2026');
    expect(formatLongDate(new Date(2026, 10, 21, 12), 'en')).toBe('November 21, 2026');
  });

  it('is the local day of the browser, never the day in universal time', () => {
    process.env.TZ = 'Pacific/Kiritimati';
    expect(formatLongDate(new Date('2026-11-20T10:30:00Z'), 'fr')).toBe('21 novembre 2026');
  });
});
