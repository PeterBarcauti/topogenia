// SheetPanel.test.tsx
// The sheet panel: its head, its closing, the person responsible, the two modes, the saving as typed, the validation, the clarification (annex B 4).

import { fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../shared/i18n/i18n.ts';
import { callsTo } from '../../test/fakeApi.ts';
import { mountSheet, renderSheet, STEP } from '../../test/sheetPanel.tsx';
import { AUTHOR_ID, contentOf, OTHER_ID, sheetViewOf } from '../../test/sheets.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the head of the panel (B-4-02, B-4-07 to B-4-12, B-2.1-39, B-8.1-35)', () => {
  it('an aside named after its step: « CODE · libellé », « Couloir : nom · Fiche : état » in the colour of the state, the gauge of the host', async () => {
    await renderSheet(sheetViewOf({ status: 'draft', completeness: 60 }));
    const panel = screen.getByTestId('sheet-panel');
    expect([panel.tagName, panel.getAttribute('aria-label'), panel.classList.contains('side-panel')]).toEqual(['ASIDE', 'Fiche de l’étape VTE-E002', true]);
    expect(screen.getByTestId('sheet-title')).toHaveTextContent('VTE-E002 · Vérifier la demande');
    const meta = screen.getByTestId('sheet-meta');
    expect([...meta.children].map((part) => part.textContent)).toEqual(['Couloir : Commercial', '·', 'Fiche :', 'brouillon', 'Enregistré']);
    expect(screen.getByTestId('sheet-state')).toHaveClass('state', 'state--draft');
    const gauge = screen.getByRole('progressbar', { name: 'Complétude de la fiche' });
    expect([gauge.getAttribute('aria-valuenow'), gauge.getAttribute('aria-valuemin'), gauge.getAttribute('aria-valuemax')]).toEqual(['60', '0', '100']);
    expect(screen.getByText('60 %')).toBeInTheDocument();
    expect(screen.queryByTestId('sheet-clarification')).toBeNull();
  });

  it('the class of each state is its code; the clarification asked shows in its box', async () => {
    await renderSheet(sheetViewOf({ status: 'needs_review', clarification_request: 'Quel outil pour l’extraction ?' }));
    expect(screen.getByTestId('sheet-state')).toHaveClass('state--needs_review');
    expect(screen.getByTestId('sheet-state')).toHaveTextContent('à revoir');
    const note = screen.getByRole('note');
    expect(note).toHaveTextContent('Précision demandée par l’animateur');
    expect(note).toHaveTextContent('Quel outil pour l’extraction ?');
  });

  it('says the sheet is loading until the host answers', () => {
    mountSheet({ 'GET /api/steps/s2/sheet': { pending: true } }, { step: { ...STEP, laneName: '' } });
    expect(screen.getByTestId('sheet-loading')).toHaveTextContent('Chargement de la fiche…');
    expect(screen.getByTestId('sheet-meta').textContent).toBe('');
  });
});

describe('closing the panel (B-4-03, B-4-04, B-8.1-34)', () => {
  it('by its button', async () => {
    const { record } = await renderSheet(sheetViewOf());
    fireEvent.click(screen.getByRole('button', { name: 'Fermer la fiche' }));
    expect(record.events).toEqual(['close']);
  });

  it('by Escape, the focus in one of its fields or out of any field', async () => {
    const { record } = await renderSheet(sheetViewOf());
    fireEvent.keyDown(screen.getByTestId('sheet-actor'), { key: 'Escape' });
    fireEvent.keyDown(document.body, { key: 'Escape' });
    expect(record.events).toEqual(['close', 'close']);
  });

  it('Escape in the form of a document cancels the form, the panel stays', async () => {
    const { record } = await renderSheet(sheetViewOf());
    fireEvent.click(screen.getByTestId('output-add'));
    fireEvent.keyDown(screen.getByTestId('document-form-name'), { key: 'Escape' });
    expect([screen.queryByTestId('document-form-output'), record.events]).toEqual([null, []]);
  });
});

