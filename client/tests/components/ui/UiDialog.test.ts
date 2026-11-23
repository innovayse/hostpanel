/**
 * Unit tests for UiDialog: open and closed rendering, the header and close button, backdrop and
 * Escape closing, the footer slot, the width and the page scroll lock.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiDialog from '~/components/ui/UiDialog.vue'
import { UiPanelSize } from '~/utils/ui/enums'

/** Wrappers mounted by the current test, unmounted afterwards so the scroll lock is released. */
const mounted: Array<{ unmount: () => void }> = []

/**
 * Mounts a dialog and remembers it for cleanup.
 *
 * @param props Props for the dialog.
 * @param slots Slot text by name.
 * @returns The wrapper.
 */
const open = (props: Record<string, unknown> = {}, slots: Record<string, string> = {}) => {
  const wrapper = mountWithKit(UiDialog, { props: { modelValue: true, ...props }, slots })
  mounted.push(wrapper)
  return wrapper
}

describe('UiDialog', () => {
  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
  })

  it('renders nothing while closed', () => {
    const wrapper = open({ modelValue: false })

    expect(wrapper.find('[data-testid="ui-dialog"]').exists()).toBe(false)
  })

  it('renders an accessible dialog with title and body when open', () => {
    const wrapper = open({ title: 'Edit profile' }, { default: 'Body text' })

    const panel = wrapper.find('[role="dialog"]')
    expect(panel.attributes('aria-modal')).toBe('true')
    expect(panel.attributes('aria-label')).toBe('Edit profile')
    expect(wrapper.find('h3').text()).toBe('Edit profile')
    expect(wrapper.text()).toContain('Body text')
  })

  it('has no header without a title or header slot', () => {
    const wrapper = open({}, { default: 'Body' })

    expect(wrapper.find('h3').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-dialog-close"]').exists()).toBe(false)
  })

  it('closes from the close button', async () => {
    const wrapper = open({ title: 'T' })

    await wrapper.find('[data-testid="ui-dialog-close"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([false])
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('hides the close button when not closable', () => {
    const wrapper = open({ title: 'T', closable: false })

    expect(wrapper.find('[data-testid="ui-dialog-close"]').exists()).toBe(false)
  })

  it('closes on a backdrop click unless told not to', async () => {
    const closing = open({ title: 'T' })
    const sticky = open({ title: 'T', closeOnBackdrop: false })

    await closing.find('[data-testid="ui-dialog-backdrop"]').trigger('click')
    await sticky.find('[data-testid="ui-dialog-backdrop"]').trigger('click')

    expect(closing.emitted('close')).toHaveLength(1)
    expect(sticky.emitted('close')).toBeUndefined()
  })

  it('renders the footer slot', () => {
    const wrapper = open({ title: 'T' }, { footer: 'Footer actions' })

    expect(wrapper.text()).toContain('Footer actions')
  })

  it('sizes the panel', () => {
    const wrapper = open({ size: UiPanelSize.Xl })

    expect(wrapper.find('[role="dialog"]').classes()).toContain('sm:max-w-2xl')
  })

  it('closes on Escape', async () => {
    const wrapper = open({ title: 'T' })

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await nextTick()

    expect(wrapper.emitted('close')).toBeTruthy()
  })

  it('locks page scrolling while open and releases it on unmount', () => {
    const wrapper = open({})
    expect(document.body.classList.contains('overflow-hidden')).toBe(true)

    wrapper.unmount()
    mounted.pop()

    expect(document.body.classList.contains('overflow-hidden')).toBe(false)
  })

  it('makes the body the scroll container while header and footer stay pinned', () => {
    const wrapper = open({ title: 'T' }, { default: 'Body', footer: 'Foot' })

    const body = wrapper.find('[data-testid="ui-dialog-body"]')
    expect(body.classes()).toEqual(expect.arrayContaining(['overflow-y-auto', 'min-h-0', 'flex-1']))
    const panel = wrapper.find('[role="dialog"]')
    expect(panel.classes()).toEqual(expect.arrayContaining(['flex', 'flex-col', 'overflow-hidden']))
    expect(panel.classes()).not.toContain('overflow-y-auto')
  })

  it('frames the panel with a border so it stays distinct from the page in dark mode', () => {
    const panel = open({ title: 'T' }).find('[role="dialog"]')

    expect(panel.classes()).toEqual(expect.arrayContaining(['sm:border', 'dark:border-white/15']))
  })

  it('keeps the page locked until the last of two nested dialogs closes', () => {
    const outer = open({})
    const inner = open({})
    inner.unmount()
    mounted.pop()
    expect(document.body.classList.contains('overflow-hidden')).toBe(true)

    outer.unmount()
    mounted.shift()
    expect(document.body.classList.contains('overflow-hidden')).toBe(false)
  })

  it('keeps its padding small on a phone and wraps footer actions instead of overflowing', () => {
    const wrapper = open({ title: 'T' }, { default: 'Body', footer: 'Foot' })

    expect(wrapper.find('[data-testid="ui-dialog-body"]').classes()).toEqual(expect.arrayContaining(['p-4', 'sm:p-6']))
    expect(wrapper.find('[role="dialog"] > div:last-child').classes()).toContain('flex-wrap')
  })

  it('is a full-screen sheet below sm and a centred card from sm', () => {
    const wrapper = open({ title: 'T' }, { default: 'Body', footer: 'Foot' })

    expect(wrapper.find('[role="dialog"]').classes()).toEqual(expect.arrayContaining(['h-dvh', 'w-full', 'rounded-none', 'sm:h-auto', 'sm:rounded-xl']))
    expect(wrapper.find('[data-testid="ui-dialog"]').classes()).not.toContain('p-4')
  })

  it('keeps the centred card on a phone when sheet is off', () => {
    const wrapper = open({ title: 'T', sheet: false })

    expect(wrapper.find('[role="dialog"]').classes()).toEqual(expect.arrayContaining(['rounded-xl', 'max-h-[90dvh]']))
    expect(wrapper.find('[data-testid="ui-dialog"]').classes()).toContain('p-4')
  })
})
