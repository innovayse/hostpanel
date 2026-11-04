/**
 * Tests for PortalAppearanceCard: the template dropdown, the colour-mode radios, the visitor
 * switch, and the warnings for unseeded rows.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import PortalAppearanceCard from '@/components/settings/PortalAppearanceCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'
import { pickOption, selectText } from '../../helpers/select'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** The three appearance rows, as the API seeds them. */
const rows: Setting[] = [
  { id: 1, key: 'portal.template', value: 'aurora' },
  { id: 2, key: 'portal.theme.default', value: 'dark' },
  { id: 3, key: 'portal.theme.user_toggle', value: 'true' },
]

describe('PortalAppearanceCard', () => {
  it('shows the stored template with its hint', async () => {
    const { wrapper } = await mountScreen(PortalAppearanceCard, { save, settings: rows })

    expect(selectText(wrapper, 'portal-template')).toBe('Aurora')
    expect(wrapper.text()).toContain('Glow-backed design')
  })

  it('falls back to the classic template when the row is empty', async () => {
    const empty = rows.map((r) => (r.key === 'portal.template' ? { ...r, value: '' } : r))
    const { wrapper } = await mountScreen(PortalAppearanceCard, { save, settings: empty })

    expect(selectText(wrapper, 'portal-template')).toBe('Classic')
    expect(wrapper.text()).toContain('the original storefront design')
  })

  it('saves a different template', async () => {
    const { wrapper } = await mountScreen(PortalAppearanceCard, { save, settings: rows })

    await pickOption(wrapper, 'portal-template', 'Nova')
    await wrapper.get('[data-testid="portal-template-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[1, 'nova']])
  })

  it('saves a different colour mode from the radios', async () => {
    const { wrapper } = await mountScreen(PortalAppearanceCard, { save, settings: rows })
    expect((wrapper.get('[data-testid="portal-theme-dark"]').element as HTMLInputElement).checked).toBe(true)

    await wrapper.get('[data-testid="portal-theme-light"]').setValue(true)
    await wrapper.get('[data-testid="portal-theme-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[2, 'light']])
  })

  it('turns the visitor switch off and saves the literal false', async () => {
    const { wrapper } = await mountScreen(PortalAppearanceCard, { save, settings: rows })
    const toggle = wrapper.get('[data-testid="portal-user-toggle"]')
    expect(toggle.attributes('aria-checked')).toBe('true')

    await toggle.trigger('click')
    await wrapper.get('[data-testid="portal-user-toggle-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[3, 'false']])
  })

  it('names every unseeded key and hides its control', async () => {
    const { wrapper } = await mountScreen(PortalAppearanceCard, { save, settings: [rows[0] as Setting] })

    const warnings = wrapper.findAll('[data-testid="setting-not-seeded"]')
    expect(warnings).toHaveLength(2)
    expect(warnings[0]?.text()).toContain('portal.theme.default')
    expect(warnings[1]?.text()).toContain('portal.theme.user_toggle')
    expect(wrapper.find('[data-testid="portal-user-toggle"]').exists()).toBe(false)
  })
})
