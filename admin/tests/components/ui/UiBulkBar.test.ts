/**
 * Unit tests for UiBulkBar: shown only with a selection, toolbar semantics, the count, the slots and clearing.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiBulkBar from '@/components/ui/UiBulkBar.vue'
import { createTestI18n } from '../../helpers/i18n'

/** The bars mounted by the running test, unmounted afterwards so none keeps listening for Escape. */
const mounted: VueWrapper[] = []

/**
 * Mounts the bar with its i18n plugin; the teleported content is reachable in the document.
 *
 * @param count - How many rows are selected.
 * @param slots - Optional slots.
 * @returns The wrapper.
 */
const mountBar = (count: number, slots: Record<string, string> = { default: '<button type="button" data-testid="act">Go</button>' }) => {
  const wrapper = mount(UiBulkBar, {
    props: { count },
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

  it('shows the action buttons and the optional summary', () => {
    mountBar(1, { default: '<button type="button" data-testid="act">Go</button>', summary: 'Total 10' })

    expect(document.body.querySelector('[data-testid="act"]')).not.toBeNull()
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
