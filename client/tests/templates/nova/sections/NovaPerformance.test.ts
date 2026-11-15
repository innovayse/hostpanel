/**
 * Unit tests for NovaPerformance: the operator figures as tiles, capability cards beneath them
 * (storage, cache and CDN hidden until their setting is filled in), and the section hiding only
 * when there is neither.
 *
 * @module tests/templates/nova/sections/NovaPerformance.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings } from '~/tests/helpers/portalSettings'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import NovaPerformance from '~/templates/nova/sections/NovaPerformance.vue'
import { PERFORMANCE_FEATURES } from '~/templates/nova/content'
import type { Feature } from '~/templates/nova/types/feature'
import { usePerformanceStats } from '~/composables/usePerformanceStats'
import { PortalSettingKey } from '~/constants/portalSettingKey'

/** The stub playing the BFF; it answers the operator's settings read. */
let stub: StubServer

/** What the stub answers, by route. */
const replies: StubRouteTable = {}

/**
 * Counts the features that do not wait on an operator setting.
 *
 * @param features A content list.
 * @returns How many are always shown.
 */
const ungated = (features: readonly Feature[]): number => features.filter(feature => !feature.requiresSetting).length

describe('NovaPerformance', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    stubLocalePath()
    vi.stubGlobal('usePerformanceStats', usePerformanceStats)
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('hides storage, cache and CDN claims until their settings exist', async () => {
    await loadPortalSettings(replies)

    expect(mountWithKit(NovaPerformance).findAll('ul > li')).toHaveLength(ungated(PERFORMANCE_FEATURES))
  })

  it('shows a tile per figure the operator has set', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.StatsUptime]: '99.9%', [PortalSettingKey.StatsResponseTime]: '180 ms' })
    const wrapper = mountWithKit(NovaPerformance)

    expect(wrapper.find('[data-testid="nova-performance-stat-uptime"]').text()).toContain('99.9%')
    expect(wrapper.find('[data-testid="nova-performance-stat-response_time"]').text()).toContain('180 ms')
    expect(wrapper.find('[data-testid="nova-performance-stat-sites_hosted"]').exists()).toBe(false)
  })

  it('omits the figures grid when no figure is set', async () => {
    await loadPortalSettings(replies)

    expect(mountWithKit(NovaPerformance).find('[data-testid="nova-performance-stats"]').exists()).toBe(false)
  })
})
