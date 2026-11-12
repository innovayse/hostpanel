/**
 * Unit tests for SectionsLocations: one card per location with a country code badge, optional
 * city and latency lines, and no section at all when there are no locations.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'
import { mountAfterReads, readOf, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import SectionsLocations from '~/components/sections/SectionsLocations.vue'
import type { ServerLocation } from '~/types/serverlocation'

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
const locationFixture = (id: number, overrides: Partial<ServerLocation> = {}): ServerLocation => ({
  id,
  countryCode: 'DE',
  name: `Example DC ${id}`,
  city: 'Example City',
  latencyNote: '20 ms',
  ...overrides,
})

/**
 * Mounts the section and lets the locations load.
 *
 * @returns The wrapper.
 */
const mountSection = () =>
  mountAfterReads(stub, [StorefrontPath.Locations], () => mountWithKit(SectionsLocations, { components: CLASSIC_COMPONENTS }))

describe('SectionsLocations', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    serve([locationFixture(1), locationFixture(2, { countryCode: 'US', city: null, latencyNote: null })])
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('asks the store for the locations', async () => {
    await mountSection()

    expect(requestsTo(stub.requests, StorefrontPath.Locations)).toHaveLength(1)
  })

  it('shows the heading labels', async () => {
    const wrapper = await mountSection()

    expect(wrapper.text()).toContain('hostingSections.locations.title')
  })

  it('shows the country as a code badge, never an emoji flag', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="location-code-1"]').text()).toBe('DE')
    expect(wrapper.find('[data-testid="location-code-2"]').text()).toBe('US')
  })

  it('shows city and latency only when present', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="location-city-1"]').text()).toBe('Example City')
    expect(wrapper.find('[data-testid="location-latency-1"]').text()).toBe('20 ms')
    expect(wrapper.find('[data-testid="location-city-2"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="location-latency-2"]').exists()).toBe(false)
  })

  it('lays the cards out in the balanced grid', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="sections-locations-grid"]').classes()).toContain('ui-balanced-grid')
  })

  it('renders nothing when there are no locations', async () => {
    serve([])

    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="sections-locations"]').exists()).toBe(false)
  })

  it('never caps the grid width: the shared container sets it for any number of locations', async () => {
    for (const count of [1, 2, 3]) {
      serve(Array.from({ length: count }, (_, index) => locationFixture(index + 1)))
      const wrapper = await mountSection()
      const classes = wrapper.find('[data-testid="sections-locations-grid"]').classes()

      expect(classes.some(name => name.startsWith('max-w-'))).toBe(false)
    }
  })
})
