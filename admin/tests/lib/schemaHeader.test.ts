/**
 * Tests of the schema header reader: the title, the subtitle and the back link caption of a screen schema, and the
 * title or subtitle a screen overrides at run time.
 */
import { describe, expect, it } from 'vitest'
import { schemaHeader } from '@/lib/schemaHeader'

/**
 * Translates a key the way the tests read it: the key itself, upper-cased, so a caption is told from its key.
 *
 * @param key - The locale key.
 * @returns The translated text.
 */
const t = (key: string): string => key.toUpperCase()

describe('schemaHeader', () => {
  it('translates the title, the subtitle and the back link caption of the schema', () => {
    const header = schemaHeader({ header: { title: 'a.title', subtitle: 'a.subtitle', backLabel: 'a.back', backMobileOnly: true } }, t)

    expect(header).toEqual({ title: 'A.TITLE', subtitle: 'A.SUBTITLE', backLabel: 'A.BACK', backMobileOnly: true })
  })

  it('leaves out what the schema does not name', () => {
    expect(schemaHeader({ header: { actions: 1 } }, t)).toEqual({ title: undefined, subtitle: undefined, backLabel: undefined, backMobileOnly: undefined })
    expect(schemaHeader({}, t).title).toBeUndefined()
  })

  it('lets the screen override the title and keep the schema subtitle', () => {
    const header = schemaHeader({ header: { title: 'a.title', subtitle: 'a.subtitle' } }, t, { title: 'Runtime' })

    expect(header.title).toBe('Runtime')
    expect(header.subtitle).toBe('A.SUBTITLE')
  })

  it('keeps the schema text when an override is undefined', () => {
    expect(schemaHeader({ header: { title: 'a.title' } }, t, { title: undefined }).title).toBe('A.TITLE')
  })
})
