/**
 * Tests for useLocalizedSettingDrafts: one draft per language of the shared list, the row names, the empty
 * marks and the refusal sentences the language tabs read.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { localizedSettingEnv, localizedSettingKey, useLocalizedSettingDrafts } from '@/composables/useLocalizedSettingDrafts'
import type { LocalizedSettingDrafts } from '@/composables/useLocalizedSettingDrafts'
import { STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import { ContentLanguage } from '@/locales/contentLanguage'
import { SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'
import { mountWithApp } from '../helpers/mount'

/**
 * Mounts a host that runs the composable over the given rows, for the contact address.
 *
 * @param rows - The settings rows the drafts read.
 * @returns What the composable returned.
 */
const makeDrafts = (rows: Setting[]): LocalizedSettingDrafts => {
  let drafts: LocalizedSettingDrafts | undefined
  mountWithApp(defineComponent({
    setup: () => {
      drafts = useLocalizedSettingDrafts(ref(rows), SettingKey.ContactAddress)
      return () => h('div')
    },
  }))
  if (!drafts) {
    throw new Error('The host did not run setup.')
  }
  return drafts
}

describe('localizedSettingKey and localizedSettingEnv', () => {
  it('keep the plain name for English and add the language for every other one', () => {
    expect(localizedSettingKey('portal.contact.address', ContentLanguage.En)).toBe('portal.contact.address')
    expect(localizedSettingKey('portal.contact.address', ContentLanguage.De)).toBe('portal.contact.address.de')
    expect(localizedSettingEnv('NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS', ContentLanguage.En)).toBe('NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS')
    expect(localizedSettingEnv('NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS', ContentLanguage.Es)).toBe('NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS_ES')
  })
})

describe('useLocalizedSettingDrafts', () => {
  it('holds a draft, a row key and a variable for every language of the shared list', () => {
    const { drafts, keys, envs } = makeDrafts([])

    expect(Object.keys(drafts)).toEqual([...STOREFRONT_LOCALES])
    expect(keys).toEqual({
      en: 'portal.contact.address',
      ru: 'portal.contact.address.ru',
      hy: 'portal.contact.address.hy',
      de: 'portal.contact.address.de',
      es: 'portal.contact.address.es',
      fr: 'portal.contact.address.fr',
    })
    expect(envs.de).toBe('NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS_DE')
  })

  it('reads the row of each language and counts a blank or unseeded row as empty', () => {
    const { drafts, filled } = makeDrafts([
      { id: 1, key: 'portal.contact.address', value: '1 Main St' },
      { id: 2, key: 'portal.contact.address.ru', value: '   ' },
      { id: 3, key: 'portal.contact.address.de', value: 'Hauptstrasse 1' },
    ])

    expect(drafts.en.value.value).toBe('1 Main St')
    expect(drafts.de.value.value).toBe('Hauptstrasse 1')
    expect(filled.value).toEqual({ en: true, ru: false, hy: false, de: true, es: false, fr: false })
  })

  it('follows an edit in the marks', () => {
    const { drafts, filled } = makeDrafts([{ id: 3, key: 'portal.contact.address.es', value: '' }])

    drafts.es.value.value = 'Calle Mayor 1'

    expect(filled.value.es).toBe(true)
  })

  it('reports only the languages whose last save was refused', async () => {
    const { drafts, errors } = makeDrafts([
      { id: 1, key: 'portal.contact.address', value: 'a' },
      { id: 2, key: 'portal.contact.address.de', value: 'b' },
    ])
    drafts.en.value.value = 'a2'
    drafts.de.value.value = 'b2'

    await drafts.en.submit(async () => ({ saved: true, error: null }))
    await drafts.de.submit(async () => ({ saved: false, error: 'Too long.' }))

    expect(errors.value).toEqual({ de: 'Too long.' })
  })

  it('falls back to the generic sentence when the refusal has none', async () => {
    const { drafts, errors } = makeDrafts([{ id: 1, key: 'portal.contact.address', value: 'a' }])
    drafts.en.value.value = 'a2'

    await drafts.en.submit(async () => ({ saved: false, error: null }))

    expect(errors.value.en).toBeTruthy()
  })
})
