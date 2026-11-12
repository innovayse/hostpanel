/**
 * Unit tests for NovaFaq: one native disclosure per question read from the storefront store, and
 * nothing at all when there are no questions. The list is cut to the home limit, and the link to the
 * full FAQ page shows only when questions were left out.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { faqFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, readOf, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { HOME_FAQ_LIMIT } from '~/constants/faq'
import NovaFaq from '~/templates/nova/sections/NovaFaq.vue'
import type { FAQ } from '~/types/faq'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change the questions it serves. */
const replies: StubRouteTable = {}

/**
 * Makes the stub serve these questions.
 *
 * @param faqs The questions the public read answers with.
 */
const serve = (faqs: FAQ[]): void => {
  replies[readOf(StorefrontPath.Faqs)] = { body: faqs }
}

/**
 * Mounts the section and lets the questions load.
 *
 * @returns The wrapper.
 */
const mountSection = () =>
  mountAfterReads(stub, [StorefrontPath.Faqs], () => mountWithKit(NovaFaq))

describe('NovaFaq', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    stubLocalePath()
    serve([faqFixture(1), faqFixture(2)])
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('renders a details element per question with its question and answer', async () => {
    const items = (await mountSection()).findAll('details')

    expect(items).toHaveLength(2)
    expect(items[0]!.find('h3').text()).toBe('Example question 1?')
    expect(items[0]!.text()).toContain('Example answer 1.')
  })

  it('gives every item a test id built from its id', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="nova-faq-2"]').exists()).toBe(true)
  })

  it('lists at most the home limit and links to the full FAQ page when more exist', async () => {
    serve(Array.from({ length: HOME_FAQ_LIMIT + 2 }, (_, index) => faqFixture(index + 1)))

    const wrapper = await mountSection()

    expect(wrapper.findAll('details')).toHaveLength(HOME_FAQ_LIMIT)
    expect(wrapper.find('[data-testid="nova-faq-view-all"]').exists()).toBe(true)
  })

  it('has no view-all link when every question fits the limit', async () => {
    const wrapper = await mountSection()

    expect(wrapper.findAll('details')).toHaveLength(2)
    expect(wrapper.find('[data-testid="nova-faq-view-all"]').exists()).toBe(false)
  })

  it('renders no section while there are no questions', async () => {
    serve([])

    const wrapper = await mountSection()

    expect(wrapper.find('details').exists()).toBe(false)
    expect(wrapper.find('h2').exists()).toBe(false)
  })
})
