/**
 * Tests for the Refund Policy page: six icon-led sections and the contact card.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { mountPage, registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { LegalPage } from '~/constants/legalPage'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { legalTextPath } from '~/tests/helpers/contentPaths'
import { readOf } from '~/tests/helpers/stubRoutes'
import LegalDocument from '~/components/legal/LegalDocument.vue'
import Page from '~/pages/refund-policy.vue'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

describe('pages/refund-policy', () => {
  beforeEach(() => {
    spies = stubPublicPageRuntime()
    registerPageComponents({ LegalDocument })
  })

  it('shows the title, the badge, the subtitle and the dated last-updated line', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('h1').text()).toBe('refundPolicy.title')
    expect(wrapper.find('[data-testid="ui-page-hero-badge"]').text()).toBe('refundPolicy.badge')
    expect(wrapper.find('[data-testid="ui-page-hero-subtitle"]').text()).toBe('refundPolicy.subtitle')
    expect(wrapper.find('[data-testid="legal-last-updated"]').text()).toBe('refundPolicy.lastUpdated: 2026-03-12')
  })

  it('renders six sections, the domains one with four paragraphs', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.findAll('[data-testid="legal-section-marker"]')).toHaveLength(6)
    const domains = wrapper.find('[data-testid="legal-section-domains"]')
    expect(domains.text()).toContain('refundPolicy.sections.domains.content.3')
    expect(domains.text()).not.toContain('refundPolicy.sections.domains.content.4')
  })

  it('closes with the contact card in the normal tone', async () => {
    const wrapper = await mountPage(Page)

    const link = wrapper.find('[data-testid="legal-contact-link"]')
    expect(link.text()).toContain('refundPolicy.contact.link')
    expect(link.classes()).toContain('bg-primary-700')
    expect(link.classes()).not.toContain('bg-red-600')
  })

  it('asks search engines not to index it', async () => {
    await mountPage(Page)

    expect(spies.useSeoMeta).toHaveBeenCalledWith(expect.objectContaining({
      title: 'seo.legal.refund.title',
      robots: 'noindex, follow',
    }))
  })

  it('shows the operator text instead of the built-in sections and the built-in date', async () => {
    replies[readOf(legalTextPath(LegalPage.Refund))] = { body: { [PortalSettingKey.LegalRefund]: '## Seller\n\nAcme Ltd. sells under these terms.\n- Paid by card' } }

    const wrapper = await mountPage(Page)

    const section = wrapper.find('[data-testid="legal-section-custom-1"]')
    expect(section.text()).toContain('Seller')
    expect(section.text()).toContain('Acme Ltd. sells under these terms.')
    expect(section.text()).toContain('Paid by card')
    expect(wrapper.findAll('[data-testid="legal-section-marker"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="legal-last-updated"]').exists()).toBe(false)
  })
})
