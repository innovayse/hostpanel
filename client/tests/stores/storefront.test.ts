/**
 * Tests for `stores/storefront.ts`, through the real API composable and transport against a stub
 * server.
 *
 * The store, not the sections, owns the lists, the loading flag and the failure sentence, so these
 * assertions pin the contract the sections render from, including that an empty list is a valid
 * state and a refusal leaves an empty list rather than stale data.
 *
 * @module tests/stores/storefront.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { faqFixture, locationFixture, partnerFixture, projectFixture, testimonialFixture } from '../helpers/storefrontFixtures'
import { startTransport } from '../helpers/stubTransport'
import { routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubServer } from '../helpers/stubServer'
import { useStorefrontStore } from '~/stores/storefront'
import { FaqCategory } from '~/types/faqcategory'
import { StorefrontSection } from '~/types/storefrontsection'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test, by `METHOD /path`. */
let table: StubRouteTable

/** The BFF path of each list the store reads. */
const PATH = {
  testimonials: '/api/portal/public/testimonials',
  partners: '/api/portal/public/partners',
  locations: '/api/portal/public/locations',
  projects: '/api/portal/public/projects',
  faqs: '/api/portal/public/faqs'
} as const

/** An API refusal as the BFF answers it. */
const refusal = { status: 404, body: { detail: 'Content unavailable.' } }

/**
 * Scripts the answer of one list.
 *
 * @param path - The BFF path of the list.
 * @param reply - What the stub answers.
 */
const answer = (path: string, reply: StubRouteTable[string]): void => {
  table[`GET ${path}`] = reply
}

describe('useStorefrontStore', () => {
  beforeEach(async () => {
    setActivePinia(createPinia())
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('starts with nothing, which is a valid state', () => {
    const store = useStorefrontStore()

    expect(store.testimonials).toEqual([])
    expect(store.partners).toEqual([])
    expect(store.locations).toEqual([])
    expect(store.projects).toEqual([])
    expect(store.faqs).toEqual([])
    expect(store.loading).toBe(false)
    expect(Object.values(store.errors).every(sentence => sentence === '')).toBe(true)
  })

  it('keeps the testimonials', async () => {
    answer(PATH.testimonials, { body: [testimonialFixture(1)] })
    const store = useStorefrontStore()

    const list = await store.loadTestimonials()

    expect(list).toHaveLength(1)
    expect(store.testimonials[0]?.name).toBe('Example Person 1')
    expect(stub.requests[0]).toMatchObject({ method: 'GET', url: PATH.testimonials })
  })

  it('keeps the partners', async () => {
    answer(PATH.partners, { body: [partnerFixture(1), partnerFixture(2)] })
    const store = useStorefrontStore()

    await store.loadPartners()

    expect(store.partners.map(p => p.id)).toEqual([1, 2])
  })

  it('keeps the server locations', async () => {
    answer(PATH.locations, { body: [locationFixture(1), locationFixture(2)] })
    const store = useStorefrontStore()

    await store.loadLocations()

    expect(store.locations.map(l => l.id)).toEqual([1, 2])
  })

  it('keeps the projects', async () => {
    answer(PATH.projects, { body: [projectFixture(1)] })
    const store = useStorefrontStore()

    await store.loadProjects()

    expect(store.projects[0]?.slug).toBe('example-project-1')
  })

  it('keeps the questions and picks those of one topic', async () => {
    answer(PATH.faqs, {
      body: [
        faqFixture(1),
        faqFixture(2, { category: FaqCategory.Products }),
        faqFixture(3, { category: FaqCategory.Products })
      ]
    })
    const store = useStorefrontStore()

    await store.loadFaqs()

    expect(store.faqs).toHaveLength(3)
    expect(store.faqsIn(FaqCategory.Products).map(f => f.id)).toEqual([2, 3])
    expect(store.faqsIn(FaqCategory.General).map(f => f.id)).toEqual([1])
  })

  it.each([
    ['loadTestimonials', PATH.testimonials, 'testimonials', StorefrontSection.Testimonials],
    ['loadPartners', PATH.partners, 'partners', StorefrontSection.Partners],
    ['loadLocations', PATH.locations, 'locations', StorefrontSection.Locations],
    ['loadProjects', PATH.projects, 'projects', StorefrontSection.Projects],
    ['loadFaqs', PATH.faqs, 'faqs', StorefrontSection.Faqs]
  ] as const)('%s keeps the API sentence and an empty list when refused', async (load, path, state, section) => {
    answer(path, refusal)
    const store = useStorefrontStore()

    const list = await store[load]()

    expect(list).toEqual([])
    expect(store[state]).toEqual([])
    expect(store.errors[section]).toBe('Content unavailable.')
    expect(store.loading).toBe(false)
  })

  it('drops stale rows when a later read is refused', async () => {
    answer(PATH.partners, { body: [partnerFixture(1)] })
    const store = useStorefrontStore()
    await store.loadPartners()
    expect(store.partners).toHaveLength(1)

    answer(PATH.partners, refusal)
    await store.loadPartners()

    expect(store.partners).toEqual([])
  })

  it('flags loading while a read is in flight', async () => {
    answer(PATH.faqs, { body: [] })
    const store = useStorefrontStore()

    const pending = store.loadFaqs()
    expect(store.loading).toBe(true)
    await pending

    expect(store.loading).toBe(false)
  })

  it('clears the failure sentence when the next read succeeds', async () => {
    answer(PATH.faqs, refusal)
    const store = useStorefrontStore()

    await store.loadFaqs()
    expect(store.errors[StorefrontSection.Faqs]).toBe('Content unavailable.')
    answer(PATH.faqs, { body: [faqFixture(1)] })
    await store.loadFaqs()

    expect(store.errors[StorefrontSection.Faqs]).toBe('')
  })

  it('keeps the failure of one section when another section loads successfully', async () => {
    answer(PATH.faqs, refusal)
    answer(PATH.partners, { body: [partnerFixture(1)] })
    const store = useStorefrontStore()

    await Promise.all([store.loadFaqs(), store.loadPartners()])

    expect(store.errors[StorefrontSection.Faqs]).toBe('Content unavailable.')
    expect(store.errors[StorefrontSection.Partners]).toBe('')
  })
})
