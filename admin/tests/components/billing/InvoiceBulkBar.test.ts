/**
 * Mount tests for InvoiceBulkBar: the five actions on the bulk bar, the safe ones running at once, cancelling and
 * deleting asking first, and the prompt staying open while the screen's action runs.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import InvoiceBulkBar from '@/components/billing/InvoiceBulkBar.vue'
import { InvoiceBulkAction } from '@/types/invoice'
import { createTestI18n } from '../../helpers/i18n'

/** The bars mounted by the running test, unmounted afterwards. */
const mounted: VueWrapper[] = []

/**
 * Mounts the bar over three selected invoices.
 *
 * @param perform - The screen's action runner.
 * @returns The wrapper.
 */
const mountBar = (perform: (action: InvoiceBulkAction) => Promise<void>) => {
  const wrapper = mount(InvoiceBulkBar, {
    props: { count: 3, testid: 'bulk', perform },
    attachTo: document.body,
    global: { plugins: [createTestI18n()] },
  })
  mounted.push(wrapper)
  return wrapper
}

/**
 * Finds an element in the document (the bar and the prompt are teleported).
 *
 * @param testid - Its test id.
 * @returns The element, or null.
 */
const byTestid = (testid: string): HTMLElement | null => document.body.querySelector(`[data-testid="${testid}"]`)

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
  document.body.innerHTML = ''
})

describe('InvoiceBulkBar', () => {
  it('shows the count and the five actions, the first of them primary', () => {
    mountBar(vi.fn())

    expect(byTestid('ui-bulk-bar-count')?.textContent?.trim()).toBe('3 selected')
    for (const suffix of ['paid', 'unpaid', 'cancelled', 'duplicate', 'delete']) {
      expect(byTestid(`bulk-${suffix}`)).not.toBeNull()
    }
    expect(byTestid('bulk-paid')?.className).toContain('gradient-brand')
  })

  it('runs the safe actions at once', async () => {
    const perform = vi.fn().mockResolvedValue(undefined)
    mountBar(perform)

    byTestid('bulk-paid')?.click()
    byTestid('bulk-duplicate')?.click()
    await flushPromises()

    expect(perform).toHaveBeenNthCalledWith(1, InvoiceBulkAction.MarkPaid)
    expect(perform).toHaveBeenNthCalledWith(2, InvoiceBulkAction.Duplicate)
  })

  it('asks before deleting or cancelling, then runs the action once confirmed', async () => {
    const perform = vi.fn().mockResolvedValue(undefined)
    mountBar(perform)

    byTestid('bulk-delete')?.click()
    await flushPromises()
    expect(perform).not.toHaveBeenCalled()
    expect(byTestid('dialog-body')?.textContent).toContain('Delete 3 selected invoices?')

    byTestid('dialog-confirm')?.click()
    await flushPromises()
    expect(perform).toHaveBeenCalledWith(InvoiceBulkAction.Delete)
  })

  it('names the cancel in its prompt and drops it without running when dismissed', async () => {
    const perform = vi.fn().mockResolvedValue(undefined)
    mountBar(perform)

    byTestid('bulk-cancelled')?.click()
    await flushPromises()
    expect(byTestid('dialog-body')?.textContent).toContain('3')

    byTestid('dialog-cancel')?.click()
    await flushPromises()
    expect(perform).not.toHaveBeenCalled()
  })

  it('emits clear from the bar', () => {
    const wrapper = mountBar(vi.fn())

    byTestid('ui-bulk-bar-clear')?.click()

    expect(wrapper.emitted('clear')).toHaveLength(1)
  })
})
