/**
 * Tests for the FAQ landing page against the real storefront store, API composable and transport,
 * with a stub BFF answering on a real socket (nothing of ours is mocked): the hero with a link to
 * every topic, the full list of questions, and the structured data for search engines.
 *
 * @module tests/pages/faq/index.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PUBLIC_PAGE_BASE_URL, canonicalOf, publishedSchemas, registerPageComponents, seoTags, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'
import FaqCategoryNav from '~/components/faq/FaqCategoryNav.vue'
import SectionsFAQ from '~/components/sections/SectionsFAQ.vue'
import { useTemplate } from '~/composables/useTemplate'
import { faqFixture } from '~/tests/helpers/storefrontFixtures'
import Page from '~/pages/faq/index.vue'

/** The root element of the page. */
const ROOT = 'faq-page'

/** The key of the questions read in a route table. */
const FAQS_READ = `GET ${StorefrontPath.Faqs}`

/** Two questions the API serves. */
const FAQS = [faqFixture(1), faqFixture(2)]

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test. */
let table: StubRouteTable

describe('pages/faq/index', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime()
    vi.stubGlobal('useTemplate', useTemplate)
    registerPageComponents({ ...CLASSIC_COMPONENTS, FaqCategoryNav, SectionsFAQ })
    table = { [FAQS_READ]: { body: FAQS } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the hero with the badge, the title and the subtitle', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('h1').text()).toBe('faq.title')
    expect(wrapper.find('[data-testid="ui-page-hero-badge"]').text()).toBe('faq.badge')
    expect(wrapper.find('[data-testid="ui-page-hero-subtitle"]').text()).toBe('faq.subtitle')
  })

  it('links to every topic page but has no "All" link', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    for (const topic of ['general', 'products']) {
      expect(wrapper.find(`[data-testid="faq-category-${topic}"]`).attributes('href')).toBe(`/en/faq/${topic}`)
    }
    expect(wrapper.find('[data-testid="faq-category-all"]').exists()).toBe(false)
  })

  it('lists every question without its own header or tabs', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    await waitUntil(() => expect(wrapper.find('[data-testid="sections-faq"]').exists()).toBe(true))
    const list = wrapper.get('[data-testid="sections-faq"]')
    // The full page never gets the home preview limit.
    expect(list.findAll('[data-testid^="ui-accordion-toggle-"]')).toHaveLength(FAQS.length)
    expect(list.text()).not.toContain('faq.title')
    expect(list.find('[data-testid^="faq-category-"]').exists()).toBe(false)
  })

  it('publishes one structured-data question per FAQ item', async () => {
    await mountRenderedPage(Page, ROOT)

    const [schema, ...others] = publishedSchemas(spies)
    expect(others).toEqual([])
    const questions = schema?.mainEntity as Array<{ name: string }>
    expect(questions).toHaveLength(FAQS.length)
    expect(questions[0]?.name).toBe('Example question 1?')
  })

  it('publishes no questions in the structured data when there are none', async () => {
    table[FAQS_READ] = { body: [] }

    await mountRenderedPage(Page, ROOT)

    expect(publishedSchemas(spies)[0]?.mainEntity).toEqual([])
  })

  it('sets the page tags for /faq', async () => {
    await mountRenderedPage(Page, ROOT)

    expect(seoTags(spies)).toMatchObject({ title: 'seo.faq.title' })
    expect(canonicalOf(spies)).toBe(`${PUBLIC_PAGE_BASE_URL}/faq`)
  })
})
