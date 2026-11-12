/**
 * Unit tests for AuroraLocations: the locations come from the storefront store, optional lines show only when set, and the section hides when there are none.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { mountAfterReads, readOf, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import AuroraLocations from '~/templates/aurora/sections/AuroraLocations.vue'
import type { ServerLocation } from '~/types/serverlocation'
import { stubAuroraGlobals } from '../auroraGlobals'
import { balancedColumns } from '~/utils/balancedGrid'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change the locations it serves. */
const replies: StubRouteTable = {}

/**
 * Makes the stub serve these locations.
 *
 * @param locations The locations the public read answers with.
 */
const serve = (locations: ServerLocation[]): void => {
  replies[readOf(StorefrontPath.Locations)] = { body: locations }
}

/**
 * Builds a location.
 *
 * @param id The location id.
 * @param overrides Fields that differ from the defaults.
 * @returns The location.
 */
const location = (id: number, overrides: Partial<ServerLocation> = {}): ServerLocation => ({
  id,
  countryCode: 'DE',
  name: `Example DC ${id}`,
  city: null,
  latencyNote: null,
  ...overrides,
})

/**
 * Mounts the section and lets the content load.
 *
 * @returns The wrapper.
 */
const mountSection = async () => {
  stubAuroraGlobals()
  return mountAfterReads(stub, [StorefrontPath.Locations], () => mountWithKit(AuroraLocations))
}

describe('AuroraLocations', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    serve([location(1, { city: 'Frankfurt', latencyNote: '20 ms to Europe' }), location(2)])
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('shows the country code, name, city and latency note of a location', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="location-code-1"]').text()).toBe('DE')
    expect(wrapper.find('[data-testid="location-1"]').text()).toContain('Example DC 1')
    expect(wrapper.find('[data-testid="location-city-1"]').text()).toBe('Frankfurt')
    expect(wrapper.find('[data-testid="location-latency-1"]').text()).toBe('20 ms to Europe')
  })

  it('leaves out the city and latency lines that are not set', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="location-city-2"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="location-latency-2"]').exists()).toBe(false)
  })

  it('renders nothing while there are no locations', async () => {
    serve([])

    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="aurora-locations"]').exists()).toBe(false)
  })

  it('lays the cards out in the balanced grid', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="aurora-locations-grid"]').attributes('data-columns')).toBe(String(balancedColumns(2)))
  })
})
