/**
 * Unit tests for UiTabs: tablist semantics, selection, arrow-key navigation and keeping the selected tab in view.
 */
import { describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import UiTabs from '@/components/ui/UiTabs.vue'
import { UiTabMarker } from '@/utils/ui/tabs'

/** Three tabs, the middle one disabled. */
const items = [
  { value: 'a', label: 'Alpha', testid: 'tab-a' },
  { value: 'b', label: 'Beta', disabled: true, testid: 'tab-b' },
  { value: 'c', label: 'Gamma', testid: 'tab-c' },
]

describe('UiTabs markers', () => {
  it('draws a dot with its accessible text on a marked tab and none on the others', () => {
    const marked = [
      { value: 'a', label: 'A', testid: 'tab-a', marker: UiTabMarker.Empty, markerLabel: 'Not filled in' },
      { value: 'b', label: 'B', testid: 'tab-b', marker: UiTabMarker.Invalid, markerLabel: 'Needs attention' },
      { value: 'c', label: 'C', testid: 'tab-c' },
    ]
    const wrapper = mount(UiTabs, { props: { items: marked, modelValue: 'a' } })

    expect(wrapper.get('[data-testid="tab-a-empty"]').attributes('aria-label')).toBe('Not filled in')
    expect(wrapper.get('[data-testid="tab-b-invalid"]').classes()).toContain('bg-status-red')
    expect(wrapper.get('[data-testid="tab-a-empty"]').classes()).toContain('bg-status-yellow')
    expect(wrapper.find('[data-testid="tab-c-empty"]').exists()).toBe(false)
    expect(wrapper.findAll('[role="tab"]').map(tab => tab.text())).toEqual(['A', 'B', 'C'])
  })
})

describe('UiTabs scrollbar', () => {
  it('always hides its scrollbar, so no grey track shows beside the selected tab underline', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    expect(wrapper.get('[role="tablist"]').classes()).toEqual(expect.arrayContaining(['no-scrollbar', 'overflow-x-auto']))
  })
})

describe('UiTabs edge fade', () => {
  /**
   * Gives the tab list a measured width, as a browser would, then lets the strip re-read it.
   *
   * @param list - The tab list element.
   * @param metrics - Its scroll position, visible width and full width.
   */
  const measure = async (list: HTMLElement, metrics: { scrollLeft: number; clientWidth: number; scrollWidth: number }): Promise<void> => {
    Object.defineProperties(list, {
      scrollLeft: { value: metrics.scrollLeft, configurable: true },
      clientWidth: { value: metrics.clientWidth, configurable: true },
      scrollWidth: { value: metrics.scrollWidth, configurable: true },
    })
    list.dispatchEvent(new Event('scroll'))
    await nextTick()
  }

  it('fades nothing while every tab fits', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })
    const list = wrapper.get('[role="tablist"]')

    await measure(list.element as HTMLElement, { scrollLeft: 0, clientWidth: 300, scrollWidth: 300 })

    expect(list.classes().join(' ')).not.toContain('mask-image')
  })

  it('fades only the end side at the start of an overflowing strip, then both sides, then only the start side at its end', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })
    const list = wrapper.get('[role="tablist"]')

    await measure(list.element as HTMLElement, { scrollLeft: 0, clientWidth: 300, scrollWidth: 600 })
    expect(list.classes().join(' ')).toContain('to_right,black_calc(100%-2rem),transparent')

    await measure(list.element as HTMLElement, { scrollLeft: 100, clientWidth: 300, scrollWidth: 600 })
    expect(list.classes().join(' ')).toContain('to_right,transparent,black_2rem')

    await measure(list.element as HTMLElement, { scrollLeft: 300, clientWidth: 300, scrollWidth: 600 })
    expect(list.classes().join(' ')).toContain('to_left,black_calc(100%-2rem),transparent')
  })
})

describe('UiTabs focus ring', () => {
  it('draws the keyboard ring inset and only on focus-visible, so the strip clip cannot open it into a box', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })
    const classes = wrapper.get('[role="tab"]').classes()

    expect(classes).toContain('focus-visible:ring-2')
    expect(classes).toContain('focus-visible:ring-inset')
    expect(classes.filter(name => name.startsWith('ring-') || name.startsWith('focus:'))).toEqual([])
  })

  it('draws the selected tab as an underline only', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })
    const classes = wrapper.get('[role="tab"]').classes()

    expect(classes).toContain('border-b-2')
    expect(classes).toContain('border-primary-500')
    expect(classes.filter(name => /^border-[trblxy]-/.test(name) || name === 'border')).toEqual(['border-b-2'])
  })
})

