/**
 * Tests for the one error reader: sentence (`detail`, `title`, the single "no answer" line),
 * code (`type`) and per-field messages (`errors`).
 */

import { afterEach, describe, expect, it } from 'vitest'
import { ApiError } from '@/composables/useApi'
import { setLocale } from '@/i18n'
import { MessageLanguage } from '@/locales/messages'
import { errorCode, errorSentence, errorStatus, fieldErrors } from '@/lib/errorHandler'

/** A representative `ErrorResponse` body. */
const problem = {
  type: 'VALIDATION_ERROR',
  title: 'Validation Failed',
  status: 400,
  detail: 'One or more validation errors occurred.',
  traceId: 'abc',
  errors: { email: ['Email is required.', 'Second.'], Price: ['Price must be greater than 0.'] },
}

afterEach(() => {
  setLocale(MessageLanguage.En)
})

describe('errorSentence', () => {
  it('prefers detail over title', () => {
    expect(errorSentence(new ApiError(400, problem))).toBe('One or more validation errors occurred.')
  })

  it('falls back to title when detail is empty', () => {
    expect(errorSentence(new ApiError(400, { ...problem, detail: '' }))).toBe('Validation Failed')
  })

  it('ignores the legacy error member', () => {
    expect(errorSentence(new ApiError(400, { error: 'old sentence' }))).toBe(
      'Could not reach the server. Check your connection and try again.',
    )
  })

  it('answers with the no-answer sentence when the response had no body', () => {
    expect(errorSentence(new ApiError(502, null))).toBe('Could not reach the server. Check your connection and try again.')
  })

  it('answers with the no-answer sentence when the request never arrived', () => {
    expect(errorSentence(new TypeError('Failed to fetch'))).toBe(
      'Could not reach the server. Check your connection and try again.',
    )
  })

  it('answers the no-answer sentence in the active language', () => {
    setLocale(MessageLanguage.Ru)

    expect(errorSentence(new TypeError('offline'))).toBe(
      'Не удалось связаться с сервером. Проверьте соединение и повторите попытку.',
    )
  })
})

describe('errorCode', () => {
  it('reads type', () => {
    expect(errorCode(new ApiError(400, problem))).toBe('VALIDATION_ERROR')
  })

  it('ignores the legacy code member', () => {
    expect(errorCode(new ApiError(400, { code: 'OLD' }))).toBeNull()
  })

  it('is null when no answer arrived', () => {
    expect(errorCode(new TypeError('offline'))).toBeNull()
  })
})

describe('errorStatus', () => {
  it('reads the HTTP status of an API refusal', () => {
    expect(errorStatus(new ApiError(404, null))).toBe(404)
  })

  it('is null when the request never got an answer', () => {
    expect(errorStatus(new TypeError('offline'))).toBeNull()
  })
})

describe('fieldErrors', () => {
  it('maps each field to its first message with a camelCase key', () => {
    expect(fieldErrors(new ApiError(400, problem))).toEqual({
      email: 'Email is required.',
      price: 'Price must be greater than 0.',
    })
  })

  it('keys a field of a nested object by its dotted camelCase path', () => {
    const nested = { type: 'VALIDATION_ERROR', errors: { 'Endpoint.Hostname': ['Hostname is required.'] } }

    expect(fieldErrors(new ApiError(400, nested))).toEqual({ 'endpoint.hostname': 'Hostname is required.' })
  })

  it('is empty when the response named no fields or never arrived', () => {
    expect(fieldErrors(new ApiError(400, { type: 'X', detail: 'No.' }))).toEqual({})
    expect(fieldErrors(new ApiError(500, null))).toEqual({})
    expect(fieldErrors(new TypeError('offline'))).toEqual({})
  })
})

describe('errorSentence for demo mode', () => {
  it('shows the backend detail of a DEMO_ACTION_DISABLED refusal', () => {
    const cause = new ApiError(403, { type: 'DEMO_ACTION_DISABLED', title: 'Forbidden', detail: 'Blocked.' })

    expect(errorSentence(cause)).toBe('Blocked.')
  })
})
