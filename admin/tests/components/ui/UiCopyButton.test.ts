/**
 * Unit tests for UiCopyButton: it copies exactly its value and confirms for a moment, a refused clipboard
 * leaves it as it was, and a pending reset is cancelled when the button goes away.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UiCopyButton from '@/components/ui/UiCopyButton.vue'

/**
 * Mounts a button for a recognisable value.
 *
 * @returns The mounted button.
 */
const mountButton = () =>
  mount(UiCopyButton, { props: { value: 'npm run build', label: 'Copy code', copiedLabel: 'Copied', testid: 'copy' } })

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('UiCopyButton', () => {
  it('is named by its label and carries the test id', () => {
    const button = mountButton().get('[data-testid="copy"]')

    expect(button.attributes('aria-label')).toBe('Copy code')
  })

  it('copies exactly the value and says so for a moment', async () => {
    vi.useFakeTimers()
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })
    const wrapper = mountButton()

    await wrapper.get('[data-testid="copy"]').trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith('npm run build')
    expect(wrapper.get('[data-testid="copy"]').attributes('aria-label')).toBe('Copied')

    vi.advanceTimersByTime(1500)
    await flushPromises()
    expect(wrapper.get('[data-testid="copy"]').attributes('aria-label')).toBe('Copy code')
  })

  it('stays as it was when the clipboard is refused or missing', async () => {
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) } })
    const refused = mountButton()
    await refused.get('[data-testid="copy"]').trigger('click')
    await flushPromises()
    expect(refused.get('[data-testid="copy"]').attributes('aria-label')).toBe('Copy code')

    vi.stubGlobal('navigator', {})
    const missing = mountButton()
    await missing.get('[data-testid="copy"]').trigger('click')
    await flushPromises()
    expect(missing.get('[data-testid="copy"]').attributes('aria-label')).toBe('Copy code')
  })

  it('cancels the pending reset when it goes away', async () => {
    vi.useFakeTimers()
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } })
    const wrapper = mountButton()
    await wrapper.get('[data-testid="copy"]').trigger('click')
    await flushPromises()

    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })
})
