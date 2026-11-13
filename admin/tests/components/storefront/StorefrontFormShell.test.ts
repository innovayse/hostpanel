/**
 * Tests for StorefrontFormShell: the breadcrumb, the slot, the loading and error states, the
 * Cancel / Save events and the question asked when leaving with unsaved edits.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import UiField from '@/components/ui/UiField.vue'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import { mountScreen } from '../../helpers/mount'

/** Props every mount starts from. */
const baseProps = {
  breadcrumb: [{ label: 'Widgets', to: '/widgets' }, { label: 'Create widget' }],
  testid: 'widget',
  saveLabel: 'Create widget',
  loading: false,
  saving: false,
  dirty: false,
  error: null,
}

/**
 * Builds a host that renders the shell with a body in its slot.
 *
 * @param overrides - Props to change.
 * @returns The host component.
 */
const hostWith = (overrides: Record<string, unknown> = {}) =>
  defineComponent({
    setup: () => () => h(StorefrontFormShell, { ...baseProps, ...overrides }, { default: () => h('p', { 'data-testid': 'body' }, 'Fields') }),
  })

describe('StorefrontFormShell', () => {
  it('draws the trail, the body and the save button label', async () => {
    const { wrapper } = await mountScreen(hostWith())

    expect(wrapper.get('[data-testid="settings-breadcrumb"]').text()).toContain('Create widget')
    expect(wrapper.get('[data-testid="body"]').text()).toBe('Fields')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create widget')
  })

  it('keeps the body and the bar while loading, with the fields as skeletons and Save disabled', async () => {
    const { wrapper } = await mountScreen(hostWith({ loading: true }))

    expect(wrapper.find('[data-testid="body"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="settings-form-save"]').attributes('disabled')).toBeDefined()
  })

  it('provides the loading flag so a field below draws a skeleton', async () => {
    const field = defineComponent({ setup: () => () => h(UiField, { label: 'Name' }, { default: () => h('input') }) })
    const host = defineComponent({
      setup: () => () => h(StorefrontFormShell, { ...baseProps, loading: true }, { default: () => h(field) }),
    })
    const { wrapper } = await mountScreen(host)

    expect(wrapper.find('[data-testid="ui-field-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('input').exists()).toBe(false)
  })

  it('shows the refusal of a save next to the footer', async () => {
    const { wrapper } = await mountScreen(hostWith({ error: 'Name taken.' }))

    expect(wrapper.get('[data-testid="widget-form-error"]').text()).toBe('Name taken.')
    expect(wrapper.get('[data-testid="widget-form-error"]').attributes('data-tone')).toBe('danger')
  })

  it('shows a refused load with a retry instead of the body', async () => {
    const { wrapper } = await mountScreen(hostWith({ loadError: 'Widgets are locked.' }))
    const shell = wrapper.findComponent(StorefrontFormShell)

    expect(wrapper.get('[data-testid="widget-form-load-error"]').text()).toBe('Widgets are locked.')
    expect(wrapper.find('[data-testid="body"]').exists()).toBe(false)

    await wrapper.get('[data-testid="widget-form-load-error-retry"]').trigger('click')
    expect(shell.emitted('retry')).toHaveLength(1)
  })

  it('emits save and cancel from the footer', async () => {
    const { wrapper } = await mountScreen(hostWith())
    const shell = wrapper.findComponent(StorefrontFormShell)

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await wrapper.get('[data-testid="settings-form-cancel"]').trigger('click')

    expect(shell.emitted('save')).toHaveLength(1)
    expect(shell.emitted('cancel')).toHaveLength(1)
  })

  it('asks before leaving a dirty form and lets a clean one go', async () => {
    const dirty = await mountScreen(hostWith({ dirty: true }), {}, '/widgets/new', '/widgets/new')
    void dirty.router.push('/elsewhere')
    await vi.waitFor(() => {
      expect(dirty.wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(true)
    })
    expect(dirty.router.currentRoute.value.path).toBe('/widgets/new')

    const clean = await mountScreen(hostWith(), {}, '/widgets/new', '/widgets/new')
    await clean.router.push('/elsewhere')
    expect(clean.router.currentRoute.value.path).toBe('/elsewhere')
  })
})
