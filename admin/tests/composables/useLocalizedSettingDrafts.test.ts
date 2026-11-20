/**
 * Tests for useLocalizedSettingDrafts: one draft per language row, the empty marks and the refusal
 * sentences the language tabs read.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { useLocalizedSettingDrafts } from '@/composables/useLocalizedSettingDrafts'
import type { LocalizedSettingDrafts } from '@/composables/useLocalizedSettingDrafts'
import { MessageLanguage } from '@/locales/messages'
import type { Setting } from '@/types/setting'
import { mountWithApp } from '../helpers/mount'

/** The row of each language. */
const keys = {
  [MessageLanguage.En]: 'portal.contact.address',
  [MessageLanguage.Ru]: 'portal.contact.address.ru',
  [MessageLanguage.Hy]: 'portal.contact.address.hy',
  [MessageLanguage.Es]: 'portal.contact.address.es',
}

/**
 * Mounts a host that runs the composable over the given rows.
 *
 * @param rows - The settings rows the drafts read.
 * @returns What the composable returned.
 */
const makeDrafts = (rows: Setting[]): LocalizedSettingDrafts => {
  let drafts: LocalizedSettingDrafts | undefined
  mountWithApp(defineComponent({
    setup: () => {
      drafts = useLocalizedSettingDrafts(ref(rows), keys)
      return () => h('div')
    },
  }))
  if (!drafts) {
    throw new Error('The host did not run setup.')
  }
  return drafts
}

describe('useLocalizedSettingDrafts', () => {
  it('reads the row of each language and counts a blank or unseeded row as empty', () => {
    const { drafts, filled } = makeDrafts([
      { id: 1, key: keys.en, value: '1 Main St' },
      { id: 2, key: keys.ru, value: '   ' },
    ])

    expect(drafts.en.value.value).toBe('1 Main St')
    expect(filled.value).toEqual({ en: true, ru: false, hy: false, es: false })
  })

  it('follows an edit in the marks', () => {
    const { drafts, filled } = makeDrafts([{ id: 3, key: keys.hy, value: '' }])

    drafts.hy.value.value = 'Հասցե'

    expect(filled.value.hy).toBe(true)
  })

  it('reports only the languages whose last save was refused', async () => {
    const { drafts, errors } = makeDrafts([
      { id: 1, key: keys.en, value: 'a' },
      { id: 2, key: keys.ru, value: 'b' },
    ])
    drafts.en.value.value = 'a2'
    drafts.ru.value.value = 'b2'

    await drafts.en.submit(async () => ({ saved: true, error: null }))
    await drafts.ru.submit(async () => ({ saved: false, error: 'Too long.' }))

    expect(errors.value).toEqual({ ru: 'Too long.' })
  })

  it('falls back to the generic sentence when the refusal has none', async () => {
    const { drafts, errors } = makeDrafts([{ id: 1, key: keys.en, value: 'a' }])
    drafts.en.value.value = 'a2'

    await drafts.en.submit(async () => ({ saved: false, error: null }))

    expect(errors.value.en).toBeTruthy()
  })
})
