/**
 * Tests for WorldMap: countries with data are filled on the scale, others stay neutral, the
 * tooltip names the hovered or focused country, the legend shows, and a choice emits its code.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import WorldMap from '@/components/reports/WorldMap.vue'
import { MAP_ACTIVE_FILL, MAP_FILL_STEPS } from '@/lib/reports'
import type { ReportCountryRow } from '@/types/reportcountry'
import { createTestI18n } from '../../helpers/i18n'

/** Two countries with data, one the map does not draw, and one unresolved. */
const countryData: ReportCountryRow[] = [
  { country: 'AM', clientCount: 10, totalRevenue: 0 },
  { country: 'FR', clientCount: 1, totalRevenue: 0 },
  { country: 'XX', clientCount: 9, totalRevenue: 0 },
  { country: null, clientCount: 5, totalRevenue: 0 },
]

/**
 * Mounts the map.
 *
 * @returns The wrapper.
 */
const mountMap = () => mount(WorldMap, { props: { countryData }, global: { plugins: [createTestI18n()] } })

describe('WorldMap', () => {
  it('fills countries with data on the scale, the busiest darkest', () => {
    const wrapper = mountMap()

    expect(wrapper.get('[data-testid="report-map-country-AM"]').classes()).toContain(MAP_FILL_STEPS.at(-1)?.fill)
    expect(wrapper.get('[data-testid="report-map-country-FR"]').classes()).toContain(MAP_FILL_STEPS[0]?.fill)
  })

  it('leaves countries without data neutral and out of the tab order', () => {
    const wrapper = mountMap()

    expect(wrapper.find('[data-testid="report-map-country-DE"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-map-country-XX"]').exists()).toBe(false)
    expect(wrapper.findAll('path[aria-hidden="true"]').length).toBeGreaterThan(100)
  })

  it('labels a country for assistive technology with its name and clients', () => {
    const wrapper = mountMap()

    expect(wrapper.get('[data-testid="report-map-country-AM"]').attributes('aria-label')).toBe('Armenia: 10 clients')
    expect(wrapper.get('[data-testid="report-map-country-AM"]').attributes('tabindex')).toBe('0')
  })

  it('highlights a hovered country and shows its tooltip, then clears both', async () => {
    const wrapper = mountMap()
    const country = wrapper.get('[data-testid="report-map-country-AM"]')

    expect(wrapper.find('[role="tooltip"]').exists()).toBe(false)

    await country.trigger('mouseenter')
    expect(wrapper.get('[role="tooltip"]').text()).toBe('Armenia: 10 clients')
    expect(country.classes()).toContain(MAP_ACTIVE_FILL)

    await country.trigger('mouseleave')
    expect(wrapper.find('[role="tooltip"]').exists()).toBe(false)
    expect(country.classes()).not.toContain(MAP_ACTIVE_FILL)
  })

  it('uses the singular for one client', async () => {
    const wrapper = mountMap()

    await wrapper.get('[data-testid="report-map-country-FR"]').trigger('focus')

    expect(wrapper.get('[role="tooltip"]').text()).toBe('France: 1 client')
  })

  it('shows the legend', () => {
    const wrapper = mountMap()

    expect(wrapper.get('[data-testid="report-map-legend"]').text()).toContain('No data')
    expect(wrapper.get('[data-testid="report-map-legend"]').text()).toContain('Fewer')
    expect(wrapper.get('[data-testid="report-map-legend"]').text()).toContain('More')
  })

  it('emits the code of a clicked country and of one chosen with Enter or Space', async () => {
    const wrapper = mountMap()
    const country = wrapper.get('[data-testid="report-map-country-FR"]')

    await country.trigger('click')
    await country.trigger('keydown', { key: 'Enter' })
    await country.trigger('keydown', { key: ' ' })

    expect(wrapper.emitted('select-country')).toEqual([['FR'], ['FR'], ['FR']])
  })
})
