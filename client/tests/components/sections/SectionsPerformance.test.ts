/**
 * Unit tests for SectionsPerformance: one tile per published figure, in the order given, and no
 * section when no figure is set.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import SectionsPerformance from '~/components/sections/SectionsPerformance.vue'
import { usePerformanceStats } from '~/composables/usePerformanceStats'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ usePerformanceStats })

/** An uptime and a response time; the other figures are left unset. */
const SET_STATS: Record<string, string> = {
  [PortalSettingKey.StatsUptime]: '99.9%',
  [PortalSettingKey.StatsResponseTime]: '180 ms',
}

describe('SectionsPerformance', () => {
  it('shows each figure with its label', async () => {
    await loadPortalSettings(replies, SET_STATS)

    const wrapper = mountWithKit(SectionsPerformance, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="performance-value-uptime"]').text()).toBe('99.9%')
    expect(wrapper.find('[data-testid="performance-response_time"]').text()).toContain('hostingSections.performance.stats.response_time')
  })

  it('shows only the figures that are set', async () => {
    await loadPortalSettings(replies, SET_STATS)

    const wrapper = mountWithKit(SectionsPerformance, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="performance-sites_hosted"]').exists()).toBe(false)
  })

  it('uses the balanced grid', async () => {
    await loadPortalSettings(replies, SET_STATS)

    const wrapper = mountWithKit(SectionsPerformance, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="sections-performance-grid"]').classes()).toContain('ui-balanced-grid')
  })

  it('renders nothing when no figure is set', async () => {
    await loadPortalSettings(replies)

    const wrapper = mountWithKit(SectionsPerformance, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="sections-performance"]').exists()).toBe(false)
  })

  it('draws every icon on a flat brand tile with no accent glow', async () => {
    await loadPortalSettings(replies, SET_STATS)

    const wrapper = mountWithKit(SectionsPerformance, { components: CLASSIC_COMPONENTS })

    const tiles = wrapper.findAll('[data-testid="classic-icon-tile"]')
    expect(tiles.length).toBeGreaterThan(0)
    expect(wrapper.html()).not.toContain('--accent')
  })
})
