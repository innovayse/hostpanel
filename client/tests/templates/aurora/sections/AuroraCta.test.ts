/**
 * Unit tests for AuroraCta: the closing headline and its two buttons.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraCta from '~/templates/aurora/sections/AuroraCta.vue'
import { useAuroraGlobals } from '../auroraGlobals'

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useAuroraGlobals()

describe('AuroraCta', () => {
  it('renders the headline and lead', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraCta)

    expect(wrapper.find('h2').text()).toBe('aurora.cta.title')
    expect(wrapper.text()).toContain('aurora.cta.lead')
  })

  it('links the primary button to contact and the secondary to hosting', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraCta)

    expect(wrapper.find('[data-testid="cta-primary"]').attributes('href')).toBe('/contact')
    expect(wrapper.find('[data-testid="cta-secondary"]').attributes('href')).toBe('/hosting')
  })

  it('paints the card with the brand-tinted wash, a dot pattern and two glows', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraCta)

    expect(wrapper.find('[data-testid="aurora-cta"]').classes()).toEqual(expect.arrayContaining(['bg-cta-card', 'overflow-hidden']))
    expect(wrapper.find('[data-testid="cta-pattern"]').classes()).toContain('pattern-dots-20')
    expect(wrapper.findAll('[data-testid="ui-decor"]').length).toBe(2)
  })

  it('keeps the brand gradient on the primary button', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraCta)

    expect(wrapper.find('[data-testid="cta-primary"]').classes()).toContain('bg-brand')
  })
})
