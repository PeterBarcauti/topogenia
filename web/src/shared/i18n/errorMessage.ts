// errorMessage.ts
// The text of an error for the person: the message of the API in the language of the interface, else the text of its code (annex B 0).

import { isApiError } from '../../api/errors.ts';
import { i18next } from './i18n.ts';

/**
 * The message an error shows: the one the API wrote (it is already in the language of the interface, B-0-17), else the
 * text of its code when the interface has one (no answer, a reply that is not JSON, a refused sign-in), else the text
 * of an unexpected error.
 */
export function messageOf(error: unknown): string {
  if (isApiError(error)) {
    if (error.message !== '') return error.message;
    const key = `errors.${error.code}`;
    if (i18next.exists(key)) return i18next.t(key);
  }
  return i18next.t('errors.UNKNOWN');
}
