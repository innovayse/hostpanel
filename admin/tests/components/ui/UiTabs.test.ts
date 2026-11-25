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
  it('keeps its scrollbar unless asked to hide it, so it cannot run into the selected tab\'s underline', () => {
    const items = [{ value: 'a', label: 'A' }, { value: 'b', label: 'B' }]
    const shown = mount(UiTabs, { props: { items, modelValue: 'a' } })
    const hidden = mount(UiTabs, { props: { items, modelValue: 'a', hideScrollbar: true } })

    expect(shown.get('[role="tablist"]').classes().join(' ')).not.toContain('scrollbar-width')
    expect(hidden.get('[role="tablist"]').classes().join(' ')).toContain('scrollbar-width:none')
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
    expect(classes.filter(name => /^border-[trlxy]-/.test(name) || name === 'border')).toEqual(['border-b-2'])
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

  it('scrolls the selected tab to the middle of the strip on mount, never the page', () => {
    const { scrolled, options, restore } = recordScrolls()
    try {
      mount(UiTabs, { props: { items, modelValue: 'c' } })

      expect(scrolled).toEqual(['tab-c'])
      expect(options[0]).toEqual({ inline: 'center', block: 'nearest' })
    } finally {
      restore()
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
