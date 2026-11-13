/**
 * Unit tests for UiFilterTrigger: the button and its count badge, the surface it opens (a centred dialog on a
 * desktop, a bottom sheet on a phone) and the Apply, Reset and Cancel paths.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiFilterTrigger from '@/components/ui/UiFilterTrigger.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the trigger with the teleport inlined so the surface is queryable.
 *
 * @param props - Props for the trigger.
 * @returns The wrapper.
 */
const mountTrigger = (props: { count: number; title?: string; disabled?: boolean }) =>
  mount(UiFilterTrigger, {
    props,
    slots: { default: '<p data-testid="fields">Fields</p>' },
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

/**
 * Makes the screen a phone or not.
 *
 * @param phone - Whether the sheet query matches.
 * @returns Nothing; `matchMedia` is stubbed.
 */
const stubPhone = (phone: boolean): void => {
  vi.stubGlobal('matchMedia', () => ({ matches: phone, addEventListener: vi.fn(), removeEventListener: vi.fn() }))
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('UiFilterTrigger', () => {
  it('disables the button while the list loads', () => {
    expect(mountTrigger({ count: 0, disabled: true }).get('[data-testid="filter-trigger"]').attributes('disabled')).toBeDefined()
  })

  it('shows a Filters button with a count badge only while filters are active', () => {
    expect(mountTrigger({ count: 0 }).find('[data-testid="filter-trigger-count"]').exists()).toBe(false)
    const wrapper = mountTrigger({ count: 3 })

    expect(wrapper.get('[data-testid="filter-trigger"]').text()).toContain('Filters')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('keeps the surface closed until the button is pressed, and announces it', async () => {
    const wrapper = mountTrigger({ count: 0 })
    const button = wrapper.get('[data-testid="filter-trigger"]')

    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    expect(button.attributes('aria-expanded')).toBe('false')
    expect(button.attributes('aria-haspopup')).toBe('dialog')

    await button.trigger('click')

    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
    expect(button.attributes('aria-expanded')).toBe('true')
    expect(wrapper.find('[data-testid="fields"]').exists()).toBe(true)
  })

  it('opens a centred dialog on a desktop', async () => {
    stubPhone(false)
    const wrapper = mountTrigger({ count: 0, title: 'Client filters' })

    await wrapper.get('[data-testid="filter-trigger"]').trigger('click')

    expect(wrapper.findComponent(UiDialog).exists()).toBe(true)
    expect(wrapper.findComponent(UiDrawer).exists()).toBe(false)
    expect(wrapper.get('h2').text()).toBe('Client filters')
  })

  it('opens a bottom sheet on a phone', async () => {
    stubPhone(true)
    const wrapper = mountTrigger({ count: 0 })

    await wrapper.get('[data-testid="filter-trigger"]').trigger('click')

    expect(wrapper.findComponent(UiDrawer).props('sheet')).toBe(true)
    expect(wrapper.findComponent(UiDialog).exists()).toBe(false)
    expect(wrapper.get('h2').text()).toBe('Filters')
  })

  it('applies and closes', async () => {
    const wrapper = mountTrigger({ count: 0 })
    await wrapper.get('[data-testid="filter-trigger"]').trigger('click')

    await wrapper.get('[data-testid="filter-apply"]').trigger('click')

    expect(wrapper.emitted('apply')).toHaveLength(1)
    expect(wrapper.emitted('cancel')).toBeUndefined()
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('resets and stays open', async () => {
    const wrapper = mountTrigger({ count: 0 })
    await wrapper.get('[data-testid="filter-trigger"]').trigger('click')

    await wrapper.get('[data-testid="filter-reset"]').trigger('click')

    expect(wrapper.emitted('reset')).toHaveLength(1)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
  })

  it('cancels from the Cancel button, Escape and the close button', async () => {
    const wrapper = mountTrigger({ count: 0 })
    /**
     * Presses the Filters button.
     *
     * @returns The trigger's promise.
     */
    const open = () => wrapper.get('[data-testid="filter-trigger"]').trigger('click')

    await open()
    await wrapper.get('[data-testid="filter-cancel"]').trigger('click')
    await open()
    await wrapper.find('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await open()
    await wrapper.get('[data-testid="dialog-close"]').trigger('click')

    expect(wrapper.emitted('cancel')).toHaveLength(3)
    expect(wrapper.emitted('apply')).toBeUndefined()
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('puts the attributes a caller passes onto the button', () => {
    const wrapper = mount(UiFilterTrigger, {
      props: { count: 0 },
      attrs: { 'data-testid': 'clients-filters' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })

    expect(wrapper.find('[data-testid="clients-filters"]').exists()).toBe(true)
  })
})
