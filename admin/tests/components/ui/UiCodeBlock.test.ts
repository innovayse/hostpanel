/**
 * Unit tests for UiCodeBlock: the text is shown verbatim, copied exactly, and the button tells the
 * reader it worked for a moment; a refused clipboard leaves the text in place, and a pending reset
 * is cancelled when the block goes away.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UiCodeBlock from '@/components/ui/UiCodeBlock.vue'

/**
 * Mounts a block with a recognisable command.
 *
 * @returns The mounted block.
 */
const mountBlock = () =>
  mount(UiCodeBlock, {
    props: { code: 'sudo ./install.sh --provider cwp "ssh-ed25519 AAAA x"', copyLabel: 'Copy', copiedLabel: 'Copied' },
    attrs: { 'data-testid': 'cmd' },
  })

describe('UiCodeBlock', () => {
  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('shows the text verbatim with the test id on the box', () => {
    const wrapper = mountBlock()

    expect(wrapper.get('[data-testid="cmd"]').text()).toBe('sudo ./install.sh --provider cwp "ssh-ed25519 AAAA x"')
    expect(wrapper.find('[data-testid="cmd-copy"]').exists()).toBe(true)
  })

  it('copies exactly the text and confirms', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })
    const wrapper = mountBlock()

    await wrapper.get('[data-testid="cmd-copy"]').trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith('sudo ./install.sh --provider cwp "ssh-ed25519 AAAA x"')
    expect(wrapper.get('[data-testid="cmd-copy"]').text()).toBe('Copied')
  })

  it('stays on the copy label when the clipboard is refused', async () => {
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) } })
    const wrapper = mountBlock()

    await wrapper.get('[data-testid="cmd-copy"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="cmd-copy"]').text()).toBe('Copy')
  })

  it('returns to the copy label after a moment', async () => {
    vi.useFakeTimers()
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } })
    const wrapper = mountBlock()

    await wrapper.get('[data-testid="cmd-copy"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-testid="cmd-copy"]').text()).toBe('Copied')

    await vi.advanceTimersByTimeAsync(2000)
    expect(wrapper.get('[data-testid="cmd-copy"]').text()).toBe('Copy')
  })

  it('cancels the pending reset when it is unmounted', async () => {
    vi.useFakeTimers()
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } })
    const wrapper = mountBlock()

    await wrapper.get('[data-testid="cmd-copy"]').trigger('click')
    await flushPromises()
    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })

  it('gives the button no test id when the box has none', () => {
    const wrapper = mount(UiCodeBlock, { props: { code: 'x', copyLabel: 'Copy', copiedLabel: 'Copied' } })

    expect(wrapper.find('button').attributes('data-testid')).toBeUndefined()
  })
})
