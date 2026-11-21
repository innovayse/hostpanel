/**
 * Tests for the Acceptable Use Policy page: five icon-led sections and a warning-toned
 * abuse-report card.
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
import Page from '~/pages/acceptable-use.vue'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

describe('pages/acceptable-use', () => {
  beforeEach(() => {
    spies = stubPublicPageRuntime()
    registerPageComponents({ LegalDocument })
  })

  it('shows the title, the badge and the dated last-updated line', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('h1').text()).toBe('aup.title')
    expect(wrapper.find('[data-testid="ui-page-hero-badge"]').text()).toBe('aup.badge')
    expect(wrapper.find('[data-testid="legal-last-updated"]').text()).toBe('aup.lastUpdated: 2026-03-12')
  })

  it('renders five sections, the abuse one with five paragraphs', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.findAll('[data-testid="legal-section-marker"]')).toHaveLength(5)
    const abuse = wrapper.find('[data-testid="legal-section-abuse"]')
    expect(abuse.text()).toContain('aup.sections.abuse.content.4')
  })

  it('names the hosted-applications section as hosting copy, not a software-as-a-service one', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('[data-testid="legal-section-apps"]').text()).toContain('aup.sections.apps.title')
    expect(wrapper.find('[data-testid="legal-section-saas"]').exists()).toBe(false)
  })

  it('paints the report button as a warning and puts the shield before its label', async () => {
    const wrapper = await mountPage(Page)

    const link = wrapper.find('[data-testid="legal-contact-link"]')
    expect(link.classes()).toContain('bg-red-600')
    expect(link.text()).toContain('aup.contact.link')
    expect(link.find('svg').exists()).toBe(true)
  })

  it('asks search engines not to index it', async () => {
    await mountPage(Page)

    expect(spies.useSeoMeta).toHaveBeenCalledWith(expect.objectContaining({
      title: 'seo.legal.aup.title',
      robots: 'noindex, follow',
    }))
  })

  it('shows the operator text instead of the built-in sections and the built-in date', async () => {
    replies[readOf(legalTextPath(LegalPage.AcceptableUse))] = { body: { [PortalSettingKey.LegalAcceptableUse]: '## Seller\n\nAcme Ltd. sells under these terms.\n- Paid by card' } }

    const wrapper = await mountPage(Page)

    const section = wrapper.find('[data-testid="legal-section-custom-1"]')
    expect(section.text()).toContain('Seller')
    expect(section.text()).toContain('Acme Ltd. sells under these terms.')
    expect(section.text()).toContain('Paid by card')
    expect(wrapper.findAll('[data-testid="legal-section-marker"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="legal-last-updated"]').exists()).toBe(false)
  })
})
