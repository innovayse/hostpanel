/**
 * Unit tests for UiChipRow: every chip when they fit, the "+N" chip with the rest in its hint when they do not, long
 * labels that never wrap, the count following the row's width, and a row that cannot hold even one chip.
 *
 * jsdom lays nothing out, so the widths are faked: a chip is as wide as ten pixels per character and the row is a
 * hundred pixels wide until the stubbed `ResizeObserver` reports another width.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChipRow from '@/components/ui/UiChipRow.vue'

/** Width in pixels the row has until a resize is reported. */
const ROW_WIDTH = 100

/** Callback the component registered on the stubbed observer, null before it mounted. */
let reportResize: ResizeObserverCallback | null = null

beforeEach(() => {
  reportResize = null
  vi.stubGlobal('ResizeObserver', class {
    /**
     * Keeps the callback so a test can report a new width.
     *
     * @param callback - The component's resize callback.
     */
    constructor(callback: ResizeObserverCallback) {
      reportResize = callback
    }
    /** Watches nothing: the test reports sizes itself. */
    observe = (): void => {}
    /** Nothing is watched. */
    unobserve = (): void => {}
    /** Nothing is watched. */
    disconnect = (): void => {}
  })
  vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockImplementation(function (this: HTMLElement) {
    return (this.textContent ?? '').trim().length * 10
  })
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(ROW_WIDTH)
})

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('UiChipRow', () => {
  it('draws every chip, and no "+N" chip, when they fit', () => {
    const wrapper = mount(UiChipRow, { props: { chips: ['#a', '#bb', '#ccc'] } })

    expect(wrapper.findAll('[data-testid="ui-chip-row-chip"]').map(chip => chip.text())).toEqual(['#a', '#bb', '#ccc'])
    expect(wrapper.find('[data-testid="ui-chip-row-more"]').exists()).toBe(false)
  })

  it('draws the chips that fit and a "+N" chip whose hint lists the rest', () => {
    const wrapper = mount(UiChipRow, { props: { chips: ['#aaaa', '#bbbb', '#cccc'] } })

    expect(wrapper.findAll('[data-testid="ui-chip-row-chip"]').map(chip => chip.text())).toEqual(['#aaaa'])
    expect(wrapper.get('[data-testid="ui-chip-row-more"]').text()).toBe('+2')
    expect(wrapper.get('[role="tooltip"]').text()).toBe('#bbbb, #cccc')
  })

  it('never lets a chip wrap and keeps one gap between the chips', () => {
    const wrapper = mount(UiChipRow, { props: { chips: ['#control-panel', '#cyberpanel'] } })

    expect(wrapper.get('[data-testid="ui-chip-row-chip"]').classes()).toContain('whitespace-nowrap')
    expect(wrapper.classes()).toContain('gap-1')
    expect(wrapper.classes()).toContain('min-w-0')
  })

  it('follows the width the observer reports', async () => {
    const wrapper = mount(UiChipRow, { props: { chips: ['#aaaa', '#bbbb', '#cccc'] } })

    reportResize?.([{ contentRect: { width: 200 } } as ResizeObserverEntry], {} as ResizeObserver)
    await wrapper.vm.$nextTick()

    expect(wrapper.findAll('[data-testid="ui-chip-row-chip"]')).toHaveLength(3)
    expect(wrapper.find('[data-testid="ui-chip-row-more"]').exists()).toBe(false)
  })

  it('draws only the "+N" chip when not even one chip fits beside it', () => {
    const wrapper = mount(UiChipRow, { props: { chips: ['#a-very-long-tag-name'] } })

    expect(wrapper.find('[data-testid="ui-chip-row-chip"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="ui-chip-row-more"]').text()).toBe('+1')
  })

  it('measures again when the labels change', async () => {
    const wrapper = mount(UiChipRow, { props: { chips: ['#a'] } })

    await wrapper.setProps({ chips: ['#aaaa', '#bbbb', '#cccc'] })
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="ui-chip-row-more"]').text()).toBe('+2')
  })
})
