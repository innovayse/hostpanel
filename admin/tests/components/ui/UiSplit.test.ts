/**
 * Unit tests for UiSplit: ratio templates that stack on narrow screens, and the aside-to-sheet mode below lg.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSplit from '@/components/ui/UiSplit.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts a split in aside-to-sheet mode.
 *
 * @param phone - Whether the screen is below the `lg` breakpoint.
 * @param open - Whether the sheet is open.
 * @returns The wrapper.
 */
const mountSheeted = (phone: boolean, open = false) => {
  vi.stubGlobal('matchMedia', () => ({ matches: phone, addEventListener: vi.fn(), removeEventListener: vi.fn() }))
  return mount(UiSplit, {
    props: { asideTitle: 'Settings', open },
    slots: { default: '<p data-testid="main">Main</p>', aside: '<p data-testid="aside">Aside</p>' },
    global: { plugins: [createTestI18n()] },
    attachTo: document.body,
  })
}

describe('UiSplit', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  it('is two thirds / one third by default', () => {
    expect(mount(UiSplit).classes()).toEqual(expect.arrayContaining(['grid', 'grid-cols-1', 'lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]', 'gap-6']))
  })

  it.each([
    ['half', 'lg:grid-cols-2'],
    ['three-quarters', 'lg:grid-cols-[minmax(0,3fr)_minmax(0,1fr)]'],
    ['wide-main', 'lg:grid-cols-[minmax(0,1.7fr)_minmax(0,1fr)]'],
    ['wide-aside', 'lg:grid-cols-[minmax(0,1fr)_minmax(0,1.7fr)]'],
  ] as const)('maps the %s ratio to %s', (ratio, expected) => {
    expect(mount(UiSplit, { props: { ratio } }).classes()).toContain(expected)
  })

  it('keeps the aside in a titled card beside the main area on a wide screen', () => {
    const wrapper = mountSheeted(false)

    expect(wrapper.find('[data-testid="aside"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Settings')
  })

  it('moves the aside into a bottom sheet below lg and shows it once opened', () => {
    const closed = mountSheeted(true)
    expect(closed.find('[data-testid="aside"]').exists()).toBe(false)
    closed.unmount()

    mountSheeted(true, true)
    expect(document.body.querySelector('[data-testid="drawer-body"] [data-testid="aside"]')).not.toBeNull()
  })
})
