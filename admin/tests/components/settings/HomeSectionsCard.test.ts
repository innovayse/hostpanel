/**
 * Tests for HomeSectionsCard: one switch per home section, each reading its own settings row,
 * the portfolio defaulting to off, and each switch saving on its own as `true` or `false`.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import HomeSectionsCard from '@/components/settings/HomeSectionsCard.vue'
import { HOME_SECTION_SETTING, HomeSection } from '@/types/homesection'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** Every section row, as the API seeds them: the portfolio off, the rest on. */
const rows: Setting[] = Object.values(HomeSection).map((section, index) => ({
  id: index + 1,
  key: HOME_SECTION_SETTING[section],
  value: section === HomeSection.Projects ? 'false' : 'true',
}))

/**
 * Finds the row id of a section.
 *
 * @param section - The section.
 * @returns The id of its settings row.
 */
const idOf = (section: HomeSection): number => rows.find((row) => row.key === HOME_SECTION_SETTING[section])?.id ?? 0

describe('HomeSectionsCard', () => {
  it('renders one switch per section with the stored state', async () => {
    const { wrapper } = await mountScreen(HomeSectionsCard, { save, settings: rows })

    for (const section of Object.values(HomeSection)) {
      const expected = section === HomeSection.Projects ? 'false' : 'true'
      expect(wrapper.get(`[data-testid="home-section-${section}"]`).attributes('aria-checked')).toBe(expected)
    }
  })

  it('turns a section off and saves the literal false', async () => {
    const { wrapper } = await mountScreen(HomeSectionsCard, { save, settings: rows })

    await wrapper.get('[data-testid="home-section-faq"]').trigger('click')
    await wrapper.get('[data-testid="home-section-faq-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[idOf(HomeSection.Faq), 'false']])
  })

  it('turns the portfolio on and saves the literal true', async () => {
    const { wrapper } = await mountScreen(HomeSectionsCard, { save, settings: rows })

    await wrapper.get('[data-testid="home-section-projects"]').trigger('click')
    await wrapper.get('[data-testid="home-section-projects-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[idOf(HomeSection.Projects), 'true']])
  })

  it('reads an empty portfolio row as off and any other empty row as on', async () => {
    const empty = rows.map((row) => ({ ...row, value: '' }))
    const { wrapper } = await mountScreen(HomeSectionsCard, { save, settings: empty })

    expect(wrapper.get('[data-testid="home-section-projects"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.get('[data-testid="home-section-cta"]').attributes('aria-checked')).toBe('true')
  })

  it('warns about a section whose row was never seeded', async () => {
    const { wrapper } = await mountScreen(HomeSectionsCard, { save, settings: rows.filter((row) => row.key !== 'portal.sections.apps') })

    const warnings = wrapper.findAll('[data-testid="setting-not-seeded"]')
    expect(warnings).toHaveLength(1)
    expect(warnings[0]?.text()).toContain('portal.sections.apps')
    expect(wrapper.find('[data-testid="home-section-apps"]').exists()).toBe(false)
  })
})
