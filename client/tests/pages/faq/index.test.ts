/**
 * Tests for the FAQ landing page against the real storefront store, API composable and transport,
 * with a stub BFF answering on a real socket (nothing of ours is mocked): the hero with a link to
 * every topic, the full list of questions, and the structured data for search engines.
 *
 * @module tests/pages/faq/index.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import FaqCategoryNav from '~/components/faq/FaqCategoryNav.vue'
import { faqFixture } from '~/tests/helpers/storefrontFixtures'
import Page from '~/pages/faq/index.vue'

/** The root element of the page. */
const ROOT = 'faq-page'

/** The key of the questions read in a route table. */
const FAQS_READ = `GET ${StorefrontPath.Faqs}`

/** Two questions the API serves. */
const FAQS = [faqFixture(1), faqFixture(2)]

/** Stands in for the FAQ list section, recording the props it was given. */
const SectionsFaqStub = defineComponent({
  name: 'SectionsFAQ',
  props: {
    showHeader: { type: Boolean, default: true },
    showCategories: { type: Boolean, default: true },
    limit: { type: Number, default: undefined },
  },
  setup: props => () => h('div', {
    'data-testid': 'sections-faq',
    'data-header': String(props.showHeader),
    'data-categories': String(props.showCategories),
    'data-limit': String(props.limit),
  }),
})

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test. */
let table: StubRouteTable

describe('pages/faq/index', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime()
    registerPageComponents({ FaqCategoryNav, SectionsFAQ: SectionsFaqStub })
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

    const list = wrapper.find('[data-testid="sections-faq"]')
    expect(list.attributes('data-header')).toBe('false')
    expect(list.attributes('data-categories')).toBe('false')
    // The full page never gets the home preview limit.
    expect(list.attributes('data-limit')).toBe('undefined')
  })

  it('publishes one structured-data question per FAQ item', async () => {
    await mountRenderedPage(Page, ROOT)

    expect(spies.injectSchema).toHaveBeenCalledTimes(1)
    const schema = spies.injectSchema.mock.calls[0]?.[0] as { items: Array<{ question: string }> }
    expect(schema.items).toHaveLength(FAQS.length)
    expect(schema.items[0]?.question).toBe('Example question 1?')
  })

  it('publishes no questions in the structured data when there are none', async () => {
    table[FAQS_READ] = { body: [] }

    await mountRenderedPage(Page, ROOT)

    const schema = spies.injectSchema.mock.calls[0]?.[0] as { items: unknown[] }
    expect(schema.items).toEqual([])
  })

  it('sets the page tags for /faq', async () => {
    await mountRenderedPage(Page, ROOT)

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ title: 'seo.faq.title', path: '/faq' }))
  })
})
