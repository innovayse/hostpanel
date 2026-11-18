/**
 * Unit tests for AuroraFooter: the link columns, and the newsletter, contact and social entries
 * that exist only when the operator configured them.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraFooter from '~/templates/aurora/layout/AuroraFooter.vue'
import { useAuroraGlobals } from '../auroraGlobals'

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useAuroraGlobals()

describe('AuroraFooter', () => {
  it('is a separate block: the footer token surface, a visible top border and a soft shadow', async () => {
    await loadGlobals()
    const classes = mountWithKit(AuroraFooter).find('footer').classes()

    expect(classes).toEqual(expect.arrayContaining(['bg-[color:var(--footer)]', 'border-t', 'border-line2']))
    expect(classes.some((name) => name.startsWith('shadow-'))).toBe(true)
  })

  it('renders the three link columns with their destinations', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraFooter)

    expect(wrapper.findAll('h3').map((title) => title.text())).toEqual([
      'aurora.footer.company',
      'aurora.footer.support',
      'aurora.footer.legal',
    ])
    expect(wrapper.find('a[href="/terms"]').exists()).toBe(true)
    expect(wrapper.find('a[href="/domains/transfer"]').exists()).toBe(true)
  })

  it('keeps the product catalogue reachable from the company column', async () => {
    await loadGlobals()
    const labels = mountWithKit(AuroraFooter).findAll('a[href="/products"]').map((link) => link.text())

    expect(labels).toContain('aurora.footer.services')
  })

  it('labels the /knowledgebase link in the company column "knowledgebase", not "blog"', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraFooter)
    const labels = wrapper.findAll('a[href="/knowledgebase"]').map((link) => link.text())

    expect(labels).toContain('aurora.footer.knowledgebase')
    expect(labels).not.toContain('aurora.footer.blog')
  })

  it('gives every footer link a theme-token hover and focus state, in light and dark', async () => {
    await loadGlobals({ settings: { 'portal.contact.email': 'hi@acme.test' } })
    const wrapper = mountWithKit(AuroraFooter)
    const links = [...wrapper.findAll('[data-testid="aurora-footer-link"]'), wrapper.find('[data-testid="aurora-footer-email"]')]

    expect(links.length).toBeGreaterThan(1)
    for (const link of links) {
      expect(link.classes()).toEqual(expect.arrayContaining(['text-mut', 'hover:text-ac1', 'focus-visible:text-ac1', 'hover:after:scale-x-100']))
    }
  })

  it('shows no newsletter form, no social row and no contact line on a fresh install', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraFooter)

    expect(wrapper.find('[data-testid="newsletter-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid^="footer-social-"]').exists()).toBe(false)
    expect(wrapper.find('a[href^="mailto:"]').exists()).toBe(false)
  })

  it('posts the newsletter form natively to the configured endpoint', async () => {
    await loadGlobals({ settings: { 'portal.newsletter.action_url': 'https://mail.test/subscribe' } })
    const form = mountWithKit(AuroraFooter).find('[data-testid="newsletter-form"]')

    expect(form.attributes('action')).toBe('https://mail.test/subscribe')
    expect(form.attributes('method')).toBe('post')
    expect(form.attributes('target')).toBe('_blank')
    expect(form.find('input').attributes('name')).toBe('email')
    expect(form.find('input').attributes('placeholder')).toBe('aurora.footer.newsletterPlaceholder')
  })

  it('warns about an obviously wrong newsletter address only after the field was left', async () => {
    await loadGlobals({ settings: { 'portal.newsletter.action_url': 'https://mail.test/subscribe' } })
    const wrapper = mountWithKit(AuroraFooter)
    const input = wrapper.find('[data-testid="newsletter-email"]')

    await input.setValue('not-an-address')
    expect(wrapper.find('[data-testid="newsletter-invalid"]').exists()).toBe(false)

    await input.trigger('blur')
    expect(wrapper.find('[data-testid="newsletter-invalid"]').exists()).toBe(true)
  })

  it('shows the contact entries and tax id the operator filled in', async () => {
    await loadGlobals({
      settings: {
        'portal.contact.email': 'hi@acme.test',
        'portal.contact.phone': '+374 (10) 123',
        'portal.legal.tax_id': 'TIN-1',
      },
    })
    const wrapper = mountWithKit(AuroraFooter)

    expect(wrapper.find('a[href="mailto:hi@acme.test"]').exists()).toBe(true)
    expect(wrapper.find('a[href="tel:+37410123"]').text()).toBe('+374 (10) 123')
    expect(wrapper.text()).toContain('TIN-1')
  })

  it('shows only the social profiles that are set, building Telegram from a handle', async () => {
    await loadGlobals({ settings: { 'portal.social.youtube': 'https://youtube.test/acme', 'portal.contact.telegram': 'acme' } })
    const wrapper = mountWithKit(AuroraFooter)

    expect(wrapper.find('[data-testid="footer-social-yt"]').attributes('href')).toBe('https://youtube.test/acme')
    expect(wrapper.find('[data-testid="footer-social-tg"]').attributes('href')).toBe('https://t.me/acme')
    expect(wrapper.find('[data-testid="footer-social-fb"]').exists()).toBe(false)
  })

  it('opens social profiles in a new tab without handing over the opener', async () => {
    await loadGlobals({ settings: { 'portal.social.youtube': 'https://youtube.test/acme' } })
    const link = mountWithKit(AuroraFooter).find('[data-testid="footer-social-yt"]')

    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('shows the operator site name beside the mark and in the copyright line', async () => {
    await loadGlobals({ siteName: 'Acme Host' })
    const text = mountWithKit(AuroraFooter).text()

    expect(text.match(/Acme Host/g)).toHaveLength(2)
    expect(text).not.toContain('Innovayse')
  })

  it('draws the bundled cube mark when no logo is uploaded, and the upload once there is one', async () => {
    await loadGlobals()
    expect(mountWithKit(AuroraFooter).find('[data-testid="app-logo-mark"]').attributes('src')).toContain('/favicon.svg')

    await loadGlobals({ logo: '/api/uploads/logo.png' })
    const wrapper = mountWithKit(AuroraFooter)
    expect(wrapper.find('[data-testid="app-logo-light"]').attributes('src')).toBe('/api/uploads/logo.png')
    expect(wrapper.find('[data-testid="app-logo-mark"]').exists()).toBe(false)
  })
})
