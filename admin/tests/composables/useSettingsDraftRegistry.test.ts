/**
 * Tests for the settings draft registry: drafts below a provider register, the registry counts the
 * dirty ones, saves them together and drops their edits; a draft outside a provider is unaffected.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import { provideSettingsDraftRegistry } from '@/composables/useSettingsDraftRegistry'
import type { SettingsDraftRegistry } from '@/composables/useSettingsDraftRegistry'
import type { Setting, SettingSave } from '@/types/setting'

/** What {@link mountPage} hands back. */
interface MountedPage {
  /** The registry the page provides. */
  registry: SettingsDraftRegistry
  /** Draft of the first row. */
  a: SettingDraft
  /** Draft of the second row. */
  b: SettingDraft
  /** Unmounts the page, as leaving the route does. */
  unmount: () => void
}

/**
 * Two stored rows the drafts below edit.
 *
 * @returns Fresh rows.
 */
const rows = (): Setting[] => [
  { id: 1, key: 'a', value: 'one' },
  { id: 2, key: 'b', value: 'two' },
]

/**
 * Mounts a provider with a child that creates two drafts.
 *
 * @returns The registry, both drafts and an unmount.
 */
const mountPage = (): MountedPage => {
  const settings = ref(rows())
  const captured: { registry?: SettingsDraftRegistry; a?: SettingDraft; b?: SettingDraft } = {}
  const Child = defineComponent({
    /**
     * Creates the two drafts below the provider.
     *
     * @returns The render function of an empty element.
     */
    setup: () => {
      captured.a = useSettingDraft(settings, 'a')
      captured.b = useSettingDraft(settings, 'b')
      return () => h('div')
    },
  })
  const Page = defineComponent({
    /**
     * Provides the registry and renders the child.
     *
     * @returns The render function of the child.
     */
    setup: () => {
      captured.registry = provideSettingsDraftRegistry()
      return () => h(Child)
    },
  })
  const wrapper = mount(Page)
  // The setup functions above ran during mount, so the three are set here.
  return { registry: captured.registry as SettingsDraftRegistry, a: captured.a as SettingDraft, b: captured.b as SettingDraft, unmount: () => wrapper.unmount() }
}

describe('settings draft registry', () => {
  it('counts the drafts that have an edit', () => {
    const { registry, a, b } = mountPage()
    expect(registry.dirtyCount.value).toBe(0)

    a.value.value = 'changed'
    expect(registry.dirtyCount.value).toBe(1)

    b.value.value = 'changed'
    expect(registry.dirtyCount.value).toBe(2)
  })

  it('saves every dirty draft and skips the clean ones', async () => {
    const { registry, a } = mountPage()
    const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))
    a.value.value = 'changed'

    await registry.saveAll(save)

    expect(save).toHaveBeenCalledTimes(1)
    expect(save).toHaveBeenCalledWith(1, 'changed')
  })

  it('drops every edit on discard', () => {
    const { registry, a, b } = mountPage()
    a.value.value = 'x'
    b.value.value = 'y'

    registry.discardAll()

    expect(registry.dirtyCount.value).toBe(0)
    expect(a.value.value).toBe('one')
    expect(b.value.value).toBe('two')
  })

  it('forgets a draft whose component is gone', () => {
    const { registry, a, unmount } = mountPage()
    a.value.value = 'x'
    expect(registry.dirtyCount.value).toBe(1)

    unmount()

    expect(registry.dirtyCount.value).toBe(0)
  })

  it('leaves a draft outside any provider working on its own', () => {
    const draft = useSettingDraft(ref(rows()), 'a')

    draft.value.value = 'x'

    expect(draft.dirty.value).toBe(true)
  })
})
