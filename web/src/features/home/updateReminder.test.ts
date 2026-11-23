// updateReminder.test.ts
// The rule of the reminder of the home page (ADR 111): from the 90th day after the release, to the administrator alone, never for the Store version nor without links; closed, back 90 days later; the days counted in the local calendar.

import { UPDATE_REMINDER_DAYS, dayKey, reminderDue, type ReminderInput } from './updateReminder.ts';

const RELEASE = '2026-11-21';

/** The base case: an administrator, the portable version, links given, never closed, at the given time. */
const input = (now: Date, changes: Partial<ReminderInput> = {}): ReminderInput => ({
  releaseDay: RELEASE, now, closedDay: null, isAdmin: true, store: false, hasLinks: true, ...changes,
});

/** The local time `days` after a local day, at the given hour. */
const after = (year: number, month: number, day: number, days: number, hour = 12, minute = 0) => new Date(year, month - 1, day + days, hour, minute);

describe('the reminder (ADR 111)', () => {
  it('is 90 days', () => {
    expect(UPDATE_REMINDER_DAYS).toBe(90);
  });

  it('shows the reminder from the 90th day after the release, not before', () => {
    expect(reminderDue(input(after(2026, 11, 21, 0)))).toBe(false);
    expect(reminderDue(input(after(2026, 11, 21, 89)))).toBe(false);
    expect(reminderDue(input(after(2026, 11, 21, 90)))).toBe(true);
    expect(reminderDue(input(after(2026, 11, 21, 400)))).toBe(true);
  });

  it('a reminder closed comes back 90 days after it was closed, not before', () => {
    const closed = { closedDay: '2027-03-01' };
    expect(reminderDue(input(after(2027, 3, 1, 0), closed))).toBe(false);
    expect(reminderDue(input(after(2027, 3, 1, 89), closed))).toBe(false);
    expect(reminderDue(input(after(2027, 3, 1, 90), closed))).toBe(true);
  });

  it('a closing day unreadable or to come counts as none', () => {
    const now = after(2027, 3, 1, 0);
    for (const closedDay of ['', 'hier', '2027-13-45', '2027-02-30', '1/3/2027', '2027-03-01T10:00:00', '2099-01-01', '2027-03-02']) {
      expect([closedDay, reminderDue(input(now, { closedDay }))]).toEqual([closedDay, true]);
    }
  });

  it('the days are counted in the local calendar, whatever the hour and the change of summer time', () => {
    expect(reminderDue(input(after(2027, 2, 19, 0, 0, 1)))).toBe(true);
    expect(reminderDue(input(after(2027, 2, 18, 0, 23, 59)))).toBe(false);
    // Across the changes of hour of spring and autumn: a day is a day of the calendar, not 24 hours.
    expect(reminderDue(input(after(2027, 3, 28, 0, 0, 30), { releaseDay: '2026-12-28' }))).toBe(true);
    expect(reminderDue(input(after(2027, 3, 27, 0, 23, 30), { releaseDay: '2026-12-28' }))).toBe(false);
    expect(reminderDue(input(after(2027, 10, 31, 0, 0, 30), { releaseDay: '2027-08-02' }))).toBe(true);
    expect(reminderDue(input(after(2027, 10, 30, 0, 23, 30), { releaseDay: '2027-08-02' }))).toBe(false);
    expect(dayKey(new Date(2027, 0, 5, 23, 59))).toBe('2027-01-05');
    expect(dayKey(new Date(2027, 0, 5, 0, 0))).toBe('2027-01-05');
  });

  it('never for a user who is not administrator, for the Store version, nor without links', () => {
    const old = after(2027, 6, 1, 0);
    expect(reminderDue(input(old))).toBe(true);
    expect(reminderDue(input(old, { isAdmin: false }))).toBe(false);
    expect(reminderDue(input(old, { store: true }))).toBe(false);
    expect(reminderDue(input(old, { hasLinks: false }))).toBe(false);
  });

  it('a release day unreadable shows nothing', () => {
    expect(reminderDue(input(after(2027, 6, 1, 0), { releaseDay: 'unknown' }))).toBe(false);
  });
});
