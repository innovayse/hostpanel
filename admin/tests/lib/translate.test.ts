/**
 * Tests for the store-side translator: the same catalogue the components read, resolved per
 * call so a language switch is honoured, with a raw-value fallback for unknown identifiers.
 */

import { afterEach, describe, expect, it } from 'vitest'
import { setLocale } from '@/i18n'
import { MessageLanguage } from '@/locales/messages'
import { translate, translateCountIn, translateIn, translateOrRaw } from '@/lib/translate'

describe('translate', () => {
  afterEach(() => {
    setLocale(MessageLanguage.En)
  })

  it('resolves a key in the active language', () => {
    expect(translate('errors.noAnswer')).toBe('Could not reach the server. Check your connection and try again.')
  })

  it('follows a language switch made after the module was loaded', () => {
    setLocale(MessageLanguage.Ru)

    expect(translate('errors.noAnswer')).toBe('Не удалось связаться с сервером. Проверьте соединение и повторите попытку.')

    setLocale(MessageLanguage.Hy)

    expect(translate('errors.noAnswer')).toBe('Չհաջողվեց կապվել սերվերի հետ։ Ստուգեք կապը և փորձեք կրկին։')
  })

  it('fills named placeholders', () => {
    expect(translate('quotes.convertedSuccess', { id: 42 })).toBe('Quote converted to invoice #42 successfully.')
  })
})

describe('translateOrRaw', () => {
  afterEach(() => {
    setLocale(MessageLanguage.En)
  })

  it('translates a known identifier', () => {
    setLocale(MessageLanguage.Ru)

    expect(translateOrRaw('ticketStatus.AwaitingReply', 'AwaitingReply')).toBe('Ожидает ответа')
  })

  it('returns the raw value when the catalogue has no such key', () => {
    expect(translateOrRaw('ticketStatus.SomethingNew', 'SomethingNew')).toBe('SomethingNew')
  })
})

describe('translateIn and translateCountIn', () => {
  it('write a message in the given language whatever the active one is', () => {
    expect(translateIn('duration.relativeNow', 'hy')).toBe('հիմա')
    expect(translateIn('duration.relativeNow', 'en')).toBe('now')
  })

  it('fill the named values of the message in the given language', () => {
    expect(translateIn('calendar.shortDay', 'ru', { month: 'мар', day: 7 })).toBe('7 мар')
  })

  it('choose the plural form of the count in the given language', () => {
    expect(translateCountIn('duration.daysAgo', 1, 'en')).toBe('1 day ago')
    expect(translateCountIn('duration.daysAgo', 4, 'en')).toBe('4 days ago')
    expect(translateCountIn('duration.daysAgo', 4, 'ru')).toBe('4 дня назад')
    expect(translateCountIn('duration.daysAgo', 5, 'ru')).toBe('5 дней назад')
    expect(translateCountIn('duration.daysAgo', 4, 'hy')).toBe('4 օր առաջ')
  })
})
