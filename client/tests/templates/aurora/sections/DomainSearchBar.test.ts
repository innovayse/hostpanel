/**
 * Unit tests for DomainSearchBar: the e2e test ids, the search event, and the category chips.
 */
import { describe, expect, it } from 'vitest'
import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import DomainSearchBar from '~/templates/aurora/sections/DomainSearchBar.vue'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { useAuroraGlobals } from '../auroraGlobals'

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useAuroraGlobals()

describe('DomainSearchBar', () => {
  it('emits the typed term when submitted, through the ids the e2e specs use', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(DomainSearchBar)

    await wrapper.find('[data-testid="domain-search-input"]').setValue('shop')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('search')).toEqual([['shop']])
    expect(wrapper.find('[data-testid="domain-search-submit"]').attributes('type')).toBe('submit')
  })

  it('draws the field and the search button at one size step, so they have the same height', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(DomainSearchBar)

    /**
     * Reads the minimum-height classes of one element of the bar.
     *
     * @param selector Selector of the element.
     * @returns Its `min-h-*` classes.
     */
    const heights = (selector: string): string[] => wrapper.find(selector).classes().filter(name => /^(sm:)?min-h-/.test(name))

    expect(heights('[data-testid="domain-search-submit"]')).toEqual(heights('[data-testid="domain-search-input"]'))
    expect(heights('[data-testid="domain-search-submit"]')).not.toHaveLength(0)
  })

  it('submits natively to the domains page with the name in the query the page reads, so Enter works before hydration', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(DomainSearchBar)

    const form = wrapper.find('[data-testid="domain-search-bar-form"]')
    expect(form.attributes('method')).toBe('get')
    expect(form.attributes('action')).toBe(SITE_DOMAINS_PATH)
    expect(wrapper.find('[data-testid="domain-search-input"]').attributes('name')).toBe(DOMAIN_SEARCH_QUERY_PARAM)
  })

  it('intercepts the submit once mounted, so the page is not reloaded', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(DomainSearchBar)
    const event = new Event('submit', { cancelable: true })

    wrapper.find('form').element.dispatchEvent(event)

    expect(event.defaultPrevented).toBe(true)
  })

  it('shows the name a search started elsewhere ran for, and follows it when it changes', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(DomainSearchBar, { props: { initialQuery: 'first-site' } })
    const input = wrapper.find('[data-testid="domain-search-input"]')
    expect((input.element as HTMLInputElement).value).toBe('first-site')

    await wrapper.setProps({ initialQuery: 'second-site' })

    expect((input.element as HTMLInputElement).value).toBe('second-site')
  })

  it('gives the field a placeholder and disables it without priced extensions', async () => {
    await loadGlobals()
    const input = mountWithKit(DomainSearchBar, { props: { hasZones: false } }).find('[data-testid="domain-search-input"]')

    expect(input.attributes('placeholder')).toBe('aurora.domainSearch.placeholder')
    expect(input.attributes('disabled')).toBeDefined()
  })

  it('hides the filter when the registrar supplied one category or none', async () => {
    await loadGlobals()

    expect(mountWithKit(DomainSearchBar, { props: { categories: [ALL_CATEGORY] } }).find('[data-testid^="domain-category-"]').exists()).toBe(false)
  })

  it('shows a chip per category, labels the sentinel "all" and marks the active one', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(DomainSearchBar, { props: { categories: [ALL_CATEGORY, 'Popular'], active: 'Popular' } })

    expect(wrapper.find(`[data-testid="domain-category-${ALL_CATEGORY}"]`).text()).toBe('aurora.tldTable.all')
    expect(wrapper.find('[data-testid="domain-category-Popular"]').attributes('aria-selected')).toBe('true')
    expect(wrapper.find(`[data-testid="domain-category-${ALL_CATEGORY}"]`).attributes('aria-selected')).toBe('false')
    expect(wrapper.find('[data-testid="domain-category-Popular"]').text()).toBe('domains.pricing.categories.popular')
  })

  it('capitalises a category that has no translation', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(DomainSearchBar, { props: { categories: [ALL_CATEGORY, 'gaming'] } })

    expect(wrapper.find('[data-testid="domain-category-gaming"]').text()).toBe('Gaming')
  })

  it('emits the chosen category', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(DomainSearchBar, { props: { categories: [ALL_CATEGORY, 'Popular'] } })

    await wrapper.find('[data-testid="domain-category-Popular"]').trigger('click')

    expect(wrapper.emitted('update:category')).toEqual([['Popular']])
  })
})
