/**
 * Unit tests for AuroraPerformance: one card per figure the operator set, the section hides without any, and the grid is the balanced one.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraPerformance from '~/templates/aurora/sections/AuroraPerformance.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { stubAuroraGlobals } from '../auroraGlobals'
import { balancedColumns } from '~/utils/balancedGrid'

describe('AuroraPerformance', () => {
  it('renders a card with the value for every figure', () => {
    stubAuroraGlobals({
      settings: { [PortalSettingKey.StatsUptime]: '99.9%', [PortalSettingKey.StatsResponseTime]: '180 ms' },
    })

    const wrapper = mountWithKit(AuroraPerformance)

    expect(wrapper.find('[data-testid="performance-value-uptime"]').text()).toBe('99.9%')
    expect(wrapper.find('[data-testid="performance-value-response_time"]').text()).toBe('180 ms')
    expect(wrapper.find('[data-testid="performance-sites_hosted"]').exists()).toBe(false)
  })

  it('renders nothing without figures', () => {
    stubAuroraGlobals()

    const wrapper = mountWithKit(AuroraPerformance)

    expect(wrapper.find('[data-testid="aurora-performance"]').exists()).toBe(false)
  })

  it('lays the cards out in the balanced grid', () => {
    stubAuroraGlobals({
      settings: {
        [PortalSettingKey.StatsUptime]: '99.9%',
        [PortalSettingKey.StatsResponseTime]: '180 ms',
        [PortalSettingKey.StatsSitesHosted]: '12,000+',
      },
    })

    const grid = mountWithKit(AuroraPerformance).find('[data-testid="aurora-performance-grid"]')

    expect(grid.attributes('data-columns')).toBe(String(balancedColumns(3)))
  })
})
