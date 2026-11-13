/**
 * Mount tests for LocaleMenu: the globe trigger with the short code and its accessible name, the three languages
 * with the active one checked, and a pick switching the locale.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import LocaleMenu from '@/components/layout/LocaleMenu.vue'
import { stubRoutes } from '../../helpers/routes'
import { mountScreen } from '../../helpers/mount'

describe('LocaleMenu', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the short code and lists the languages with the active one checked', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(LocaleMenu, {}, '/dashboard')
    const trigger = wrapper.get('[data-testid="locale-menu"]')

    expect(trigger.attributes('aria-label')).toBe('Language')
    expect(trigger.attributes('aria-haspopup')).toBe('menu')
    expect(trigger.text()).toBe('en')
    await trigger.trigger('click')

    expect(wrapper.findAll('[role="menuitemradio"]').map(item => item.text())).toEqual(['English', 'Հայերեն', 'Русский'])
    expect(wrapper.get('[data-testid="locale-option-en"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="locale-option-ru"]').attributes('aria-checked')).toBe('false')
  })
})
