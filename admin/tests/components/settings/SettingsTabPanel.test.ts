/**
 * Tests for SettingsTabPanel: the slot is rendered, the bar counts this panel's unsaved drafts and saves or
 * discards them, and the bar is left out while loading.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { inject } from 'vue'
import { mount } from '@vue/test-utils'
import SettingsTabPanel from '@/components/settings/SettingsTabPanel.vue'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import type { Setting, SettingSave } from '@/types/setting'
import { createTestI18n } from '../../helpers/i18n'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'

/**
 * Mounts the panel around one control with a draft.
 *
 * @param options - The saver and the loading flag.
 * @returns The wrapper, the control's draft and the saver.
 */
const mountPanel = (options: { save?: SettingSave; loading?: boolean } = {}) => {
  /** The stored rows the control edits. */
  const settings = ref<Setting[]>([{ id: 1, key: 'a', value: 'one' }])
  /** What the control's setup hands back to the test. */
  const captured: { draft?: SettingDraft } = {}
  /** The card stand-in that owns one draft. */
  const Control = defineComponent({
    /**
     * Creates the draft below the panel.
     *
     * @returns The render function of the slot content.
     */
    setup: () => {
      captured.draft = useSettingDraft(settings, 'a')
      return () => h('p', { 'data-testid': 'slot-content' }, 'card')
    },
  })
  /** The saver the bar runs; answers success unless the test brings its own. */
  const save = options.save ?? vi.fn(async () => ({ saved: true, error: null }))
  /** The mounted panel. */
  const wrapper = mount(SettingsTabPanel, {
    props: { save, loading: options.loading },
    slots: { default: () => h(Control) },
    global: { plugins: [createTestI18n()] },
  })
  return { wrapper, draft: captured.draft as SettingDraft, save }
}

describe('SettingsTabPanel', () => {
  it('renders the slot and an idle bar', () => {
    const { wrapper } = mountPanel()

    expect(wrapper.find('[data-testid="slot-content"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="settings-form-save"]').attributes('disabled')).toBeDefined()
  })

  it('counts the unsaved draft of its own tab', async () => {
    const { wrapper, draft } = mountPanel()

    draft.value.value = 'two'
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="settings-form-save"]').attributes('disabled')).toBeUndefined()
    expect(wrapper.get('[data-testid="settings-form-status"]').text()).toContain('1')
  })

  it('saves every unsaved draft through the given saver', async () => {
    const { wrapper, draft, save } = mountPanel()

    draft.value.value = 'two'
    await wrapper.vm.$nextTick()
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    expect(save).toHaveBeenCalledWith(1, 'two')
  })

  it('discards the unsaved drafts', async () => {
    const { wrapper, draft } = mountPanel()

    draft.value.value = 'two'
    await wrapper.vm.$nextTick()
    await wrapper.get('[data-testid="settings-form-cancel"]').trigger('click')

    expect(draft.value.value).toBe('one')
  })

  it('leaves the bar out while loading', () => {
    const { wrapper } = mountPanel({ loading: true })

    expect(wrapper.find('[data-testid="settings-form-save"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="slot-content"]').exists()).toBe(true)
  })

  it('has every field below it draw its placeholder while it loads', () => {
    /** A control that reads the flag the panel provides. */
    const Probe = defineComponent({
      /**
       * Reads the loading flag the panel provides.
       *
       * @returns The render function of the probe.
       */
      setup: () => {
        const loading = inject(UI_FIELD_LOADING, ref(false))
        return () => h('p', { 'data-testid': 'probe' }, String(loading.value))
      },
    })
    const wrapper = mount(SettingsTabPanel, {
      props: { save: vi.fn(async () => ({ saved: true, error: null })), loading: true },
      slots: { default: () => h(Probe) },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.get('[data-testid="probe"]').text()).toBe('true')
  })
})
