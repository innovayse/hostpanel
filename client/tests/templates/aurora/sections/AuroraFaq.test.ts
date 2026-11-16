/**
 * Unit tests for AuroraFaq: the questions come from the storefront store, every answer stays in
 * the page for crawlers, the first one is open, toggling follows the accordion, and the section
 * hides itself when there are no questions. The list is cut to the home limit, and the link to the
 * full FAQ page shows only when questions were left out.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { faqFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { readOf } from '~/tests/helpers/stubRoutes'
import { useContactDetails } from '~/composables/useContactDetails'
import { HOME_FAQ_LIMIT } from '~/constants/faq'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import type { FAQ } from '~/types/faq'
import AuroraFaq from '~/templates/aurora/sections/AuroraFaq.vue'

/** The stub playing the BFF and what it answers, by route; the hooks start it before each test and close it after. */
const { replies, stub } = usePortalSettingsServer({ useContactDetails })

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
const mountSection = () => mountAfterReads(stub(), [StorefrontPath.Faqs], () => mountWithKit(AuroraFaq))

describe('AuroraFaq', () => {
  beforeEach(async () => {
    await loadPortalSettings(replies)
    serve([
      faqFixture(1, { question: 'How fast?', answer: 'Very fast.' }),
      faqFixture(2, { question: 'Is it safe?', answer: 'Yes, safe.' }),
    ])
  })

  it('renders every question and keeps every answer in the markup', async () => {
    const wrapper = await mountSection()

    expect(wrapper.text()).toContain('How fast?')
    expect(wrapper.text()).toContain('Is it safe?')
    expect(wrapper.html()).toContain('Very fast.')
    expect(wrapper.html()).toContain('Yes, safe.')
  })

  it('opens the first answer and hides the second', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="ui-accordion-toggle-0"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.find('[data-testid="ui-accordion-toggle-1"]').attributes('aria-expanded')).toBe('false')
  })

  it('opens another answer when its question is clicked', async () => {
    const wrapper = await mountSection()

    await wrapper.find('[data-testid="ui-accordion-toggle-1"]').trigger('click')

    expect(wrapper.find('[data-testid="ui-accordion-toggle-1"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.find('[data-testid="ui-accordion-toggle-0"]').attributes('aria-expanded')).toBe('false')
  })

  it('lists at most the home limit and links to the full FAQ page when more exist', async () => {
    serve(Array.from({ length: HOME_FAQ_LIMIT + 3 }, (_, index) => faqFixture(index + 1)))

    const wrapper = await mountSection()

    expect(wrapper.findAll('[data-testid^="ui-accordion-toggle-"]')).toHaveLength(HOME_FAQ_LIMIT)
    expect(wrapper.text()).not.toContain(`Example question ${HOME_FAQ_LIMIT + 1}?`)
    expect(wrapper.find('[data-testid="faq-view-all"]').attributes('href')).toBe('/faq')
  })

  it('has no view-all link when every question fits the limit', async () => {
    serve(Array.from({ length: HOME_FAQ_LIMIT }, (_, index) => faqFixture(index + 1)))

    const wrapper = await mountSection()

    expect(wrapper.findAll('[data-testid^="ui-accordion-toggle-"]')).toHaveLength(HOME_FAQ_LIMIT)
    expect(wrapper.find('[data-testid="faq-view-all"]').exists()).toBe(false)
  })

  it('renders nothing when there are no questions', async () => {
    serve([])

    const wrapper = await mountSection()

    expect(wrapper.find('#faq').exists()).toBe(false)
    expect(wrapper.find('[data-testid="aurora-faq"]').exists()).toBe(false)
  })

  it('keeps the intro sticky and links the contact call to action to the contact page', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="faq-aside"]').classes()).toEqual(expect.arrayContaining(['lg:sticky', 'lg:self-start']))
    expect(wrapper.find('[data-testid="faq-contact"]').attributes('href')).toBe('/contact')
    expect(wrapper.find('[data-testid="faq-email"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="faq-phone"]').exists()).toBe(false)
  })

  it('shows the support email and phone when the operator configured them', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactEmail]: 'help@example.com', [PortalSettingKey.ContactPhone]: '+1 555 0100' })

    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="faq-email"]').attributes('href')).toBe('mailto:help@example.com')
    expect(wrapper.find('[data-testid="faq-phone"]').attributes('href')).toBe('tel:+15550100')
    expect(wrapper.find('[data-testid="faq-phone"]').text()).toBe('+1 555 0100')
  })
})
