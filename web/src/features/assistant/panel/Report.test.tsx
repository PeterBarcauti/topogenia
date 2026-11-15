// Report.test.tsx
// The reply of the practice « report » in the panel of the assistant (plan 3.1, L4): what it says in a minute, and the button that saves the report with the model that wrote it; none without a map to save it from.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { sampleReport } from '../../../test/reports.ts';
import type { AssistantReply } from './assistantStore.ts';
import { Report } from './ReplyParts.tsx';
import type { ThreadContext } from './threadContext.ts';

const reply = (overrides: Partial<AssistantReply> = {}): AssistantReply => ({
  form: 'report', message: 'Le rapport de la carte.', questions: [], findings: [], proposal: null, objects: [], raw: '{}', cost: null, duration_milliseconds: 1200,
  report: sampleReport(), ...overrides,
});

const context = (saveReport?: ThreadContext['saveReport']): ThreadContext => ({
  store: undefined as never, home: false, status: null, ...(saveReport === undefined ? {} : { saveReport }),
});

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the reply of a report (plan 3.1, L4)', () => {
  it('shows what the report says in a minute and saves it with the model that wrote it', async () => {
    const saveReport = vi.fn(() => Promise.resolve());
    render(<Report reply={reply({ cost: { model: 'claude-opus-5-5', input_tokens: 1, cache_read_tokens: 0, cache_write_tokens: 0, output_tokens: 1, estimated_usd: null } })} context={context(saveReport)} />);
    expect(screen.getByTestId('assistant-report')).toHaveTextContent('Une demande reçue est vérifiée, puis facturée si elle est conforme.');
    await userEvent.click(screen.getByTestId('assistant-report-save'));
    expect(saveReport).toHaveBeenCalledWith(sampleReport(), 'claude-opus-5-5');
  });

  it('offers no saving without a map to save it from, and shows nothing without a report', () => {
    const { rerender } = render(<Report reply={reply()} context={context()} />);
    expect(screen.queryByTestId('assistant-report-save')).toBeNull();
    rerender(<Report reply={reply({ report: null })} context={context()} />);
    expect(screen.queryByTestId('assistant-report')).toBeNull();
  });
});
