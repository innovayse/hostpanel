/**
 * Tests for the Terms of Service page: it hands its copy to LegalDocument section by section and
 * keeps itself out of search results.
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
import Page from '~/pages/terms.vue'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

describe('pages/terms', () => {
  beforeEach(() => {
    spies = stubPublicPageRuntime()
    registerPageComponents({ LegalDocument })
  })

  it('shows the title, the badge and the dated last-updated line', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('h1').text()).toBe('terms.title')
    expect(wrapper.find('[data-testid="ui-page-hero-badge"]').text()).toBe('terms.badge')
    expect(wrapper.find('[data-testid="legal-last-updated"]').text()).toBe('terms.lastUpdated: 2026-03-12')
  })

  it('renders the eight sections with their paragraphs', async () => {
    const wrapper = await mountPage(Page)

    for (const id of ['acceptance', 'services', 'accounts', 'intellectual', 'payment', 'termination', 'liability', 'changes']) {
      expect(wrapper.find(`[data-testid="legal-section-${id}"]`).exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="legal-section-services"]').text()).toContain('terms.sections.services.content.2')
    expect(wrapper.find('[data-testid="legal-section-acceptance"]').text()).not.toContain('terms.sections.acceptance.content.2')
  })

  it('closes with a contact card linking to the localised contact page', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('[data-testid="legal-contact"]').text()).toContain('terms.contact.title')
    expect(wrapper.find('[data-testid="legal-contact-link"]').attributes('href')).toBe('/en/contact')
  })

  it('asks search engines not to index it', async () => {
    await mountPage(Page)

    expect(spies.useSeoMeta).toHaveBeenCalledWith(expect.objectContaining({
      title: 'seo.legal.terms.title',
      robots: 'noindex, follow',
    }))
  })

  it('shows the operator text instead of the built-in sections and the built-in date', async () => {
    replies[readOf(legalTextPath(LegalPage.Terms))] = { body: { [PortalSettingKey.LegalTerms]: '## Seller\n\nAcme Ltd. sells under these terms.\n- Paid by card' } }

    const wrapper = await mountPage(Page)

    const section = wrapper.find('[data-testid="legal-section-custom-1"]')
    expect(section.text()).toContain('Seller')
    expect(section.text()).toContain('Acme Ltd. sells under these terms.')
    expect(section.text()).toContain('Paid by card')
    expect(wrapper.findAll('[data-testid="legal-section-marker"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="legal-last-updated"]').exists()).toBe(false)
  })
})
