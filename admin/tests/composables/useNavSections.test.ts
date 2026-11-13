/**
 * Tests for useNavSections: the panel's own sections in their configured order, the Add-ons section
 * only while a plugin declares a menu item, and no section left empty.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useNavSections } from '@/composables/useNavSections'
import { NavSectionId } from '@/lib/navigation'
import { usePluginUiStore } from '@/stores/pluginUi'

describe('useNavSections', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('lists the seven built-in sections while no plugin declares a menu item', () => {
    const sections = useNavSections()

    expect(sections.value.map((section) => section.id)).toEqual([
      NavSectionId.Home,
      NavSectionId.Sales,
      NavSectionId.Support,
      NavSectionId.Catalog,
      NavSectionId.Infrastructure,
      NavSectionId.Insights,
      NavSectionId.System,
    ])
  })

  it('adds the Add-ons section before System once a plugin declares a menu item, and follows the plugin menu', () => {
    const sections = useNavSections()

    usePluginUiStore().menu = [{ key: 'sites:main', label: 'Sites', icon: null, to: '/apps/sites' }]

    expect(sections.value.map((section) => section.id)).toEqual([
      NavSectionId.Home,
      NavSectionId.Sales,
      NavSectionId.Support,
      NavSectionId.Catalog,
      NavSectionId.Infrastructure,
      NavSectionId.Insights,
      NavSectionId.Addons,
      NavSectionId.System,
    ])

    usePluginUiStore().menu = []

    expect(sections.value.some((section) => section.id === NavSectionId.Addons)).toBe(false)
  })
})
