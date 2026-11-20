/**
 * Unit tests for UiBulkBar: shown only with a selection, toolbar semantics, the count, the slots and clearing.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiBulkBar from '@/components/ui/UiBulkBar.vue'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import type { UiBulkAction } from '@/utils/ui/bulkBar'
import { createTestI18n } from '../../helpers/i18n'

/** Two actions: a primary one and a danger one, each with a short label and a full sentence as its title. */
const ACTIONS: UiBulkAction[] = [
  { key: 'paid', label: 'Paid', title: 'Mark as paid', icon: Icon.Check, variant: UiButtonVariant.Primary, testid: 'act' },
  { key: 'remove', label: 'Delete', title: 'Delete the invoices', icon: Icon.Trash, variant: UiButtonVariant.Danger, testid: 'act-remove' },
]

/** The bars mounted by the running test, unmounted afterwards so none keeps listening for Escape. */
const mounted: VueWrapper[] = []

/**
 * Mounts the bar with its i18n plugin; the teleported content is reachable in the document.
 *
 * @param count - How many rows are selected.
 * @param slots - Optional slots (the summary).
 * @returns The wrapper.
 */
const mountBar = (count: number, slots: Record<string, string> = {}) => {
  const wrapper = mount(UiBulkBar, {
    props: { count, actions: ACTIONS },
    slots,
    attrs: { 'data-testid': 'bar' },
    attachTo: document.body,
    global: { plugins: [createTestI18n()] },
  })
  mounted.push(wrapper)
  return wrapper
}

describe('UiBulkBar', () => {
  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
    document.body.innerHTML = ''
  })

  it('renders nothing without a selection', () => {
    mountBar(0)

    expect(document.body.querySelector('[role="toolbar"]')).toBeNull()
  })

  it('is a named toolbar teleported to the body, carrying the caller attributes', () => {
    mountBar(2)

    const bar = document.body.querySelector('[role="toolbar"]')
    expect(bar).not.toBeNull()
    expect(bar?.getAttribute('aria-label')).toBe('Bulk actions')
    expect(bar?.getAttribute('data-testid')).toBe('bar')
    expect(bar?.className).toContain('glass-strong')
    expect(bar?.className).toContain('rounded-pill')
  })

  it('announces the count through a polite live region', () => {
    mountBar(3)

    const count = document.body.querySelector('[data-testid="ui-bulk-bar-count"]')
    expect(count?.textContent?.trim()).toBe('3 selected')
    expect(count?.getAttribute('aria-live')).toBe('polite')
  })

  it('shows each action as an icon and a short label that never wraps, with the full sentence as its title', () => {
    mountBar(1)

    const button = document.body.querySelector('[data-testid="act"]')
    expect(button?.textContent?.trim()).toBe('Paid')
    expect(button?.querySelector('svg')).not.toBeNull()
    expect(button?.className).toContain('whitespace-nowrap')
    expect(button?.getAttribute('title')).toBe('Mark as paid')
  })

  it('emits the key of the chosen action', () => {
    const wrapper = mountBar(1)

    ;(document.body.querySelector('[data-testid="act-remove"]') as HTMLElement).click()

    expect(wrapper.emitted('action')).toEqual([['remove']])
  })

  it('keeps only the primary action as a button on a phone and folds the others into a menu', () => {
    const original = window.matchMedia
    window.matchMedia = ((query: string) => ({ matches: true, media: query, addEventListener: () => undefined, removeEventListener: () => undefined })) as unknown as typeof window.matchMedia
    try {
      mountBar(1)

      expect(document.body.querySelector('[data-testid="act"]')).not.toBeNull()
      expect(document.body.querySelector('[data-testid="act-remove"]')).toBeNull()
      expect(document.body.querySelector('[data-testid="ui-bulk-bar-more"]')).not.toBeNull()
    } finally {
      window.matchMedia = original
    }
  })

  it('shows the optional summary', () => {
    mountBar(1, { summary: 'Total 10' })

    expect(document.body.querySelector('[data-testid="ui-bulk-bar-summary"]')?.textContent).toContain('Total 10')
  })

  it('draws no summary when the slot is empty', () => {
    mountBar(1)

    expect(document.body.querySelector('[data-testid="ui-bulk-bar-summary"]')).toBeNull()
  })

  it('emits clear from the × button', () => {
    const wrapper = mountBar(2)

    ;(document.body.querySelector('[data-testid="ui-bulk-bar-clear"]') as HTMLElement).click()

    expect(wrapper.emitted('clear')).toHaveLength(1)
  })

  it('emits clear on Escape while shown, but not when the key was already handled', () => {
    const wrapper = mountBar(2)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }))
    expect(wrapper.emitted('clear')).toHaveLength(1)

    const handled = new KeyboardEvent('keydown', { key: 'Escape', cancelable: true })
    handled.preventDefault()
    window.dispatchEvent(handled)
    expect(wrapper.emitted('clear')).toHaveLength(1)
  })

  it('ignores Escape without a selection and other keys', () => {
    const empty = mountBar(0)
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    expect(empty.emitted('clear')).toBeUndefined()

    const shown = mountBar(1)
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }))
    expect(shown.emitted('clear')).toBeUndefined()
  })

  it('stops listening once unmounted', () => {
    const wrapper = mountBar(2)
    wrapper.unmount()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))

    expect(wrapper.emitted('clear')).toBeUndefined()
  })
})
