/**
 * Tests for the locale tab state: one tab per locale with the given test ids, and a selection
 * that ignores a value that is not a locale.
 */

import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { useStorefrontLocaleTabs } from '@/composables/useStorefrontLocaleTabs'
import { ContentLanguage } from '@/locales/contentLanguage'
import { mountWithApp } from '../helpers/mount'

/**
 * Mounts a host component and returns the tab state it created.
 *
 * @returns The tab state.
 */
const makeTabs = (): ReturnType<typeof useStorefrontLocaleTabs> => {
  let tabs: ReturnType<typeof useStorefrontLocaleTabs> | undefined
  mountWithApp(defineComponent({
    setup: () => {
      tabs = useStorefrontLocaleTabs('demo-tab')
      return () => h('div')
    },
  }))
  if (!tabs) {
    throw new Error('The host did not run setup.')
  }
  return tabs
}

describe('useStorefrontLocaleTabs', () => {
  it('lists en, ru, hy, de and es with prefixed test ids', () => {
    const { tabs } = makeTabs()

    expect(tabs.value.map((tab) => [tab.value, tab.testid])).toEqual([
      ['en', 'demo-tab-en'],
      ['ru', 'demo-tab-ru'],
      ['hy', 'demo-tab-hy'],
      ['de', 'demo-tab-de'],
      ['es', 'demo-tab-es'],
    ])
  })

  it('starts on English and switches to a known locale', () => {
    const state = makeTabs()
    expect(state.activeLocale.value).toBe(ContentLanguage.En)

    state.select('hy')

    expect(state.activeLocale.value).toBe(ContentLanguage.Hy)

    state.select('de')

    expect(state.activeLocale.value).toBe(ContentLanguage.De)

    state.select('es')

    expect(state.activeLocale.value).toBe(ContentLanguage.Es)
  })

  it('ignores a value that is not a locale', () => {
    const state = makeTabs()

    state.select('fr')

    expect(state.activeLocale.value).toBe(ContentLanguage.En)
  })
})
