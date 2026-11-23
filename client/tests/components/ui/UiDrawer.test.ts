/**
 * Unit tests for UiDrawer: open and closed rendering, the title, closing from the button, the
 * backdrop and Escape, the side and width, the footer slot and the page scroll lock.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiDrawer from '~/components/ui/UiDrawer.vue'
import { UiDrawerSide, UiPanelSize } from '~/utils/ui/enums'

/** Wrappers mounted by the current test, unmounted afterwards so the scroll lock is released. */
const mounted: Array<{ unmount: () => void }> = []

/**
 * Mounts a drawer and remembers it for cleanup.
 *
 * @param props Props for the drawer.
 * @param slots Slot text by name.
 * @returns The wrapper.
 */
const open = (props: Record<string, unknown> = {}, slots: Record<string, string> = {}) => {
  const wrapper = mountWithKit(UiDrawer, { props: { modelValue: true, title: 'Cart', ...props }, slots })
  mounted.push(wrapper)
  return wrapper
}

describe('UiDrawer', () => {
  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
  })

  it('renders nothing while closed', () => {
    expect(open({ modelValue: false }).find('[data-testid="ui-drawer"]').exists()).toBe(false)
  })

  it('renders an accessible panel with the title and body', () => {
    const wrapper = open({}, { default: 'Items' })

    const panel = wrapper.find('[role="dialog"]')
    expect(panel.attributes('aria-label')).toBe('Cart')
    expect(wrapper.find('h2').text()).toBe('Cart')
    expect(wrapper.text()).toContain('Items')
  })

  it('closes from the close button', async () => {
    const wrapper = open()

    await wrapper.find('[data-testid="ui-drawer-close"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([false])
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('closes on a backdrop click', async () => {
    const wrapper = open()

    await wrapper.find('[data-testid="ui-drawer-backdrop"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('closes on Escape', async () => {
    const wrapper = open()

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await nextTick()

    expect(wrapper.emitted('close')).toBeTruthy()
  })

  it('attaches to the right edge by default and to the left on request', () => {
    expect(open().find('[role="dialog"]').classes()).toContain('right-0')
    expect(open({ side: UiDrawerSide.Left }).find('[role="dialog"]').classes()).toContain('left-0')
  })

  it('sizes the panel', () => {
    expect(open({ size: UiPanelSize.Lg }).find('[role="dialog"]').classes()).toContain('sm:max-w-lg')
  })

  it('renders the footer slot only when given', () => {
    expect(open({}, { footer: 'Checkout' }).text()).toContain('Checkout')
    expect(open().text()).not.toContain('Checkout')
  })

  it('locks page scrolling while open', () => {
    open()

    expect(document.body.classList.contains('overflow-hidden')).toBe(true)
  })
})
