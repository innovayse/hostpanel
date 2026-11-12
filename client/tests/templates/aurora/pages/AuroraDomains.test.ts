/**
 * Unit tests for AuroraDomains: the search bar, the results and the price table are composed,
 * the search, category and add events travel up, and cart state flows down.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraDomains from '~/templates/aurora/pages/AuroraDomains.vue'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { DomainResultStatus } from '~/types/domainresultstatus'
import type { TldPriceRow } from '~/types/tldpricerow'
import { stubAuroraGlobals } from '../auroraGlobals'

/** One priced extension, so the page treats the registrar as configured. */
const ROW: TldPriceRow = {
  tld: '.com',
  register: '$10',
  renew: '$12',
  transfer: '$9',
  categories: ['Popular'],
  registerAmount: 10,
  transferAmount: 10,
  sellCurrency: 'USD',
}

describe('AuroraDomains', () => {
  it('renders the headline, the search bar and the price table', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraDomains, { props: { priceRows: [ROW] } })

    expect(wrapper.find('[data-testid="domains-title"]').text()).toBe('aurora.domainsPage.title')
    expect(wrapper.find('[data-testid="domain-search-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tld-row-.com"]').exists()).toBe(true)
  })

  it('disables the search and explains why when no extension is priced', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraDomains)

    expect(wrapper.find('[data-testid="domain-search-input"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="domain-results-no-zones"]').exists()).toBe(true)
  })

  it('passes the typed term up to the page', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraDomains, { props: { priceRows: [ROW] } })

    await wrapper.find('[data-testid="domain-search-input"]').setValue('shop')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('search')).toEqual([['shop']])
  })

  it('passes the chosen category up and filters the table by the active one', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraDomains, {
      props: { priceRows: [ROW], categories: [ALL_CATEGORY, 'Popular'], activeCategory: 'Country' },
    })

    expect(wrapper.find('[data-testid="tld-empty"]').exists()).toBe(true)

    await wrapper.find('[data-testid="domain-category-Popular"]').trigger('click')
    expect(wrapper.emitted('update:category')).toEqual([['Popular']])
  })

  it('passes an add request up and shows cart state from the page', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraDomains, {
      props: {
        priceRows: [ROW],
        results: [
          { name: 'a.com', price: '$10', status: DomainResultStatus.Available },
          { name: 'b.com', price: '$10', status: DomainResultStatus.Available },
        ],
        inCart: ['b.com'],
      },
    })

    await wrapper.find('[data-testid="domain-add-a.com"]').trigger('click')

    expect(wrapper.emitted('add')).toEqual([['a.com']])
    expect(wrapper.find('[data-testid="domain-add-b.com"]').attributes('disabled')).toBeDefined()
  })
})
