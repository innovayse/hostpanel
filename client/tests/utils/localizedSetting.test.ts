/**
 * Tests for `utils/localizedSetting.ts`: the one place that names the row of a setting kept per language.
 *
 * @module tests/utils/localizedSetting.test
 */

import { describe, expect, it } from 'vitest'
import { APP_LOCALES } from '~/constants/appLocales'
import { localizedRuntimeConfig, localizedSettingEnvKey, localizedSettingEnvVar, localizedSettingKey, localizedSettingKeys } from '~/utils/localizedSetting'

describe('localizedSettingKey', () => {
  it('keeps the plain key for the default language', () => {
    expect(localizedSettingKey('portal.contact.address', 'en')).toBe('portal.contact.address')
  })

  it.each(['ru', 'hy', 'de', 'es', 'fr'])('adds the language code to name the %s row', (locale) => {
    expect(localizedSettingKey('portal.contact.address', locale)).toBe(`portal.contact.address.${locale}`)
  })
})

describe('localizedSettingEnvKey', () => {
  it('keeps the plain runtime-config key for the default language and capitalises the code for the others', () => {
    expect(localizedSettingEnvKey('portalAboutText', 'en')).toBe('portalAboutText')
    expect(localizedSettingEnvKey('portalAboutText', 'ru')).toBe('portalAboutTextRu')
    expect(localizedSettingEnvKey('portalAboutText', 'de')).toBe('portalAboutTextDe')
  })
})

describe('localizedSettingKeys', () => {
  it('lists one row per portal language, the default first, so a new language is covered without an edit', () => {
    const keys = localizedSettingKeys('portal.about.text')

    expect(keys).toHaveLength(APP_LOCALES.length)
    expect(keys[0]).toBe('portal.about.text')
    expect(keys).toContain('portal.about.text.de')
    expect(keys).toContain('portal.about.text.fr')
  })
})

describe('localizedSettingEnvVar and localizedRuntimeConfig', () => {
  it('names the variable of a language by appending its upper-case code', () => {
    expect(localizedSettingEnvVar('NUXT_PUBLIC_PORTAL_ABOUT_TEXT', 'de')).toBe('NUXT_PUBLIC_PORTAL_ABOUT_TEXT_DE')
  })

  it('reads one entry per language but the default, empty when the variable is unset', () => {
    const config = localizedRuntimeConfig('portalAboutText', 'NUXT_PUBLIC_PORTAL_ABOUT_TEXT', { NUXT_PUBLIC_PORTAL_ABOUT_TEXT_FR: 'Histoire' })

    expect(Object.keys(config)).toEqual(APP_LOCALES.filter(locale => locale !== 'en').map(locale => localizedSettingEnvKey('portalAboutText', locale)))
    expect(config.portalAboutTextFr).toBe('Histoire')
    expect(config.portalAboutTextDe).toBe('')
  })
})
