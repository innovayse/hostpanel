/**
 * Unit tests for ProductsComparison: the real plans of a group are the columns, the featured plan
 * wears the badge, the specification rows fill the body, nothing is hard-coded, and with several
 * groups a tab switches the table.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import ProductsComparison from '~/components/catalog/ProductsComparison.vue'
import MarketingSectionHeader from '~/components/marketing/MarketingSectionHeader.vue'
import { PricingCycle } from '~/types/pricingcycle'
import type { ComparisonGroup } from '~/types/comparisongroup'

/**
 * Builds a comparable group.
 *
 * @param id The group id.
 * @param names The plan names, one column each.
 * @param featuredIndex Index of the featured plan, or -1 for none.
 * @returns The group.
 */
const group = (id: number, names: string[], featuredIndex = -1): ComparisonGroup => ({
  id,
  name: `Group ${id}`,
  plans: names.map((name, index) => ({
    id: id * 10 + index, name, price: `$${index + 1}`, billingCycle: PricingCycle.Monthly, isFeatured: index === featuredIndex,
  })),
  rows: [
    { label: 'Disk', values: names.map((_, index) => `${(index + 1) * 10} GB`) },
    { label: 'Mail', values: names.map((_, index) => (index === 0 ? '' : 'Yes')) },
  ],
})

/**
 * Mounts the table.
 *
 * @param groups The comparable groups.
 * @returns The wrapper.
 */
const mountTable = (groups: ComparisonGroup[]) =>
  mountWithKit(ProductsComparison, {
    props: { groups },
    components: { MarketingSectionHeader },
    stubs: { 'i18n-t': true },
  })

describe('ProductsComparison', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
  })

  it('renders nothing when no group is comparable', () => {
    expect(mountTable([]).find('[data-testid="products-comparison"]').exists()).toBe(false)
  })

  it('renders the real plans with their prices and the specification rows', () => {
    const wrapper = mountTable([group(1, ['Alpha', 'Beta', 'Gamma'])])

    expect(wrapper.find('[data-testid="comparison-plan-10"]').text()).toContain('Alpha')
    expect(wrapper.find('[data-testid="comparison-price-12"]').text()).toBe('$3')
    expect(wrapper.find('[data-testid="comparison-row-0"]').text()).toContain('Disk')
    expect(wrapper.find('[data-testid="comparison-row-0"]').text()).toContain('20 GB')
  })

  it('is built from the kit table parts: a header cell per plan and a cell per value', () => {
    const wrapper = mountTable([group(1, ['Alpha', 'Beta'])])

    expect(wrapper.findAll('thead th')).toHaveLength(3)
    expect(wrapper.find('[data-testid="comparison-row-0"]').findAll('td')).toHaveLength(3)
  })

  it('shows a placeholder where a plan lacks a line', () => {
    const wrapper = mountTable([group(1, ['Alpha', 'Beta'])])

    expect(wrapper.find('[data-testid="comparison-row-1"]').text()).toContain('products.comparison.notIncluded')
  })

  it('badges only the featured plan', () => {
    const wrapper = mountTable([group(1, ['Alpha', 'Beta'], 1)])

    expect(wrapper.find('[data-testid="comparison-popular-11"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="comparison-popular-10"]').exists()).toBe(false)
  })

  it('links a plan to its order page', () => {
    const wrapper = mountTable([group(1, ['Alpha', 'Beta'])])

    expect(wrapper.find('[data-testid="comparison-plan-10"] a').attributes('href')).toBe('/en/configure/10')
  })

  it('carries no hard-coded tier names or footnote', () => {
    const text = mountTable([group(1, ['Alpha', 'Beta'])]).text()

    for (const word of ['Starter', 'Professional', 'Enterprise', 'footer']) {
      expect(text).not.toContain(word)
    }
  })

  it('has no tabs for one group', () => {
    expect(mountTable([group(1, ['Alpha', 'Beta'])]).find('[data-testid="comparison-groups"]').exists()).toBe(false)
  })

  it('switches the table with the group tabs', async () => {
    const wrapper = mountTable([group(1, ['Alpha', 'Beta']), group(2, ['Delta', 'Omega'])])

    expect(wrapper.text()).toContain('Alpha')
    await wrapper.find('[data-testid="comparison-tab-2"]').trigger('click')

    expect(wrapper.text()).toContain('Delta')
    expect(wrapper.text()).not.toContain('Alpha')
  })
})
