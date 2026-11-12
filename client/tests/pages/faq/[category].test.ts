/**
 * Tests for the FAQ topic page against the real storefront store, API composable and transport, with
 * a stub BFF answering on a real socket (nothing of ours is mocked): the questions of one topic as an
 * accordion with the first one open, the navigation to the other topics, per-topic tags and a 404 for
 * an unknown topic.
 *
 * @module tests/pages/faq/[category].test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import FaqCategoryNav from '~/components/faq/FaqCategoryNav.vue'
import { faqFixture } from '~/tests/helpers/storefrontFixtures'
import { FaqCategory } from '~/types/faqcategory'
import { faqCategorySlug } from '~/utils/faqCategory'
import Page from '~/pages/faq/[category].vue'

/** The root element of the page. */
const ROOT = 'faq-category-page'

/** The key of the questions read in a route table. */
const FAQS_READ = `GET ${StorefrontPath.Faqs}`

/** Two general questions and two about products. */
const FAQS = [
  faqFixture(1),
  faqFixture(2),
  faqFixture(3, { category: FaqCategory.Products }),
  faqFixture(4, { category: FaqCategory.Products }),
]

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test. */
let table: StubRouteTable

describe('pages/faq/[category]', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime({ params: { category: faqCategorySlug(FaqCategory.Products) } })
    registerPageComponents({ FaqCategoryNav })
    table = { [FAQS_READ]: { body: FAQS } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the topic heading and description from the topic copy', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('h1').text()).toBe('seo.faqCategory.products.h1')
    expect(wrapper.find('[data-testid="ui-page-hero-subtitle"]').text()).toBe('seo.faqCategory.products.description')
  })

  it('puts the FAQ page and the topic in the breadcrumbs', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    const trail = wrapper.find('nav')
    expect(trail.text()).toContain('faq.title')
    expect(trail.text()).toContain('faq.categories.products')
  })

  it('lists exactly the questions of the topic, the first one open', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    const toggles = wrapper.findAll('[data-testid^="ui-accordion-toggle-"]')
    expect(toggles).toHaveLength(2)
    expect(toggles[0]?.text()).toBe('Example question 3?')
    expect(toggles[0]?.attributes('aria-expanded')).toBe('true')
    expect(toggles[1]?.attributes('aria-expanded')).toBe('false')
    expect(wrapper.text()).toContain('Example answer 3.')
    expect(wrapper.text()).not.toContain('Example question 1?')
  })

  it('shows the empty state when the topic has no questions', async () => {
    table[FAQS_READ] = { body: [] }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="faq-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="faq-accordion"]').exists()).toBe(false)
  })

  it('highlights the current topic and offers the "All" link', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="faq-category-products"]').classes()).toContain('bg-primary-700')
    expect(wrapper.find('[data-testid="faq-category-all"]').attributes('href')).toBe('/en/faq')
  })

  it('links back to the landing page', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="faq-back-to-all"]').attributes('href')).toBe('/en/faq')
  })

  it('sets the tags and the structured data of the topic only', async () => {
    await mountRenderedPage(Page, ROOT)

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({
      title: 'seo.faqCategory.products.title',
      path: '/faq/products',
    }))
    const schema = spies.injectSchema.mock.calls[0]?.[0] as { items: unknown[] }
    expect(schema.items).toHaveLength(2)
  })

  it('raises a 404 for a topic that does not exist', async () => {
    spies = stubPublicPageRuntime({ params: { category: 'astrology' } })
    registerPageComponents({ FaqCategoryNav })
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)

    const errorHandler = vi.fn()

    const wrapper = await mountPage(Page, { errorHandler })
    await vi.waitFor(() => expect(errorHandler).toHaveBeenCalled())

    expect(spies.createError).toHaveBeenCalledWith(expect.objectContaining({ statusCode: 404 }))
    expect(errorHandler).toHaveBeenCalledWith(expect.objectContaining({ statusCode: 404 }), expect.anything(), expect.anything())
    expect(wrapper.find('[data-testid="faq-category-page"]').exists()).toBe(false)
    consoleError.mockRestore()
    consoleWarn.mockRestore()
  })

  it('declares a route validator that accepts known topics and rejects others', async () => {
    const definePageMeta = vi.fn()
    vi.stubGlobal('definePageMeta', definePageMeta)

    await mountRenderedPage(Page, ROOT)

    const meta = definePageMeta.mock.calls[0]?.[0] as { validate: (to: { params: Record<string, string | string[]> }) => boolean }
    expect(meta.validate({ params: { category: 'general' } })).toBe(true)
    expect(meta.validate({ params: { category: ['products'] } })).toBe(true)
    expect(meta.validate({ params: { category: 'astrology' } })).toBe(false)
  })
})
