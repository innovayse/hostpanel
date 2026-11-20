/**
 * Tests for `useSchemaOrg`: the JSON-LD documents the public site publishes and their injection
 * into the document head.
 *
 * @module tests/composables/useSchemaOrg.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { useContactDetails } from '~/composables/useContactDetails'
import { useSchemaOrg } from '~/composables/useSchemaOrg'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** The base URL of the stood-in site. */
const BASE = 'https://host.test'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useSiteIdentity, useContactDetails })

/** Spy for `useHead`, to read the scripts that were injected. */
const useHead = vi.fn()

/**
 * Loads the operator's settings and stubs the Nuxt globals `useSchemaOrg` reads. The operator is called
 * Acme Hosting unless the settings say otherwise.
 *
 * @param settings Operator setting key to value.
 * @param baseUrl The configured public base URL; empty selects the built-in fallback.
 * @returns Resolves once the settings are loaded.
 */
const stubRuntime = async (settings: Record<string, string> = {}, baseUrl = BASE): Promise<void> => {
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { baseUrl } }))
  vi.stubGlobal('useHead', useHead)
  await loadPortalSettings(replies, {
    [PortalSettingKey.SiteName]: 'Acme Hosting',
    ...settings,
  })
}

describe('useSchemaOrg', () => {
  beforeEach(() => {
    useHead.mockClear()
  })

  it('exposes the configured base URL and invents no host when none is set', async () => {
    await stubRuntime()
    expect(useSchemaOrg().baseUrl).toBe(BASE)

    await stubRuntime({}, '')
    expect(useSchemaOrg().baseUrl).toBe('')
  })

  it('describes a product family as a Product with one priced Offer per plan', async () => {
    await stubRuntime()
    const { productFamilySchema } = useSchemaOrg()

    const schema = productFamilySchema({
      name: 'Web Hosting',
      description: 'Fast hosting',
      url: '/products/web-hosting',
      offers: [
        { name: 'Starter', price: 4.99, priceCurrency: 'USD', billingDuration: 'P1M', url: '/products/web-hosting' },
        { name: 'Pro', price: 99, priceCurrency: 'USD', billingDuration: 'P1Y', url: `${BASE}/products/web-hosting` },
      ],
    })

    expect(schema).toMatchObject({ '@type': 'Product', name: 'Web Hosting', url: `${BASE}/products/web-hosting`, brand: { '@id': `${BASE}/#organization` } })
    expect(schema.offers).toHaveLength(2)
    expect(schema.offers?.[0]).toMatchObject({
      '@type': 'Offer',
      name: 'Starter',
      price: 4.99,
      priceCurrency: 'USD',
      url: `${BASE}/products/web-hosting`,
      priceSpecification: { '@type': 'UnitPriceSpecification', price: 4.99, priceCurrency: 'USD', billingDuration: 'P1M' },
    })
    expect(schema.offers?.[1]?.priceSpecification.billingDuration).toBe('P1Y')
  })

  it('publishes no offers for a family with no priced plan', async () => {
    await stubRuntime()
    const schema = useSchemaOrg().productFamilySchema({ name: 'Web Hosting', description: 'd', url: '/p', offers: [] })

    expect(schema).not.toHaveProperty('offers')
  })

  it('publishes no offer, rating or served area the operator never stated', async () => {
    await stubRuntime()
    const { productSchema, serviceSchema } = useSchemaOrg()

    const product = productSchema({ name: 'P', description: 'd', url: '/p' })
    const service = serviceSchema({ name: 'S', description: 'd', url: '/s', serviceType: 'Hosting' })

    expect(product).not.toHaveProperty('offers')
    expect(product).not.toHaveProperty('aggregateRating')
    expect(service).not.toHaveProperty('areaServed')
  })

  it('names the organisation after the operator and uses their tagline when set', async () => {
    await stubRuntime({ [PortalSettingKey.SiteTagline]: 'Calm hosting' })

    const schema = useSchemaOrg().organizationSchema()

    expect(schema['@type']).toBe('Organization')
    expect(schema.name).toBe('Acme Hosting')
    expect(schema.description).toBe('Calm hosting')
    expect(schema['@id']).toBe(`${BASE}/#organization`)
  })

  it('omits the description when the operator set no tagline', async () => {
    await stubRuntime()

    const schema = useSchemaOrg()

    expect(schema.organizationSchema()).not.toHaveProperty('description')
    expect(schema.websiteSchema()).not.toHaveProperty('description')
  })

  it('builds sameAs and the contact fields from the operator settings', async () => {
    await stubRuntime({
      [PortalSettingKey.ContactEmail]: 'hello@acme.test',
      [PortalSettingKey.ContactPhone]: '+1 555 0100',
      [PortalSettingKey.ContactTelegram]: '@acme',
      [PortalSettingKey.ContactAddress]: '1 Example Street',
    })

    const schema = useSchemaOrg().organizationSchema()

    expect(schema.sameAs).toEqual(['https://t.me/acme'])
    expect(schema.email).toBe('hello@acme.test')
    expect(schema.contactPoint?.telephone).toBe('+1 555 0100')
    expect(schema.address?.streetAddress).toBe('1 Example Street')
  })

  it('omits every operator field that is not set and publishes no invented facts', async () => {
    await stubRuntime()

    const schema = useSchemaOrg().organizationSchema() as Record<string, unknown>
    const business = useSchemaOrg().localBusinessSchema() as Record<string, unknown>

    expect(schema.sameAs).toEqual([])
    for (const key of ['email', 'contactPoint', 'address', 'foundingDate']) {
      expect(schema).not.toHaveProperty(key)
    }
    for (const key of ['telephone', 'email', 'address', 'geo', 'aggregateRating', 'openingHoursSpecification']) {
      expect(business).not.toHaveProperty(key)
    }
  })

  it('links the website schema to the organisation and lists every language', async () => {
    await stubRuntime()

    const schema = useSchemaOrg().websiteSchema()

    expect(schema['@type']).toBe('WebSite')
    expect(schema.publisher['@id']).toBe(`${BASE}/#organization`)
    expect(schema.inLanguage).toEqual(['en', 'ru', 'hy', 'de', 'fr'])
  })

  it('names the local business after the operator', async () => {
    await stubRuntime()

    const schema = useSchemaOrg().localBusinessSchema()

    expect(schema['@type']).toBe('LocalBusiness')
    expect(schema.name).toBe('Acme Hosting')
  })

  it('makes a relative service URL absolute and keeps an absolute one', async () => {
    await stubRuntime()
    const { serviceSchema } = useSchemaOrg()
    const service = { name: 'S', description: 'D', serviceType: 'Hosting' }

    expect(serviceSchema({ ...service, url: '/services/x' }).url).toBe(`${BASE}/services/x`)
    expect(serviceSchema({ ...service, url: 'https://other.test/x' }).url).toBe('https://other.test/x')
  })

  it('makes a relative product URL absolute and keeps an absolute one', async () => {
    await stubRuntime()
    const { productSchema } = useSchemaOrg()
    const product = { name: 'P', description: 'D' }

    expect(productSchema({ ...product, url: '/p' }).url).toBe(`${BASE}/p`)
    expect(productSchema({ ...product, url: 'https://other.test/p' }).url).toBe('https://other.test/p')
  })

  it('numbers breadcrumb steps from one and absolutises their URLs', async () => {
    await stubRuntime()

    const schema = useSchemaOrg().breadcrumbSchema([
      { name: 'Home', url: '/' },
      { name: 'Hosting', url: 'https://other.test/h' },
    ])

    expect(schema.itemListElement).toEqual([
      { '@type': 'ListItem', position: 1, name: 'Home', item: `${BASE}/` },
      { '@type': 'ListItem', position: 2, name: 'Hosting', item: 'https://other.test/h' },
    ])
  })

  it('maps FAQ entries to questions with accepted answers', async () => {
    await stubRuntime()

    const schema = useSchemaOrg().faqSchema([{ question: 'Q?', answer: 'A.' }])

    expect(schema['@type']).toBe('FAQPage')
    expect(schema.mainEntity).toEqual([
      { '@type': 'Question', name: 'Q?', acceptedAnswer: { '@type': 'Answer', text: 'A.' } },
    ])
  })

  it('describes an article with an absolute URL and only the fields it was given', async () => {
    await stubRuntime()
    const { articleSchema } = useSchemaOrg()

    expect(articleSchema({ headline: 'H', url: '/announcements/3', description: 'D', datePublished: '2026-03-15T12:00:00Z' })).toEqual({
      '@context': 'https://schema.org',
      '@type': 'Article',
      headline: 'H',
      mainEntityOfPage: `${BASE}/announcements/3`,
      url: `${BASE}/announcements/3`,
      publisher: { '@id': `${BASE}/#organization` },
      description: 'D',
      datePublished: '2026-03-15T12:00:00Z',
    })
    const bare = articleSchema({ headline: 'H', url: 'https://other.test/a' })
    expect(bare).not.toHaveProperty('description')
    expect(bare).not.toHaveProperty('datePublished')
    expect(bare.url).toBe('https://other.test/a')
  })

  it('reads a getter again each time the head renders', async () => {
    await stubRuntime()
    /** The schema list the getter reads; the test changes it between renders. */
    const schema = ref<object[]>([])

    useSchemaOrg().injectSchema(() => schema.value)
    const render = useHead.mock.calls[0]![0] as () => { script: unknown[] }
    expect(render().script).toEqual([])

    schema.value = [{ '@type': 'A' }]
    expect(render().script).toHaveLength(1)
  })

  it('injects one JSON-LD script per schema into the head', async () => {
    await stubRuntime()
    const { injectSchema } = useSchemaOrg()
    const first = { '@type': 'A' }
    const second = { '@type': 'B' }

    injectSchema([first, second])

    /** The script entries the head renders for the injected schemas. */
    const scripts = (useHead.mock.calls[0]![0] as () => { script: Array<{ type: string, innerHTML: string }> })().script
    expect(scripts).toEqual([
      { type: 'application/ld+json', innerHTML: JSON.stringify(first) },
      { type: 'application/ld+json', innerHTML: JSON.stringify(second) },
    ])
  })

  it('accepts a single schema object as well as a list', async () => {
    await stubRuntime()

    useSchemaOrg().injectSchema({ '@type': 'A' })

    expect((useHead.mock.calls[0]![0] as () => { script: unknown[] })().script).toHaveLength(1)
  })
})