describe('the person responsible (B-4-13 to B-4-16)', () => {
  const assign = { fill: true, validate: true, request_clarification: true, assign: true };

  it('the facilitator chooses among the members who are not readers; the choice goes to the map, then the sheet is read again', async () => {
    const { record, sent } = await renderSheet(sheetViewOf({ permissions: assign }));
    const list = screen.getByLabelText('Responsable de la fiche');
    expect(within(list).getAllByRole('option').map((option) => option.textContent)).toEqual(['Personne en particulier', 'Camille Bernard']);
    fireEvent.change(list, { target: { value: AUTHOR_ID } });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'GET', '/api/steps/s2/sheet')).toHaveLength(2);
    });
    fireEvent.change(list, { target: { value: '' } });
    await vi.waitFor(() => {
      expect(record.assigned).toEqual([AUTHOR_ID, null]);
    });
  });

  it('the empty choice names the person of the lane who answers when nobody is chosen; nobody in particular when a member is chosen', async () => {
    const first = await renderSheet(sheetViewOf({ permissions: assign, responsible: { user_id: OTHER_ID, origin: 'lane' } }));
    expect(within(screen.getByLabelText('Responsable de la fiche')).getAllByRole('option')[0]).toHaveTextContent('Alex Martin (personne du couloir)');
    first.unmount();
    await renderSheet(sheetViewOf({ permissions: assign, responsible: { user_id: AUTHOR_ID, origin: 'step' } }));
    expect(within(screen.getByLabelText('Responsable de la fiche')).getAllByRole('option')[0]).toHaveTextContent('Personne en particulier');
  });

  it('without the right to assign: « Responsable : nom », a person who left the project named as such', async () => {
    await renderSheet(sheetViewOf({ responsible: { user_id: AUTHOR_ID, origin: 'step' } }));
    expect(screen.getByTestId('sheet-responsible')).toHaveTextContent('Responsable : Camille Bernard');
    expect(screen.queryByLabelText('Responsable de la fiche')).toBeNull();
  });

  it('a person who is no member any more', async () => {
    await renderSheet(sheetViewOf({ responsible: { user_id: 'gone', origin: 'step' } }));
    expect(screen.getByTestId('sheet-responsible')).toHaveTextContent('Responsable : un ancien membre');
  });
});

describe('the two modes (B-4-17 to B-4-20)', () => {
  it('a radio group « Formulaire » and « Écrire librement »; the free text in 14 lines of 20 000 characters; both contents kept', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ purpose: 'Facturer' }) }));
    const radios = within(screen.getByRole('radiogroup', { name: 'Façon de remplir' })).getAllByRole('radio');
    expect(radios.map((radio) => [radio.textContent, radio.getAttribute('aria-checked')])).toEqual([['Formulaire', 'true'], ['Écrire librement', 'false']]);
    fireEvent.click(radios[1] as HTMLElement);
    const free = screen.getByTestId('sheet-free-text');
    expect([free.getAttribute('rows'), free.getAttribute('maxlength')]).toEqual(['14', '20000']);
    fireEvent.change(free, { target: { value: 'Notes libres' } });
    fireEvent.click(radios[0] as HTMLElement);
    expect(screen.getByTestId('sheet-purpose')).toHaveValue('Facturer');
    fireEvent.click(radios[1] as HTMLElement);
    expect(screen.getByTestId('sheet-free-text')).toHaveValue('Notes libres');
  });

  it('opens on the free text when it is the only content', async () => {
    await renderSheet(sheetViewOf({ content: contentOf({ free_text: 'Tout est ici' }) }));
    expect(screen.getByTestId('sheet-mode-free')).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByTestId('sheet-free-text')).toHaveValue('Tout est ici');
  });
});

describe('a sheet that is gone or refused', () => {
  it('says the refusal of the reading', async () => {
    const { record } = mountSheet({ 'GET /api/steps/s2/sheet': { refused: 404, code: 'INTROUVABLE', message: 'Étape introuvable.' } });
    await vi.waitFor(() => {
      expect(record.notes).toEqual([['Étape introuvable.', 'error']]);
    });
    expect(screen.getByTestId('sheet-loading')).toBeInTheDocument();
  });
});
