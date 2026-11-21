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
    expect(result.de).toEqual({})
  })

  it('groups the German files like any other language', () => {
    expect(buildMessages({ './de/nav.json': { home: 'Start' } }).de).toEqual({ nav: { home: 'Start' } })
  })

  it('ignores a folder that is not a shipped language', () => {
    expect(buildMessages({ './fr/nav.json': { home: 'Accueil' } })).toEqual({ en: {}, ru: {}, hy: {}, de: {} })
  })
})

describe('shipped messages', () => {
  it('has the same areas in en, ru, hy and de', () => {
    const areas = Object.keys(messages.en).sort()

    expect(areas.length).toBeGreaterThan(0)
    expect(Object.keys(messages.ru).sort()).toEqual(areas)
    expect(Object.keys(messages.hy).sort()).toEqual(areas)
    expect(Object.keys(messages.de).sort()).toEqual(areas)
  })
})
