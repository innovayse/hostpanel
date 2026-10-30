/**
 * Client-side checks of the profile form and of the sign-in address change.
 *
 * They run before the save is sent, so a blank name or a malformed address is pointed out on its
 * field without a round trip. The server stays the authority: each check only decides whether a
 * typed value is acceptable and returns the i18n key of the reason, never a sentence.
 *
 * @module utils/profileValidation
 */

import type { EmailChangeInput } from '~/types/emailchangeinput'

/** Something before the `@`, a domain after it with at least one dot, and no whitespace anywhere. */
const EMAIL_FORMAT = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/**
 * Tells whether a typed value is empty once surrounding whitespace is dropped.
 *
 * @param value - The value as typed; absent counts as blank.
 * @returns True when nothing visible was typed.
 */
export const isBlank = (value: string | null | undefined): boolean => (value ?? '').trim() === ''

/**
 * Tells whether a typed value has the shape of an email address.
 *
 * @param value - The value as typed.
 * @returns True when the trimmed value looks like `name@domain.tld`.
 */
export const isEmailAddress = (value: string): boolean => EMAIL_FORMAT.test(value.trim())

/**
 * Checks a sign-in address change: the shape of the new address and that a password was typed.
 *
 * @param input - The change about to be asked for.
 * @returns Field name (as on the wire) to the i18n key of its reason; empty when it may be sent.
 */
export const validateEmailChange = (input: EmailChangeInput): Record<string, string> => {
  const errors: Record<string, string> = {}
  if (isBlank(input.newEmail)) {
    errors.newEmail = 'client.profile.validation.emailRequired'
  } else if (!isEmailAddress(input.newEmail)) {
    errors.newEmail = 'client.profile.validation.emailInvalid'
  }
  if (isBlank(input.currentPassword)) {
    errors.currentPassword = 'client.emailChange.passwordRequired'
  }
  return errors
}
