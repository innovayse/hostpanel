/**
 * Unit tests for DomainSearch: the typed term reaching the search event, the disabled state
 * without priced extensions, the starting-price hints, and the compact result rows.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import DomainSearch from '~/templates/aurora/sections/DomainSearch.vue'
import { DomainResultStatus } from '~/types/domainresultstatus'
import type { TldPriceRow } from '~/types/tldpricerow'
import { stubAuroraGlobals } from '../auroraGlobals'

/**
 * Builds one price row for the hint list.
 *
 * @param tld The extension.
 * @param registerAmount Numeric registration price; zero means unpriced.
 * @returns A price row.
 */
const hint = (tld: string, registerAmount: number): TldPriceRow => ({
  tld,
  register: `$${registerAmount}`,
  renew: '$0',
  transfer: '$0',
  categories: [],
  registerAmount,
  transferAmount: registerAmount,
  sellCurrency: 'USD',
})

describe('DomainSearch', () => {
  it('emits the typed term when the form is submitted', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainSearch)

    await wrapper.find('[data-testid="home-domain-search-input"]').setValue('my-site')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('search')).toEqual([['my-site']])
  })

  it('starts with the initial query and gives the field a placeholder', () => {
    stubAuroraGlobals()
    const input = mountWithKit(DomainSearch, { props: { initialQuery: 'abc' } }).find('[data-testid="home-domain-search-input"]')

    expect((input.element as HTMLInputElement).value).toBe('abc')
    expect(input.attributes('placeholder')).toBe('aurora.domainSearch.placeholder')
  })

  it('disables the field and the button, and says why, when nothing is priced', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainSearch, { props: { hasZones: false } })

    expect(wrapper.find('[data-testid="home-domain-search-input"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="home-domain-search-submit"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('aurora.domainSearch.noZones')
  })

  it('shows starting prices only for extensions that have one', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainSearch, { props: { priceHints: [hint('.com', 10), hint('.free', 0)] } })
    const hints = wrapper.find('[data-testid="price-hints"]')

    expect(hints.text()).toContain('.com')
    expect(hints.text()).not.toContain('.free')
  })

  it('renders one row per result with its status label and price', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainSearch, {
      props: { results: [{ name: 'x.com', price: '$10', status: DomainResultStatus.Available }] },
    })
    const row = wrapper.find('[data-testid="home-domain-result-x.com"]')

    expect(row.attributes('data-status')).toBe('available')
    expect(row.text()).toContain('aurora.domainSearch.available')
    expect(row.text()).toContain('$10')
  })

  it('shows the loading skeleton while a lookup runs', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(DomainSearch, { props: { pending: true } })

    expect(wrapper.find('[data-testid="domain-search-pending"]').exists()).toBe(true)
  })
})
