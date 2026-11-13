/**
 * Tests for WorldMap: one marker per known city, the hover caption, and the country a click emits.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import WorldMap from '@/components/reports/WorldMap.vue'
import type { ReportCityRow } from '@/types/reportcountry'
import { createTestI18n } from '../../helpers/i18n'

/** Two cities the map knows and one it does not. */
const cityData: ReportCityRow[] = [
  { city: 'Yerevan', country: 'AM', clientCount: 4 },
  { city: 'Paris', country: 'FR', clientCount: 1 },
  { city: 'Nowhere', country: 'XX', clientCount: 9 },
]

/**
 * Mounts the map.
 *
 * @returns The wrapper.
 */
const mountMap = () => mount(WorldMap, { props: { cityData }, global: { plugins: [createTestI18n()] } })

describe('WorldMap', () => {
  it('draws a marker for each city it can place and none for the others', () => {
    const wrapper = mountMap()

    expect(wrapper.find('[data-testid="report-map-city-Yerevan"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-map-city-Paris"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-map-city-Nowhere"]').exists()).toBe(false)
  })

  it('hints to hover a dot until one is hovered, then names the city, country and clients', async () => {
    const wrapper = mountMap()

    expect(wrapper.get('[data-testid="report-map-caption"]').text()).toBe('Hover a dot to see the city.')

    await wrapper.get('[data-testid="report-map-city-Yerevan"]').trigger('mouseenter')
    expect(wrapper.get('[data-testid="report-map-caption"]').text()).toBe('Yerevan, Armenia: 4 clients')

    await wrapper.get('[data-testid="report-map-city-Yerevan"]').trigger('mouseleave')
    expect(wrapper.get('[data-testid="report-map-caption"]').text()).toBe('Hover a dot to see the city.')
  })

  it('uses the singular for one client', async () => {
    const wrapper = mountMap()

    await wrapper.get('[data-testid="report-map-city-Paris"]').trigger('mouseenter')

    expect(wrapper.get('[data-testid="report-map-caption"]').text()).toContain('1 client')
    expect(wrapper.get('[data-testid="report-map-caption"]').text()).not.toContain('clients')
  })

  it('emits the country of a clicked city', async () => {
    const wrapper = mountMap()

    await wrapper.get('[data-testid="report-map-city-Paris"]').trigger('click')

    expect(wrapper.emitted('select-country')).toEqual([['FR']])
  })
})
