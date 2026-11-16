/**
 * Tests for `usePerformanceStats`: only set figures, in enum order.
 *
 * @module tests/composables/usePerformanceStats.test
 */

import { describe, expect, it } from 'vitest'
import { PerformanceStatKey } from '~/constants/performanceStatKey'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { usePerformanceStats } from '~/composables/usePerformanceStats'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('usePerformanceStats', () => {
  it('is empty when no figure is set, which hides the section', async () => {
    await loadPortalSettings(replies)

    expect(usePerformanceStats().value).toEqual([])
  })

  it('returns the set figures in enum order', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.StatsSupportResponse]: '15 min',
      [PortalSettingKey.StatsUptime]: '99.9%',
      [PortalSettingKey.StatsSitesHosted]: '12,000+',
    })

    expect(usePerformanceStats().value).toEqual([
      { key: PerformanceStatKey.Uptime, value: '99.9%' },
      { key: PerformanceStatKey.SitesHosted, value: '12,000+' },
      { key: PerformanceStatKey.SupportResponse, value: '15 min' },
    ])
  })
})
