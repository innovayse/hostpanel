/**
 * Unit tests for UiDialog: plain and confirm variants, closing paths and accessibility.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { h, nextTick, ref } from 'vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the dialog with the teleport inlined so its content is queryable.
 *
 * @param props - Props for the dialog.
 * @param slots - Slots for the dialog.
 * @returns The wrapper.
 */
const mountDialog = (props: InstanceType<typeof UiDialog>['$props'] = {}, slots: Record<string, string> = {}) =>
  mount(UiDialog, {
    props,
    slots,
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

describe('UiDialog', () => {
  it('renders a labelled modal dialog with a title and body', () => {
    const wrapper = mountDialog({ title: 'Edit client' }, { default: '<p data-testid="body">Hello</p>' })
    const dialog = wrapper.get('[role="dialog"]')

    expect(dialog.attributes('aria-modal')).toBe('true')
    expect(dialog.attributes('aria-labelledby')).toBe(wrapper.get('h2').attributes('id'))
    expect(wrapper.get('h2').text()).toBe('Edit client')
    expect(wrapper.find('[data-testid="body"]').exists()).toBe(true)
  })

  it('puts the attributes a caller passes onto the dialog panel', () => {
    const wrapper = mount(UiDialog, {
      props: { title: 'Cancel invoices' },
      attrs: { 'data-testid': 'cancel-invoices-dialog' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })

    expect(wrapper.get('[data-testid="cancel-invoices-dialog"]').attributes('role')).toBe('dialog')
  })

  it('pins header and footer and scrolls only the body region', () => {
    const wrapper = mountDialog({ title: 'Create product' }, { footer: '<button>Save</button>' })
    const panelClasses = wrapper.get('[role="dialog"]').classes()
    const bodyClasses = wrapper.get('[data-testid="dialog-body"]').classes()

    expect(panelClasses).toContain('max-h-[calc(100dvh-2rem)]')
    expect(panelClasses).toContain('flex-col')
    expect(panelClasses).not.toContain('overflow-y-auto')
    expect(bodyClasses).toContain('overflow-y-auto')
    expect(bodyClasses).toContain('min-h-0')
  })

  it('locks page scroll while open and restores it on unmount', () => {
    document.body.style.overflow = ''
    const wrapper = mountDialog({ title: 'x' })

    expect(document.body.style.overflow).toBe('hidden')

    wrapper.unmount()

    expect(document.body.style.overflow).toBe('')
  })

  it('returns focus to the opener on close', async () => {
    const trigger = document.createElement('button')
    document.body.appendChild(trigger)
    trigger.focus()
    const wrapper = mountDialog({ title: 'x' })

    wrapper.unmount()

    expect(document.activeElement).toBe(trigger)
    trigger.remove()
  })

  it('renders nothing when closed', () => {
    expect(mountDialog({ title: 'x', open: false }).find('[role="dialog"]').exists()).toBe(false)
  })

  it('closes from the header button', async () => {
    const wrapper = mountDialog({ title: 'x' })

    await wrapper.get('[data-testid="dialog-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('closes on Escape', async () => {
    const wrapper = mountDialog({ title: 'x' })

    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('closes only the topmost of two nested dialogs on Escape', async () => {
    // The inner dialog opens after the outer one, the way a confirmation opens from a dialog.
    const innerOpen = ref(false)
    const outer = mount(UiDialog, {
      props: { title: 'outer' },
      slots: { default: () => h(UiDialog, { title: 'inner', open: innerOpen.value }) },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })
    innerOpen.value = true
    await nextTick()
    const inner = outer.findAllComponents(UiDialog).find(dialog => dialog.props('title') === 'inner')

    await outer.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })

    expect(inner?.emitted('close')).toHaveLength(1)
    expect(outer.emitted('close')).toBeUndefined()
  })

  it('closes on a backdrop click unless that is turned off', async () => {
    const closing = mountDialog({ title: 'x' })
    const sticky = mountDialog({ title: 'x', closeOnBackdrop: false })

    await closing.get('.fixed').trigger('click')
    await sticky.get('.fixed').trigger('click')

    expect(closing.emitted('close')).toHaveLength(1)
    expect(sticky.emitted('close')).toBeUndefined()
  })

  it('shows the message and both buttons in the confirm variant', async () => {
    const wrapper = mountDialog({ variant: 'confirm', title: 'Delete', message: 'Are you sure?', tone: 'danger' })

    expect(wrapper.text()).toContain('Are you sure?')
    expect(wrapper.get('[data-testid="dialog-confirm"]').classes()).toContain('bg-status-red')
    expect(wrapper.get('[data-testid="dialog-confirm"]').text()).toBe('Confirm')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')

    expect(wrapper.emitted('confirm')).toHaveLength(1)
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('uses the given confirm label and swaps it while loading', () => {
    const idle = mountDialog({ variant: 'confirm', confirmLabel: 'Delete' })
    const busy = mountDialog({ variant: 'confirm', confirmLabel: 'Delete', loading: true, loadingLabel: 'Deleting...' })

    expect(idle.get('[data-testid="dialog-confirm"]').text()).toBe('Delete')
    expect(busy.get('[data-testid="dialog-confirm"]').text()).toBe('Deleting...')
    expect(busy.get('[data-testid="dialog-confirm"]').attributes('disabled')).toBeDefined()
  })

  it('has no confirm buttons in the plain variant, but renders a footer slot', () => {
    const wrapper = mountDialog({ title: 'x' }, { footer: '<button data-testid="save">Save</button>' })

    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="save"]').exists()).toBe(true)
  })

  it('fades and scales in, and honours reduced motion', () => {
    const wrapper = mountDialog({ title: 'Edit client' })
    const transition = wrapper.findComponent({ name: 'Transition' })

    expect(transition.props('appear')).toBe(true)
    expect(transition.props('enterActiveClass')).toContain('duration-200')
    expect(transition.props('enterActiveClass')).toContain('motion-reduce:transition-none')
    expect(transition.props('leaveActiveClass')).toContain('motion-reduce:transition-none')
  })
})
