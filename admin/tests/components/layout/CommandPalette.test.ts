/**
 * Mount tests for the command palette: it opens from the store and from Ctrl K / Cmd K, lists the sidebar's
 * pages and the create actions, filters and highlights, moves with the arrow keys, opens the highlighted entry
 * on Enter, remembers it, closes on Escape and shows the empty state.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Router } from 'vue-router'
import CommandPalette from '@/components/layout/CommandPalette.vue'
import { AppRoute } from '@/lib/routes'
import { useAuthStore } from '@/stores/auth'
import { useCommandPaletteStore } from '@/stores/commandPalette'
import { createTestI18n } from '../../helpers/i18n'

/** Wrappers mounted by the current test, unmounted after it so the window listener and the DOM do not leak. */
const mounted: VueWrapper[] = []

/**
 * Lets pending renders and microtasks finish.
 *
 * @returns A promise that resolves after a macrotask.
 */
const settle = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0))

/**
 * Mounts the palette with a memory router and a signed-in viewer.
 *
 * @returns The wrapper, the palette store and the router.
 */
const mountPalette = async () => {
  const pinia = createPinia()
  setActivePinia(pinia)
  useAuthStore().user = { email: 'a@x.test', firstName: 'Ada', lastName: 'Lovelace', roles: ['Admin'] }
  const router: Router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/:pathMatch(.*)*', component: { template: '<div />' } }],
  })
  await router.push('/')
  const wrapper = mount(CommandPalette, {
    attachTo: document.body,
    global: { plugins: [createTestI18n(), pinia, router] },
  })
  mounted.push(wrapper)
  return { wrapper, palette: useCommandPaletteStore(), router }
}

/**
 * Finds an element in the teleported palette by its test id.
 *
 * @param id - The `data-testid`.
 * @returns The element, or null.
 */
const find = (id: string): HTMLElement | null => document.body.querySelector<HTMLElement>(`[data-testid="${id}"]`)

/**
 * Presses a key on the palette panel.
 *
 * @param key - The `KeyboardEvent.key`.
 */
const press = (key: string): void => {
  find('command-palette')?.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }))
}

/**
 * Types into the search field and waits for the list to update.
 *
 * @param text - The text to type.
 */
const type = async (text: string): Promise<void> => {
  const input = find('command-palette-input') as HTMLInputElement
  input.value = text
  input.dispatchEvent(new Event('input', { bubbles: true }))
  await settle()
}

describe('CommandPalette', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
    localStorage.clear()
  })

  it('is hidden until opened', async () => {
    const { palette } = await mountPalette()

    expect(find('command-palette')).toBeNull()
    palette.open()
    await settle()

    expect(find('command-palette')).not.toBeNull()
  })

  it('toggles on Ctrl K and Cmd K and ignores a bare K', async () => {
    const { palette } = await mountPalette()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }))
    expect(palette.isOpen).toBe(true)
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'K', metaKey: true }))
    expect(palette.isOpen).toBe(false)
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k' }))
    expect(palette.isOpen).toBe(false)
  })

  it('exposes the combobox and listbox roles with a placeholder', async () => {
    const { palette } = await mountPalette()
    palette.open()
    await settle()
    const input = find('command-palette-input') as HTMLInputElement

    expect(input.getAttribute('role')).toBe('combobox')
    expect(input.placeholder).toBe('Search pages and actions')
    expect(input.getAttribute('aria-controls')).toBe(find('command-palette-list')?.id)
    expect(find('command-palette-list')?.getAttribute('role')).toBe('listbox')
    expect(find('command-palette-group-actions')).not.toBeNull()
    expect(find('command-palette-group-navigation')).not.toBeNull()
    expect(find('command-palette-option-0')?.getAttribute('aria-selected')).toBe('true')
    expect(input.getAttribute('aria-activedescendant')).toBe(find('command-palette-option-0')?.id)
  })

  it('filters by the search and highlights the match', async () => {
    const { palette } = await mountPalette()
    palette.open()
    await settle()
    await type('billable')

    expect(find('command-palette-group-recent')).toBeNull()
    expect(find('command-palette-list')?.querySelector('mark')?.textContent?.toLowerCase()).toBe('billable')
    expect(find('command-palette-list')?.textContent).not.toContain('Dashboard')
  })

  it('moves the highlight with the arrow keys and wraps', async () => {
    const { palette } = await mountPalette()
    palette.open()
    await settle()

    press('ArrowDown')
    await settle()
    expect(find('command-palette-option-1')?.getAttribute('aria-selected')).toBe('true')
    press('ArrowUp')
    press('ArrowUp')
    await settle()
    expect(find('command-palette-option-0')?.getAttribute('aria-selected')).toBe('false')
  })

  it('opens the highlighted entry on Enter, closes and remembers it', async () => {
    const { palette, router } = await mountPalette()
    palette.open()
    await settle()

    press('Enter')
    await settle()

    expect(router.currentRoute.value.path).toBe(AppRoute.ClientsAdd)
    expect(palette.isOpen).toBe(false)
    expect(palette.recent[0]).toBe(AppRoute.ClientsAdd)
  })

  it('lists recent entries first while the search is empty', async () => {
    const { palette } = await mountPalette()
    palette.remember(AppRoute.Orders)
    palette.open()
    await settle()

    expect(find('command-palette-group-recent')?.textContent).toContain('Orders')
  })

  it('closes on Escape and on the phone close button', async () => {
    const { palette } = await mountPalette()
    palette.open()
    await settle()

    press('Escape')
    expect(palette.isOpen).toBe(false)

    palette.open()
    await settle()
    find('command-palette-close')?.click()
    expect(palette.isOpen).toBe(false)
  })

  it('shows the empty state when nothing matches', async () => {
    const { palette } = await mountPalette()
    palette.open()
    await settle()
    await type('zzzzzz')

    expect(find('command-palette-empty')?.textContent).toContain('zzzzzz')
    expect(find('command-palette-option-0')).toBeNull()
  })
})
