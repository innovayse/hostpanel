/**
 * Tests for the language both transport hops stamp and read (`utils/transportLanguage.ts`).
 *
 * @module tests/utils/transportLanguage.test
 */

import { describe, expect, it } from 'vitest'
import { DEFAULT_LANGUAGE, LANGUAGE_HEADER, SUPPORTED_LANGUAGES, resolveLanguage } from '~/utils/transportLanguage'

describe('resolveLanguage', () => {
  it.each(['en', 'ru', 'hy', 'es'])('passes the supported language %s through', (language) => {
    expect(resolveLanguage(language)).toBe(language)
  })

  it('trims and lower-cases before judging, as the backend does', () => {
    expect(resolveLanguage(' RU ')).toBe('ru')
  })

  it.each([undefined, null, '', 'fr', 'ru-RU', 'en,ru'])('falls back to the default for %s', (raw) => {
    expect(resolveLanguage(raw)).toBe(DEFAULT_LANGUAGE)
  })

  it('names the header the backend reads and the languages it ships', () => {
    expect(LANGUAGE_HEADER).toBe('X-Language')
    expect([...SUPPORTED_LANGUAGES]).toEqual(['en', 'ru', 'hy', 'es'])
  })
})
