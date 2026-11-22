/**
 * Tests for the Privacy Policy page: nine icon-led sections, bullet lists on the sections that
 * have them, and no index.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { LegalPage } from '~/constants/legalPage'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { legalTextPath } from '~/tests/helpers/contentPaths'
import { readOf } from '~/tests/helpers/stubRoutes'
import LegalDocument from '~/components/legal/LegalDocument.vue'
import Page from '~/pages/privacy.vue'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

describe('pages/privacy', () => {
  beforeEach(() => {
    spies = stubPublicPageRuntime()
    registerPageComponents({ LegalDocument })
  })

  it('shows the title, the badge and the dated last-updated line', async () => {
    const wrapper = await mountRenderedPage(Page, 'legal-document')

    expect(wrapper.find('h1').text()).toBe('privacy.title')
    expect(wrapper.find('[data-testid="ui-page-hero-badge"]').text()).toBe('privacy.badge')
    expect(wrapper.find('[data-testid="legal-last-updated"]').text()).toBe('privacy.lastUpdated: 2026-03-12')
  })

  it('leads every section with its own icon', async () => {
    const wrapper = await mountRenderedPage(Page, 'legal-document')

    const markers = wrapper.findAll('[data-testid="legal-section-marker"]')
    expect(markers).toHaveLength(9)
    expect(markers[0]?.find('[data-icon="mdi:database"]').exists()).toBe(true)
    expect(markers[8]?.find('[data-icon="mdi:pencil"]').exists()).toBe(true)
  })

  it('lists five entries under collection and four under rights', async () => {
    const wrapper = await mountRenderedPage(Page, 'legal-document')

    const collection = wrapper.find('[data-testid="legal-section-collection"]')
    const rights = wrapper.find('[data-testid="legal-section-rights"]')
    expect(collection.find('[data-testid="legal-section-items"]').findAll('li')).toHaveLength(5)
    expect(rights.find('[data-testid="legal-section-items"]').findAll('li')).toHaveLength(4)
    expect(collection.text()).toContain('privacy.sections.collection.list.4')
  })

  it('has no list under a section without entries', async () => {
    const wrapper = await mountRenderedPage(Page, 'legal-document')

    expect(wrapper.find('[data-testid="legal-section-sharing"]').find('[data-testid="legal-section-items"]').exists()).toBe(false)
  })

  it('asks search engines not to index it', async () => {
    await mountRenderedPage(Page, 'legal-document')

    expect(spies.useSeoMeta).toHaveBeenCalledWith(expect.objectContaining({
      title: 'seo.legal.privacy.title',
      robots: 'noindex, follow',
    }))
  })

  it('shows the operator text instead of the built-in sections and the built-in date', async () => {
    replies[readOf(legalTextPath(LegalPage.Privacy))] = { body: { [PortalSettingKey.LegalPrivacy]: '## Seller\n\nAcme Ltd. sells under these terms.\n- Paid by card' } }

    const wrapper = await mountRenderedPage(Page, 'legal-document')

    const section = wrapper.find('[data-testid="legal-section-custom-1"]')
    expect(section.text()).toContain('Seller')
    expect(section.text()).toContain('Acme Ltd. sells under these terms.')
    expect(section.text()).toContain('Paid by card')
    expect(wrapper.findAll('[data-testid="legal-section-marker"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="legal-last-updated"]').exists()).toBe(false)
  })
})
