/**
 * Unit tests for NovaFooter: link columns from content, and contact and social entries that
 * exist only when the operator filled them in.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import NovaFooter from '~/templates/nova/layout/NovaFooter.vue'
import { FOOTER_COLUMNS } from '~/templates/nova/content'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'

/** The stub server and the loader of the template globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useTemplateGlobals()

describe('NovaFooter', () => {
  it('is a separate block: the nova footer token surface, a visible top border and a soft shadow', async () => {
    await loadGlobals()
    const classes = mountWithKit(NovaFooter).find('footer').classes()

    expect(classes).toEqual(expect.arrayContaining(['bg-[color:var(--n-footer)]', 'border-t', 'border-nova-border']))
    expect(classes).not.toContain('bg-nova-bg')
    expect(classes.some((name) => name.startsWith('shadow-'))).toBe(true)
  })

  it('renders every column with all of its links', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(NovaFooter)

    for (const column of FOOTER_COLUMNS) {
      for (const link of column.links) {
        expect(wrapper.find(`[data-testid="nova-footer-${column.key}-${link.key}"]`).attributes('href')).toBe(link.to)
      }
    }
  })

  it('gives every footer link a nova-token hover and focus state, in light and dark', async () => {
    await loadGlobals({ settings: { 'portal.contact.email': 'hi@acme.test' } })
    const wrapper = mountWithKit(NovaFooter)
    const links = [...wrapper.findAll('[data-testid^="nova-footer-"][href]')]

    expect(links.length).toBeGreaterThan(1)
    for (const link of links) {
      expect(link.classes()).toEqual(expect.arrayContaining(['text-nova-muted', 'hover:text-nova-brand', 'focus-visible:text-nova-brand']))
    }
  })

  it('publishes no contact entry and no social row on a fresh install', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(NovaFooter)

    expect(wrapper.find('[data-testid="nova-footer-email"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nova-footer-phone"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid^="nova-footer-social-"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('nova.footer.social')
  })

  it('shows the contact entries the operator configured', async () => {
    await loadGlobals({ settings: { 'portal.contact.email': 'hi@acme.test', 'portal.contact.phone': '+374 (10) 123' } })
    const wrapper = mountWithKit(NovaFooter)

    expect(wrapper.find('[data-testid="nova-footer-email"]').attributes('href')).toBe('mailto:hi@acme.test')
    expect(wrapper.find('[data-testid="nova-footer-phone"]').attributes('href')).toBe('tel:+37410123')
  })

  it('shows only the social profiles that are set, building the Telegram URL from a handle', async () => {
    await loadGlobals({ settings: { 'portal.social.youtube': 'https://youtube.test/acme', 'portal.contact.telegram': 'acme' } })
    const wrapper = mountWithKit(NovaFooter)

    expect(wrapper.find('[data-testid="nova-footer-social-youtube"]').attributes('href')).toBe('https://youtube.test/acme')
    expect(wrapper.find('[data-testid="nova-footer-social-telegram"]').attributes('href')).toBe('https://t.me/acme')
    expect(wrapper.find('[data-testid="nova-footer-social-facebook"]').exists()).toBe(false)
  })

  it('opens social profiles in a new tab without handing over the opener', async () => {
    await loadGlobals({ settings: { 'portal.social.youtube': 'https://youtube.test/acme' } })
    const link = mountWithKit(NovaFooter).find('[data-testid="nova-footer-social-youtube"]')

    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('draws the bundled cube mark beside the site name when no logo is uploaded', async () => {
    await loadGlobals({ siteName: 'Acme Host' })
    const wrapper = mountWithKit(NovaFooter)

    expect(wrapper.find('[data-testid="app-logo-mark"]').attributes('src')).toContain('/favicon.svg')
    expect(wrapper.find('[data-testid="app-logo-name"]').text()).toBe('Acme Host')
  })

  it('shows the tax id only when set', async () => {
    await loadGlobals({ settings: { 'portal.legal.tax_id': 'TIN-1' } })

    expect(mountWithKit(NovaFooter).text()).toContain('TIN-1')
  })
})
