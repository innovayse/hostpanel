/**
 * Unit tests for SectionsCTA: the hosting headline, the domain search and the plans action.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import SectionsCTA from '~/components/sections/SectionsCTA.vue'

/** Mounts the band. */
const mountCta = () => mountWithKit(SectionsCTA)

describe('SectionsCTA', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
  })

  it('shows the headline and the subtitle', () => {
    const wrapper = mountCta()

    expect(wrapper.find('h2').text()).toBe('cta.title')
    expect(wrapper.text()).toContain('cta.subtitle')
  })

  it('is never rendered hidden: the content block enters with a CSS animation, not an initial opacity', () => {
    const block = mountCta().find('.animate-reveal-up')

    expect(block.attributes('style') ?? '').not.toContain('opacity')
    expect(block.classes()).toContain('animate-reveal-up')
  })

  it('offers the hosting plans as the main action, a contact link and no domain search', () => {
    const wrapper = mountCta()

    expect(wrapper.find('[data-testid="sections-domain-search"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="cta-contact"]').attributes('href')).toBe('/en/contact')
    expect(wrapper.find('[data-testid="cta-plans"]').attributes('href')).toBe('/en/hosting')
    expect(wrapper.find('[data-testid="cta-plans"]').text()).toBe('cta.button')
    expect(wrapper.find('[data-testid="cta-contact"]').text()).toBe('common.contactUs')
  })

  it('draws no gradient or glow decoration', () => {
    const html = mountCta().html()

    expect(html).not.toContain('bg-gradient')
    expect(html).not.toContain('blur-')
    expect(html).not.toContain('animate-blob')
  })
})
