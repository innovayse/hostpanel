/**
 * Mount tests for UiRowActions: trigger opens the menu, items are rendered, an action fires and
 * closes the menu, danger items are styled red, Escape closes without firing, and an outside click
 * closes the menu.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import { createTestI18n } from '../../helpers/i18n'

const actions = [
  { label: 'Edit', testid: 'action-edit', onSelect: vi.fn() },
  { label: 'Delete', testid: 'action-delete', onSelect: vi.fn(), danger: true },
]

afterEach(() => {
  vi.restoreAllMocks()
})

/** Mounts the component with the standard action list. */
const mountIt = () =>
  mount(UiRowActions, {
    props: { actions },
    global: { plugins: [createTestI18n()] },
    attachTo: document.body,
  })

describe('UiRowActions', () => {
  it('hides the menu by default', () => {
    const wrapper = mountIt()
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('opens the menu when the trigger is clicked', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('renders all action labels', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    const text = wrapper.get('[data-testid="row-actions-menu"]').text()
    expect(text).toContain('Edit')
    expect(text).toContain('Delete')
    wrapper.unmount()
  })

  it('calls onSelect and closes the menu when an action is clicked', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="action-edit"]').trigger('click')
    expect(actions[0].onSelect).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('applies danger styling to destructive actions', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    const deleteBtn = wrapper.get('[data-testid="action-delete"]')
    expect(deleteBtn.classes().some((c) => c.includes('danger'))).toBe(true)
    wrapper.unmount()
  })

  it('closes the menu on Escape', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('closes the menu on outside click', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await document.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })
})
