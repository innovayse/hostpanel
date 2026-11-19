/**
 * Tests for the translation helpers: blank forms per locale, copying stored text in, collecting
 * only the filled locales out, and picking the text a list shows.
 */

import { describe, expect, it } from 'vitest'
import { blankTranslations, collectTranslations, displayTranslation, formsFromTranslations } from '@/lib/storefrontTranslations'

/** A one-field translation as the tests use it. */
interface Text {
  /** The text. */
  text: string
}

/**
 * Makes a blank text.
 *
 * @returns An empty text.
 */
const blank = (): Text => ({ text: '' })

describe('storefrontTranslations', () => {
  it('blankTranslations makes an independent form per locale', () => {
    const forms = blankTranslations(blank)

    forms.en.text = 'x'

    expect(Object.keys(forms)).toEqual(['en', 'ru', 'hy', 'de', 'es'])
    expect(forms.ru.text).toBe('')
  })

  it('formsFromTranslations copies stored text and leaves missing locales blank', () => {
    const stored = { ru: { text: 'Hello' } }
    const forms = formsFromTranslations(stored, blank)

    expect(forms.ru.text).toBe('Hello')
    expect(forms.en.text).toBe('')
    forms.ru.text = 'changed'
    expect(stored.ru.text).toBe('Hello')
  })

  it('collectTranslations keeps only the filled locales, trimmed', () => {
    const forms = blankTranslations(blank)
    forms.en.text = '  Hello '
    forms.hy.text = '   '

    const collected = collectTranslations(forms, (form) => form.text.trim() !== '', (form) => ({ text: form.text.trim() }))

    expect(collected).toEqual({ en: { text: 'Hello' } })
  })

  it('displayTranslation prefers English, then the first locale with text', () => {
    expect(displayTranslation({ en: { text: 'a' }, ru: { text: 'b' } })).toEqual({ text: 'a' })
    expect(displayTranslation({ hy: { text: 'c' } })).toEqual({ text: 'c' })
    expect(displayTranslation({})).toBeUndefined()
  })
})
