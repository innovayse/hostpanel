/**
 * Unit tests for UiDialog: plain and confirm variants, closing paths and accessibility.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { h, nextTick, ref } from 'vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import { UiDrawerPresentation } from '@/utils/ui/drawer'
import { createTestI18n } from '../../helpers/i18n'
import { stubPhone } from '../../helpers/phone'

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

afterEach(() => {
  vi.unstubAllGlobals()
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
    expect(wrapper.get('[data-testid="dialog-confirm"]').classes()).toContain('bg-tint-red')
    expect(mountDialog({ variant: 'confirm', title: 'Save', message: 'Sure?' }).get('[data-testid="dialog-confirm"]').classes()).toContain('text-text-on-primary')
    expect(wrapper.get('[data-testid="dialog-confirm"]').text()).toBe('Confirm')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')

    expect(wrapper.emitted('confirm')).toHaveLength(1)
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('uses the given confirm label and keeps it, with a spinner, while loading', () => {
    const idle = mountDialog({ variant: 'confirm', confirmLabel: 'Delete' })
    const busy = mountDialog({ variant: 'confirm', confirmLabel: 'Delete', loading: true })

    expect(idle.get('[data-testid="dialog-confirm"]').text()).toBe('Delete')
    expect(busy.get('[data-testid="dialog-confirm"]').text()).toBe('Delete')
    expect(busy.get('[data-testid="dialog-confirm"]').attributes('aria-busy')).toBe('true')
    expect(busy.get('[data-testid="dialog-confirm"]').attributes('disabled')).toBeDefined()
  })

  it('has no confirm buttons in the plain variant, but renders a footer slot', () => {
    const wrapper = mountDialog({ title: 'x' }, { footer: '<button data-testid="save">Save</button>' })

    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="save"]').exists()).toBe(true)
  })

  it('stacks the footer actions at full width on phones, right-aligned above', () => {
    const wrapper = mountDialog({ title: 'x' }, { footer: '<button>Save</button>' })

    expect(wrapper.get('[data-testid="ui-dialog-footer"]').classes()).toEqual(expect.arrayContaining(['justify-end', 'max-sm:flex-col-reverse', 'max-sm:items-stretch']))
  })
})

describe('UiDialog on a phone and while work runs', () => {
  it('takes the row between the page gutters on a phone and the size from sm up', () => {
    const classes = mountDialog({ title: 'x', size: 'md' }).get('[role="dialog"]').classes()

    expect(classes).toEqual(expect.arrayContaining(['max-w-[calc(100vw-2rem)]', 'sm:max-w-md']))
  })

  it('is a full-screen sheet on a phone for a plain dialog, with its slots, attributes and footer', () => {
    stubPhone(true)
    const wrapper = mount(UiDialog, {
      props: { title: 'Edit client' },
      attrs: { 'data-testid': 'edit-dialog' },
      slots: { default: '<p data-testid="body">Hello</p>', footer: '<b data-testid="footer" />' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })

    expect(wrapper.findComponent(UiDrawer).props('presentation')).toBe(UiDrawerPresentation.Sheet)
    expect(wrapper.findComponent(UiDrawer).props('fullscreen')).toBe(true)
    expect(wrapper.get('[data-testid="edit-dialog"]').classes()).toEqual(expect.arrayContaining(['inset-0', 'h-dvh']))
    expect(wrapper.get('[data-testid="edit-dialog"]').classes()).not.toContain('rounded-t-panel')
    expect(wrapper.find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(wrapper.get('h2').text()).toBe('Edit client')
    expect(wrapper.find('[data-testid="body"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="footer"]').exists()).toBe(true)
  })

  it('makes the body an inline-size container in the panel and in the phone sheet, so a container grid counts by the dialog width', () => {
    stubPhone(false)
    const panel = mountDialog({ title: 'x' }, { default: '<p>Body</p>' })
    expect(panel.get('[data-testid="dialog-body"]').classes()).toContain('@container')

    stubPhone(true)
    const sheet = mount(UiDialog, {
      props: { title: 'x' },
      slots: { default: '<p>Body</p>' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })
    expect(sheet.get('[data-testid="dialog-body"]').classes()).toContain('@container')
  })

  it('stays a centred card on a phone for a confirm prompt and on a wide screen for a plain dialog', () => {
    stubPhone(true)
    const confirm = mountDialog({ title: 'x', variant: 'confirm', message: 'Sure?' })
    expect(confirm.findComponent(UiDrawer).exists()).toBe(false)
    expect(confirm.find('[data-testid="dialog-backdrop"]').exists()).toBe(true)

    stubPhone(false)
    const plain = mountDialog({ title: 'x' })
    expect(plain.findComponent(UiDrawer).exists()).toBe(false)
    expect(plain.find('[data-testid="dialog-backdrop"]').exists()).toBe(true)
  })

  it('closes the phone sheet from the close button, Escape and the backdrop, and honours closeOnBackdrop', async () => {
    stubPhone(true)
    const wrapper = mountDialog({ title: 'x' })

    await wrapper.get('[data-testid="drawer-close"]').trigger('click')
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('.fixed').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(3)

    const pinned = mountDialog({ title: 'x', closeOnBackdrop: false })
    await pinned.get('.fixed').trigger('click')
    expect(pinned.emitted('close')).toBeUndefined()
  })

  it('cannot be dismissed as a phone sheet while it is not dismissible', async () => {
    stubPhone(true)
    const wrapper = mountDialog({ title: 'Working', dismissible: false })

    expect(wrapper.find('[data-testid="drawer-close"]').exists()).toBe(false)
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('.fixed').trigger('click')

    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('stacks the confirm buttons at full width on a phone', () => {
    const wrapper = mountDialog({ title: 'x', variant: 'confirm', message: 'Sure?' })

    expect(wrapper.get('[data-testid="dialog-cancel"]').element.parentElement?.className).toContain('max-sm:flex-col-reverse')
  })

  it('cannot be dismissed while it is not dismissible: no close button, Escape and the backdrop do nothing', async () => {
    const wrapper = mountDialog({ title: 'Working', dismissible: false })

    expect(wrapper.find('[data-testid="dialog-close"]').exists()).toBe(false)
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('[data-testid="dialog-backdrop"]').trigger('click')

    expect(wrapper.emitted('close')).toBeUndefined()
  })
})

describe('UiDialog surface', () => {
  it('is an elevated glass panel with the panel radius', () => {
    const classes = mountDialog({ title: 'Edit' }).get('[role="dialog"]').classes()

    expect(classes).toEqual(expect.arrayContaining(['glass-strong', 'rounded-panel']))
    expect(classes).not.toContain('bg-surface-card')
  })
})
