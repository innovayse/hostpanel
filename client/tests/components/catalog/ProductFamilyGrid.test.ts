/**
 * Unit tests for ProductFamilyGrid: one shortcut and one card per family, the links, the accent
 * colour of each card and the tagline and feature rules.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import ProductFamilyGrid from '~/components/catalog/ProductFamilyGrid.vue'
import { balancedColumns } from '~/utils/balancedGrid'
import type { ProductFamily } from '~/types/productfamily'

/**
 * Builds a family.
 *
 * @param overrides Fields that differ from the defaults.
 * @returns The family.
 */
const family = (overrides: Partial<ProductFamily> = {}): ProductFamily => ({
  id: 'alpha-suite',
  gid: 3,
  icon: 'mdi:school',
  color: '#8b5cf6',
  learnMoreUrl: '/products/alpha-suite',
  name: 'Alpha Suite',
  tagline: '',
  description: 'Learn online',
  features: ['One', 'Two', 'Three', 'Four'],
  pricing: [],
  ...overrides,
})

describe('ProductFamilyGrid', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
  })

  it('renders a shortcut and a card for every family', () => {
    const wrapper = mountWithKit(ProductFamilyGrid, { props: { products: [family(), family({ id: 'gamma-tool', name: 'Gamma Tool' })] } })

    expect(wrapper.findAll('[data-testid^="product-nav-"]')).toHaveLength(2)
    expect(wrapper.findAll('[data-testid^="product-card-"]')).toHaveLength(2)
  })

  it('links a family to its learn-more page, or to its detail page when it has none', () => {
    const wrapper = mountWithKit(ProductFamilyGrid, {
      props: { products: [family(), family({ id: 'gamma-tool', name: 'Gamma Tool', learnMoreUrl: '' })] },
    })

    expect(wrapper.find('[data-testid="product-link-alpha-suite"]').attributes('href')).toBe('/en/products/alpha-suite')
    expect(wrapper.find('[data-testid="product-link-gamma-tool"]').attributes('href')).toBe('/en/products/gamma-tool')
  })

  it('paints each card in its family accent colour', () => {
    const wrapper = mountWithKit(ProductFamilyGrid, { props: { products: [family()] } })

    const accent = wrapper.find('[data-testid="ui-accent"]').element as HTMLElement
    expect(accent.style.getPropertyValue('--accent')).toBe('#8b5cf6')
  })

  it('shows at most three features and a tagline only when the family has one', () => {
    const wrapper = mountWithKit(ProductFamilyGrid, {
      props: { products: [family(), family({ id: 'domains', name: 'Domains', tagline: 'Find yours', features: [] })] },
    })

    const first = wrapper.find('[data-testid="product-card-alpha-suite"]')
    expect(first.text()).toContain('Three')
    expect(first.text()).not.toContain('Four')
    expect(first.text()).not.toContain('Find yours')
    const domains = wrapper.find('[data-testid="product-card-domains"]')
    expect(domains.text()).toContain('Find yours')
    expect(domains.text()).not.toContain('products.products.badge')
  })

  it('renders no feature list and no placeholder for a family with none', () => {
    const wrapper = mountWithKit(ProductFamilyGrid, { props: { products: [family({ features: [] })] } })

    const card = wrapper.find('[data-testid="product-card-alpha-suite"]')
    expect(card.text()).not.toContain('products.products.badge')
  })

  it('hands the full icon name to the tile', () => {
    const wrapper = mountWithKit(ProductFamilyGrid, { props: { products: [family()] } })

    expect(wrapper.html()).not.toContain('mdi:mdi:')
  })

  it.each([1, 2, 5, 7])('balances %i families in one centred grid', (count) => {
    const products = Array.from({ length: count }, (_, i) => family({ id: `f${i}`, name: `Family ${i}` }))
    const wrapper = mountWithKit(ProductFamilyGrid, { props: { products } })
    const grid = wrapper.find('[data-testid="product-grid"]').element as HTMLElement

    expect(wrapper.findAll('[data-testid^="product-card-"]')).toHaveLength(count)
    expect(grid.getAttribute('data-columns')).toBe(String(balancedColumns(count)))
  })

  it('keeps cards the same height with very long text: clamped description, three bullets, wrapped name', () => {
    const long = 'word '.repeat(300)
    const wrapper = mountWithKit(ProductFamilyGrid, {
      props: { products: [family({ name: long, description: long, features: [long, long, long, long, long] })] },
    })
    const card = wrapper.find('[data-testid="product-card-alpha-suite"]')

    expect(card.classes()).toContain('h-full')
    expect(card.find('.line-clamp-3').exists()).toBe(true)
    expect(card.find('h3').classes()).toContain('break-words')
    expect(card.findAll('.line-clamp-1')).toHaveLength(3)
  })
})
