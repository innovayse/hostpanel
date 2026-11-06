/**
 * Unit tests for UiCopyButton: it copies its value to the clipboard, shows the copied name for a moment, and leaves the
 * button as it was when the browser blocks the clipboard.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UiCopyButton from '~/components/ui/UiCopyButton.vue'
import UiIconButton from '~/components/ui/UiIconButton.vue'

/** The clipboard write the tests stub. */
const writeText = vi.fn()

/**
 * Mounts the button with its usual props.
 *
 * @returns The wrapper.
 */
const mountButton = () =>
  mount(UiCopyButton, {
    props: { value: 'AM00 1111', label: 'Copy IBAN', copiedLabel: 'Copied', testid: 'copy-iban' },
    global: { components: { UiIconButton } }
  })

beforeEach(() => {
  vi.useFakeTimers()
  writeText.mockReset()
  vi.stubGlobal('navigator', { clipboard: { writeText } })
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('UiCopyButton', () => {
  it('names itself by the label before a copy', () => {
    const wrapper = mountButton()

    expect(wrapper.get('[data-testid="copy-iban"]').attributes('aria-label')).toBe('Copy IBAN')
  })

  it('copies the value, shows the copied name, then goes back', async () => {
    writeText.mockResolvedValue(undefined)
    const wrapper = mountButton()

    await wrapper.get('[data-testid="copy-iban"]').trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith('AM00 1111')
    expect(wrapper.get('[data-testid="copy-iban"]').attributes('aria-label')).toBe('Copied')

    vi.advanceTimersByTime(1600)
    await flushPromises()

    expect(wrapper.get('[data-testid="copy-iban"]').attributes('aria-label')).toBe('Copy IBAN')
  })

  it('stays as it was when the browser blocks the clipboard', async () => {
    writeText.mockRejectedValue(new Error('blocked'))
    const wrapper = mountButton()

    await wrapper.get('[data-testid="copy-iban"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="copy-iban"]').attributes('aria-label')).toBe('Copy IBAN')
  })
})
