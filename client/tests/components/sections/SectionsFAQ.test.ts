/**
 * Unit tests for SectionsFAQ: the questions read from the storefront store, the category tabs and
 * filtering, the preview limit, the header, the contact card, and the section hiding itself when
 * there are no questions.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { faqFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, readOf, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import SectionsFAQ from '~/components/sections/SectionsFAQ.vue'
import MarketingSectionHeader from '~/components/marketing/MarketingSectionHeader.vue'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'
import { TemplateName } from '~/types/templatename'
import { FaqCategory } from '~/types/faqcategory'
import type { FAQ } from '~/types/faq'
import { faqCategorySlug } from '~/utils/faqCategory'

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

/** The template the stubbed composable reports. */
const activeTemplate = ref<TemplateName>(TemplateName.Aurora)

/** Three general questions and two about products. */
const FAQS = [
  faqFixture(1),
  faqFixture(2),
  faqFixture(3),
  faqFixture(4, { category: FaqCategory.Products }),
  faqFixture(5, { category: FaqCategory.Products }),
]

/**
 * Mounts the section and lets the questions load.
 *
 * @param props Props of the section.
 * @returns The wrapper.
 */
const mountFaq = (props: Record<string, unknown> = {}) =>
  mountAfterReads(stub, [StorefrontPath.Faqs], () =>
    mountWithKit(SectionsFAQ, { props, components: { MarketingSectionHeader, ...CLASSIC_COMPONENTS } }))

/**
 * Counts the questions on screen.
 *
 * @param wrapper The mounted section.
 * @returns How many accordion headers there are.
 */
const questionCount = (wrapper: Awaited<ReturnType<typeof mountFaq>>): number => wrapper.findAll('[data-testid^="ui-accordion-toggle-"]').length

describe('SectionsFAQ', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    stubPublicPageRuntime()
    vi.stubGlobal('useTemplate', () => ({ name: activeTemplate }))
    serve(FAQS)
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('shows every question under the "all" tab at first, with every answer closed', async () => {
    const wrapper = await mountFaq()

    expect(questionCount(wrapper)).toBe(FAQS.length)
    expect(wrapper.text()).toContain('Example question 1?')
    expect(wrapper.find('[data-testid="ui-accordion-toggle-0"]').attributes('aria-expanded')).toBe('false')
  })

  it('offers an all tab and one tab per topic', async () => {
    const wrapper = await mountFaq()

    expect(wrapper.findAll('[data-testid^="faq-category-"]')).toHaveLength(Object.values(FaqCategory).length + 1)
  })

  it('filters to a topic and opens its first question', async () => {
    const wrapper = await mountFaq()

    await wrapper.find(`[data-testid="faq-category-${faqCategorySlug(FaqCategory.Products)}"]`).trigger('click')

    expect(questionCount(wrapper)).toBe(2)
    expect(wrapper.text()).toContain('Example question 4?')
    expect(wrapper.find('[data-testid="ui-accordion-toggle-0"]').attributes('aria-expanded')).toBe('true')
  })

  it('cuts the list to the limit and offers the full FAQ page', async () => {
    const wrapper = await mountFaq({ limit: 3 })

    expect(questionCount(wrapper)).toBe(3)
    expect(wrapper.find('[data-testid="faq-view-all"]').attributes('href')).toBe('/en/faq')
  })

  it('has no view-all link when the limit hides nothing', async () => {
    const wrapper = await mountFaq({ limit: FAQS.length })

    expect(questionCount(wrapper)).toBe(FAQS.length)
    expect(wrapper.find('[data-testid="faq-view-all"]').exists()).toBe(false)
  })

  it('has no view-all link without a limit', async () => {
    expect((await mountFaq()).find('[data-testid="faq-view-all"]').exists()).toBe(false)
  })

  it('hides the tabs and the header when asked to', async () => {
    const wrapper = await mountFaq({ showCategories: false, showHeader: false })

    expect(wrapper.find('[data-testid="faq-category-all"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="section-header"]').exists()).toBe(false)
  })

  it('links the contact card to the contact page', async () => {
    expect((await mountFaq()).find('[data-testid="faq-contact-button"]').attributes('href')).toBe('/en/contact')
  })

  it('keeps the glow background and the gradient contact card on aurora', async () => {
    activeTemplate.value = TemplateName.Aurora

    const wrapper = await mountFaq()

    expect(wrapper.find('[data-testid="section-header"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sections-faq"]').classes()).toContain('bg-ui-surface')
    expect(wrapper.html()).toContain('blur-')
  })

  it('paints the classic template flat: no glow, no gradient, a plain contact card', async () => {
    activeTemplate.value = TemplateName.Classic

    const wrapper = await mountFaq()

    expect(wrapper.find('[data-testid="classic-section-header"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sections-faq"]').classes()).toContain('bg-classic-surface')
    expect(wrapper.html()).not.toContain('blur-')
    expect(wrapper.find('[data-testid="faq-contact"]').html()).not.toContain('gradient')
    expect(wrapper.find('[data-testid="faq-contact"]').classes()).toContain('bg-classic-surface')
    expect(wrapper.find('[data-testid="faq-contact-button"]').attributes('href')).toBe('/en/contact')
  })

  it('renders nothing when there are no questions', async () => {
    serve([])

    const wrapper = await mountFaq()

    expect(wrapper.find('[data-testid="sections-faq"]').exists()).toBe(false)
  })
})
