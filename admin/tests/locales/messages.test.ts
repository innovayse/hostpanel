/**
 * Tests for the locale assembler: area files are grouped per language under their file name,
 * and the shipped catalogue has the same areas in every language.
 */
import { describe, expect, it } from 'vitest'
import { buildMessages, messages } from '@/locales/messages'

describe('buildMessages', () => {
  it('groups files by language and area', () => {
    const result = buildMessages({
      './en/nav.json': { home: 'Home' },
      './ru/nav.json': { home: 'Главная' },
      './en/common.json': { save: 'Save' },
    })

    expect(result.en).toEqual({ nav: { home: 'Home' }, common: { save: 'Save' } })
    expect(result.ru).toEqual({ nav: { home: 'Главная' } })
    expect(result.hy).toEqual({})
  })

  it('ignores a folder that is not a shipped language', () => {
    expect(buildMessages({ './de/nav.json': { home: 'Start' } })).toEqual({ en: {}, ru: {}, hy: {}, es: {} })
  })
})

describe('shipped messages', () => {
  it('has the same areas in en, ru and hy', () => {
    const areas = Object.keys(messages.en).sort()

    expect(areas.length).toBeGreaterThan(0)
    expect(Object.keys(messages.ru).sort()).toEqual(areas)
    expect(Object.keys(messages.hy).sort()).toEqual(areas)
  })
})
