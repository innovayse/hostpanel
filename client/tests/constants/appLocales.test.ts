/**
 * Unit tests for the locale list: it follows the enum, in order, and the default is English.
 *
 * @module tests/constants/appLocales.test
 */

import { describe, expect, it } from 'vitest'
import { APP_LOCALES, DEFAULT_APP_LOCALE } from '~/constants/appLocales'
import { AppLocale } from '~/types/applocale'

describe('APP_LOCALES', () => {
  it('lists every AppLocale member in declaration order', () => {
    expect([...APP_LOCALES]).toEqual([AppLocale.En, AppLocale.Ru, AppLocale.Hy, AppLocale.De])
  })

  it('defaults to English, the language served without a prefix', () => {
    expect(DEFAULT_APP_LOCALE).toBe(AppLocale.En)
    expect(APP_LOCALES).toContain(DEFAULT_APP_LOCALE)
  })
})