describe('UiTabs', () => {
  it('renders a tablist with the selected tab marked', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })
    const tabs = wrapper.findAll('[role="tab"]')

    expect(wrapper.find('[role="tablist"]').exists()).toBe(true)
    expect(tabs.map(t => t.text())).toEqual(['Alpha', 'Beta', 'Gamma'])
    expect(tabs[0]?.attributes('aria-selected')).toBe('true')
    expect(tabs[2]?.attributes('aria-selected')).toBe('false')
  })

  it('hides vertical overflow on the horizontally scrollable tablist', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    expect(wrapper.get('[role="tablist"]').classes()).toContain('overflow-y-hidden')
  })

  it('selects a tab on click', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    await wrapper.get('[data-testid="tab-c"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['c'])
  })

  it('does not select a disabled tab', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    await wrapper.get('[data-testid="tab-b"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('moves to the next enabled tab with the arrow keys, wrapping around', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    await wrapper.get('[data-testid="tab-a"]').trigger('keydown', { key: 'ArrowRight' })
    await wrapper.get('[data-testid="tab-a"]').trigger('keydown', { key: 'ArrowLeft' })

    expect(wrapper.emitted('update:modelValue')).toEqual([['c'], ['c']])
  })

  it('moves focus to the tab the arrow key selects and keeps only the selected tab in the tab order', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' }, attachTo: document.body })

    await wrapper.get('[data-testid="tab-a"]').trigger('keydown', { key: 'ArrowRight' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="tab-c"]').element)

    await wrapper.setProps({ modelValue: 'c' })
    expect(wrapper.get('[data-testid="tab-c"]').attributes('tabindex')).toBe('0')
    expect(wrapper.get('[data-testid="tab-a"]').attributes('tabindex')).toBe('-1')
    wrapper.unmount()
  })

  it('gives the scoped slot the active value', () => {
    const wrapper = mount(UiTabs, {
      props: { items, modelValue: 'c' },
      slots: { default: '<template #default="{ active }"><p data-testid="panel">{{ active }}</p></template>' },
    })

    expect(wrapper.get('[data-testid="panel"]').text()).toBe('c')
  })

  it('colours the selected tab with the primary accent and keeps idle tabs readable', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    expect(wrapper.get('[data-testid="tab-a"]').classes()).toContain('text-text-accent')
    expect(wrapper.get('[data-testid="tab-c"]').classes()).toContain('text-text-secondary')
  })

  it('draws the slot in a panel set 22px under the tab line, and no panel without a slot', () => {
    const withSlot = mount(UiTabs, { props: { items, modelValue: 'a' }, slots: { default: '<p data-testid="body" />' } })
    const without = mount(UiTabs, { props: { items, modelValue: 'a' } })

    expect(withSlot.get('[data-testid="ui-tabs-panel"]').classes()).toContain('mt-5.5')
    expect(withSlot.find('[data-testid="ui-tabs-panel"] [data-testid="body"]').exists()).toBe(true)
    expect(without.find('[data-testid="ui-tabs-panel"]').exists()).toBe(false)
  })
})

describe('UiTabs reveal of the selected tab', () => {
  /**
   * Replaces `scrollIntoView` with a recorder for the duration of one test.
   *
   * @returns The ids of the tabs scrolled into view, in order, and the restore function.
   */
  const recordScrolls = () => {
    const scrolled: string[] = []
    const options: ScrollIntoViewOptions[] = []
    const original = HTMLElement.prototype.scrollIntoView
    HTMLElement.prototype.scrollIntoView = function scrollIntoView(this: HTMLElement, arg?: boolean | ScrollIntoViewOptions) {
      scrolled.push(this.getAttribute('data-testid') ?? '')
      options.push(typeof arg === 'object' ? arg : {})
    }
    return { scrolled, options, restore: () => { HTMLElement.prototype.scrollIntoView = original } }
  }

  it('scrolls the selected tab just into view on mount, smoothly, never the page', () => {
    const { scrolled, options, restore } = recordScrolls()
    try {
      mount(UiTabs, { props: { items, modelValue: 'c' } })

      expect(scrolled).toEqual(['tab-c'])
      expect(options[0]).toEqual({ inline: 'nearest', block: 'nearest', behavior: 'smooth' })
    } finally {
      restore()
    }
  })

  it('jumps instead of gliding to the selected tab under reduced motion', () => {
    const original = window.matchMedia
    window.matchMedia = ((query: string) => ({ matches: true, media: query, addEventListener: () => undefined, removeEventListener: () => undefined })) as unknown as typeof window.matchMedia
    const { options, restore } = recordScrolls()
    try {
      mount(UiTabs, { props: { items, modelValue: 'c' } })

      expect(options[0]).toEqual({ inline: 'nearest', block: 'nearest', behavior: 'auto' })
    } finally {
      restore()
      window.matchMedia = original
    }
  })

  it('scrolls the newly selected tab into view when the selection changes', async () => {
    const { scrolled, restore } = recordScrolls()
    try {
      const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })
      scrolled.length = 0

      await wrapper.setProps({ modelValue: 'c' })
      await nextTick()

      expect(scrolled).toEqual(['tab-c'])
    } finally {
      restore()
    }
  })

  it('moves nothing when no tab is selected (a loading strip)', () => {
    const { scrolled, restore } = recordScrolls()
    try {
      mount(UiTabs, { props: { items, modelValue: '' } })

      expect(scrolled).toEqual([])
    } finally {
      restore()
    }
  })

  it('brings the selected tab back into view when the strip or a tab changes size, and watches every tab', async () => {
    /** Callbacks of the observers created, so the test can play a resize. */
    const callbacks: ResizeObserverCallback[] = []
    /** Every element an observer was asked to watch. */
    const observed: Element[] = []
    vi.stubGlobal('ResizeObserver', class {
      /**
       * Keeps the callback to play later.
       *
       * @param callback - The strip's resize callback.
       */
      constructor(callback: ResizeObserverCallback) {
        callbacks.push(callback)
      }

      /**
       * Records the observed element.
       *
       * @param target - The element the strip watches.
       */
      observe = (target: Element): void => {
        observed.push(target)
      }

      /** Not used by the strip. */
      unobserve = (): void => {}

      /** Not used by the test. */
      disconnect = (): void => {}
    })
    const { scrolled, restore } = recordScrolls()
    try {
      const wrapper = mount(UiTabs, { props: { items, modelValue: 'c' } })
      await nextTick()
      scrolled.length = 0

      expect(observed).toContain(wrapper.get('[role="tablist"]').element)
      for (const tab of wrapper.findAll('[role="tab"]')) {
        expect(observed).toContain(tab.element)
      }

      callbacks[0]?.([], {} as ResizeObserver)

      expect(scrolled).toEqual(['tab-c'])
    } finally {
      restore()
      vi.unstubAllGlobals()
    }
  })
})
