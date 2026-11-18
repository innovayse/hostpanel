/**
 * Tests for the Cookie Policy page: the cookie types as cards, the uses as a checklist, a note,
 * and the contact address as a mail link.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { mountPage, registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import LegalDocument from '~/components/legal/LegalDocument.vue'
import Page from '~/pages/cookie-policy.vue'

/** The message tree `tm` returns for the two arrays the page reads. */
const MESSAGES: Record<string, unknown> = {
  'cookiePolicy.sections.typesOfCookies.types': [
    { title: 'Strictly necessary', description: 'Keeps you signed in.' },
    { title: 'Analytics', description: 'Counts visits.' },
  ],
  'cookiePolicy.sections.howWeUseCookies.uses': ['Authentication', 'Security', 'Language'],
}

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

describe('pages/cookie-policy', () => {
  beforeEach(() => {
    spies = stubPublicPageRuntime({ tm: key => MESSAGES[key] ?? [] })
    registerPageComponents({ LegalDocument })
  })

  it('shows the title and the dated last-updated line without a badge', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('h1').text()).toBe('cookiePolicy.title')
    expect(wrapper.find('[data-testid="ui-page-hero-badge"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="legal-last-updated"]').text()).toBe('cookiePolicy.lastUpdated: 2026-03-12')
  })

  it('renders seven numbered sections', async () => {
    const wrapper = await mountPage(Page)

    const markers = wrapper.findAll('[data-testid="legal-section-marker"]')
    expect(markers).toHaveLength(7)
    expect(markers[6]?.text()).toBe('7')
  })

  it('draws each cookie type as a card', async () => {
    const wrapper = await mountPage(Page)

    const cards = wrapper.find('[data-testid="legal-section-typesOfCookies"]').findAll('[data-testid="legal-section-card"]')
    expect(cards).toHaveLength(2)
    expect(cards[0]?.text()).toContain('Strictly necessary')
    expect(cards[0]?.text()).toContain('Keeps you signed in.')
  })

  it('lists the uses as a checklist', async () => {
    const wrapper = await mountPage(Page)

    const items = wrapper.find('[data-testid="legal-section-howWeUseCookies"]').find('[data-testid="legal-section-items"]')
    expect(items.findAll('li')).toHaveLength(3)
    expect(items.classes()).not.toContain('list-disc')
  })

  it('shows the managing note as a highlighted remark', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('[data-testid="legal-section-note"]').text()).toContain('cookiePolicy.sections.managingCookies.note')
  })

  it('links the contact address as a mail link and has no separate contact card', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactEmail]: 'hello@acme.test' })
    const wrapper = await mountPage(Page)

    const link = wrapper.find('[data-testid="legal-section-link"]')
    expect(link.attributes('href')).toBe('mailto:hello@acme.test')
    expect(link.text()).toBe('hello@acme.test')
    expect(wrapper.find('[data-testid="legal-contact"]').exists()).toBe(false)
  })

  it('leaves the contact link out when no address is configured', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.find('[data-testid="legal-section-link"]').exists()).toBe(false)
  })

  it('asks search engines not to index it and sets the social tags', async () => {
    await mountPage(Page)

    expect(spies.useSeoMeta).toHaveBeenCalledWith(expect.objectContaining({
      title: 'seo.legal.cookies.title',
      robots: 'noindex, follow',
      twitterCard: 'summary',
    }))
  })
})
