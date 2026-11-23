/**
 * Unit tests for UiCommandDialog: it is teleported and only present while open, names itself, passes the caller's
 * attributes and key handler to the panel, and closes on Escape and on a backdrop click.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiCommandDialog from '@/components/ui/UiCommandDialog.vue'

/** Wrappers mounted by the current test, unmounted after it so the scroll lock and the DOM do not leak. */
const mounted: VueWrapper[] = []

/**
 * Mounts the dialog attached to the document.
 *
 * @param props - Props of the dialog.
 * @param attrs - Attributes and listeners that must reach the panel.
 * @returns The wrapper.
 */
const mountDialog = (props: { open: boolean }, attrs: Record<string, unknown> = {}): VueWrapper => {
  const wrapper = mount(UiCommandDialog, {
    props: { label: 'Search', ...props },
    attrs,
    slots: { default: '<input data-testid="inside" placeholder="x" />' },
    attachTo: document.body,
  })
  mounted.push(wrapper)
  return wrapper
}

/**
 * Finds a teleported element by its test id.
 *
 * @param id - The `data-testid`.
 * @returns The element, or null.
 */
const find = (id: string): HTMLElement | null => document.body.querySelector<HTMLElement>(`[data-testid="${id}"]`)

describe('UiCommandDialog', () => {
  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
  })

  it('draws nothing while closed', () => {
    mountDialog({ open: false })

    expect(find('command-dialog-backdrop')).toBeNull()
  })

  it('is a named modal dialog whose panel carries the caller attributes and key handler', () => {
    const onKeydown = vi.fn()
    mountDialog({ open: true }, { 'data-testid': 'palette', onKeydown })
    const panel = find('palette')

    expect(panel?.getAttribute('role')).toBe('dialog')
    expect(panel?.getAttribute('aria-modal')).toBe('true')
    expect(panel?.getAttribute('aria-label')).toBe('Search')
    expect(panel?.contains(find('inside'))).toBe(true)

    panel?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }))
    expect(onKeydown).toHaveBeenCalledTimes(1)
  })

  it('asks to close on Escape and on a backdrop click, but not on a click inside the panel', () => {
    const wrapper = mountDialog({ open: true }, { 'data-testid': 'palette' })

    find('palette')?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))
    expect(wrapper.emitted('close')).toHaveLength(1)

    find('palette')?.click()
    expect(wrapper.emitted('close')).toHaveLength(1)
    find('command-dialog-backdrop')?.click()
    expect(wrapper.emitted('close')).toHaveLength(2)
  })
})
