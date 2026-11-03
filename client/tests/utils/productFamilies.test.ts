/**
 * Unit tests for the product-family helpers: the first offered price, the pricing rows, the grouping
 * of the catalogue into families and the lookup of one product for its detail page.
 */
import { describe, expect, it } from 'vitest'
import { PricingCycle } from '~/types/pricingcycle'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import { familyColor } from '~/utils/catalogGroups'
import type { ProductFamily } from '~/types/productfamily'
import type { ProductFeature } from '~/types/productfeature'
import type { ProductPlan } from '~/types/productplan'
import { buildPricingRows, buildProductFamilies, comparePlans, cycleAmount, familyBullets, familyCardBullets, familyProductType, findProductDetail, firstOfferedPrice, orderingPrice, recommendedPlanId, representativePlan, specificationBullets } from '~/utils/productFamilies'
import { ProductType } from '~/types/producttype'

/**
 * Builds a catalogue product.
 *
 * @param overrides Fields that differ from the defaults.
 * @returns The product.
 */
const product = (overrides: Partial<PortalProduct> & Pick<PortalProduct, 'id' | 'groupId' | 'name'>): PortalProduct => ({
  description: '',
  slug: null,
  pricing: null,
  ...overrides,
})

/**
 * Formats an amount the way the tests expect to read it.
 *
 * @param amount Amount in dollars.
 * @returns The amount with a dollar sign.
 */
const dollars = (amount: number): string => `$${amount}`

/**
 * Builds a catalogue group.
 *
 * @param id Group id.
 * @param name Group name.
 * @param overrides Fields that differ from the defaults.
 * @returns The group.
 */
const group = (id: number, name: string, overrides: Partial<PortalProductGroup> = {}): PortalProductGroup => ({
  id,
  name,
  description: null,
  productCount: 1,
  ...overrides,
})

/** The groups the fixtures below belong to. */
const groups: PortalProductGroup[] = [group(3, 'Alpha Suite', { productCount: 3 }), group(4, 'Beta Kit'), group(5, 'Gamma Tool')]

/** A family group: the parent that names it and two plans. */
const learnGroup: PortalProduct[] = [
  product({ id: 10, groupId: 3, name: 'Alpha Suite', slug: 'alpha-suite', description: 'Learn online<br />✔ Courses<br />✔ Exams' }),
  product({ id: 11, groupId: 3, name: 'Starter', slug: 'starter', description: '✔ One site', pricing: { monthly: 10, annual: 100 } }),
  product({ id: 12, groupId: 3, name: 'Pro', slug: 'pro', description: '✔ Two sites', pricing: { monthly: null, annual: 200 } }),
]

describe('firstOfferedPrice', () => {
  it('prefers the monthly price', () => {
    expect(firstOfferedPrice({ monthly: 5, annual: 50 })).toEqual({ cycle: PricingCycle.Monthly, amount: 5 })
  })

  it('falls back to the annual price when there is no monthly one', () => {
    expect(firstOfferedPrice({ monthly: null, annual: 50 })).toEqual({ cycle: PricingCycle.Annual, amount: 50 })
  })

  it('keeps a free (zero) monthly price', () => {
    expect(firstOfferedPrice({ monthly: 0, annual: 50 })).toEqual({ cycle: PricingCycle.Monthly, amount: 0 })
  })

  it('answers null when the plan carries no price', () => {
    expect(firstOfferedPrice({ monthly: null, annual: null })).toBeNull()
    expect(firstOfferedPrice(null)).toBeNull()
    expect(firstOfferedPrice(undefined)).toBeNull()
  })
})

describe('buildPricingRows', () => {
  it('maps each plan to a row with its first price and parsed features', () => {
    const rows = buildPricingRows(learnGroup.slice(1), dollars, 'USD')

    expect(rows).toHaveLength(2)
    expect(rows[0]).toMatchObject({ id: 11, tier: 'Starter', price: '$10', amount: 10, currency: 'USD', billingCycle: PricingCycle.Monthly, features: ['One site'] })
    expect(rows[1]).toMatchObject({ id: 12, price: '$200', billingCycle: PricingCycle.Annual })
  })

  it('leaves the price empty for a plan without one, and the currency empty while it resolves', () => {
    const rows = buildPricingRows([product({ id: 1, groupId: 3, name: 'Free-ish' })], dollars, null)

    expect(rows[0]).toMatchObject({ price: '', amount: 0, currency: '', billingCycle: PricingCycle.Monthly })
  })
})

describe('specificationBullets', () => {
  it('joins label and value once per label, in display order', () => {
    const lines: ProductFeature[] = [
      { id: 2, productId: 1, label: 'Bandwidth', value: '1 TB', sortOrder: 2 },
      { id: 1, productId: 1, label: 'Disk', value: '10 GB', sortOrder: 1 },
      { id: 3, productId: 2, label: 'Disk', value: '20 GB', sortOrder: 1 },
      { id: 4, productId: 2, label: 'Backups', value: '', sortOrder: 3 },
    ]

    expect(specificationBullets(lines)).toEqual(['Disk: 10 GB', 'Bandwidth: 1 TB', 'Backups'])
  })
})

describe('representativePlan and familyBullets', () => {
  it('picks the cheapest priced plan, and the first on a tie or when none is priced', () => {
    const cheap = product({ id: 2, groupId: 1, name: 'B', pricing: { monthly: 3, annual: null } })
    const dear = product({ id: 1, groupId: 1, name: 'A', pricing: { monthly: 9, annual: null } })

    expect(representativePlan([dear, cheap])).toBe(cheap)
    expect(representativePlan([dear, product({ id: 3, groupId: 1, name: 'C', pricing: { monthly: 9, annual: null } })])).toBe(dear)
    expect(representativePlan([])).toBeUndefined()
  })

  it('takes the bullets from the one plan, capped at three, and never from other plans', () => {
    const lines: ProductFeature[] = [
      ...['A', 'B', 'C', 'D'].map((label, i) => ({ id: i, productId: 1, label, value: '1', sortOrder: i })),
      { id: 9, productId: 2, label: 'Other', value: 'x', sortOrder: 0 },
    ]

    expect(familyBullets(product({ id: 1, groupId: 1, name: 'A' }), lines)).toEqual(['A: 1', 'B: 1', 'C: 1'])
    expect(familyBullets(undefined, lines)).toEqual([])
  })

  it('uses the group description as the family description and the plan lines for the bullets', () => {
    const lines: ProductFeature[] = [{ id: 1, productId: 11, label: 'Sites', value: '1', sortOrder: 0 }, { id: 2, productId: 12, label: 'Sites', value: '9', sortOrder: 0 }]
    const families = buildProductFamilies([group(3, 'Alpha Suite', { description: 'Family sentence.', productCount: 2 })], learnGroup.slice(1), dollars, 'USD', [], () => lines)

    expect(families[0]).toMatchObject({ description: 'Family sentence.', features: ['Sites: 1'] })
  })
})

describe('buildProductFamilies', () => {
  it('builds one family per group, priced by its sub-plans and named by its parent', () => {
    const families = buildProductFamilies(groups, learnGroup, dollars, 'USD')

    expect(families).toHaveLength(1)
    expect(families[0]).toMatchObject({ id: 'alpha-suite', gid: 3, tagline: '', description: 'Learn online' })
    expect(families[0]?.pricing.map((row) => row.id)).toEqual([11, 12])
    // Bullets come from ONE plan (the cheapest, Starter), never a mix of every plan's lines.
    expect(families[0]?.features).toEqual(['One site'])
  })

  it('prices a group that has only its parent by the parent alone', () => {
    const families = buildProductFamilies(
      groups,
      [product({ id: 20, groupId: 4, name: 'Beta Kit', slug: 'beta-kit', pricing: { monthly: 7, annual: null } })],
      dollars,
      'USD',
    )

    expect(families[0]?.pricing).toHaveLength(1)
    expect(families[0]?.pricing[0]).toMatchObject({ id: 20, price: '$7' })
  })

  it('leaves out a group that has no products, and puts the leading entries first', () => {
    const leading: ProductFamily = {
      id: 'domains', gid: null, icon: 'mdi:earth', color: '#3b82f6', learnMoreUrl: '/domains', name: 'Domains',
      tagline: 'Tag', description: 'Desc', features: [], pricing: [],
    }

    const families = buildProductFamilies(groups, learnGroup, dollars, 'USD', [leading])

    expect(families.map((family) => family.id)).toEqual(['domains', 'alpha-suite'])
  })

  it('shows domains once: a domain-type group is left out in favour of the leading domains card', () => {
    const leading: ProductFamily = {
      id: 'domains', gid: null, icon: 'mdi:earth', color: '#3b82f6', learnMoreUrl: '/domains', name: 'Domains',
      tagline: 'Tag', description: 'Desc', features: [], pricing: [],
    }

    const families = buildProductFamilies(
      [...groups, group(9, 'Domains')],
      [...learnGroup, product({ id: 90, groupId: 9, name: 'Domain Registration', type: ProductType.Domain })],
      dollars,
      'USD',
      [leading],
    )

    expect(families.map((family) => family.id)).toEqual(['domains', 'alpha-suite'])
  })

  it('derives the icon of a family from the type of its products', () => {
    const families = buildProductFamilies(
      [group(7, 'Epsilon Line')],
      [product({ id: 70, groupId: 7, name: 'Basic', type: ProductType.Vps })],
      dollars,
      'USD',
    )

    expect(families[0]?.icon).toBe('mdi:server-network')
  })

  it('falls back to the specification lines when no description lists a feature, and to nothing otherwise', () => {
    const lines: ProductFeature[] = [{ id: 1, productId: 70, label: 'Disk space', value: '50 GB', sortOrder: 1 }]
    const plans = [product({ id: 70, groupId: 7, name: 'Basic', description: 'Plain summary' })]

    const withLines = buildProductFamilies([group(7, 'Epsilon Line')], plans, dollars, 'USD', [], () => lines)
    const without = buildProductFamilies([group(7, 'Epsilon Line')], plans, dollars, 'USD')

    expect(withLines[0]?.features).toEqual(['Disk space: 50 GB'])
    expect(without[0]?.features).toEqual([])
  })

  it('leaves out an empty group and a group with no products in the list', () => {
    const families = buildProductFamilies(
      [group(3, 'Alpha Suite'), group(5, 'Gamma Tool', { productCount: 0 }), group(6, 'Delta Pack')],
      [...learnGroup, product({ id: 60, groupId: 4, name: 'Hidden', slug: 'hidden' }), product({ id: 61, groupId: 5, name: 'Stray' })],
      dollars,
      'USD',
    )

    expect(families.map((family) => family.id)).toEqual(['alpha-suite'])
  })

  it('prices every product of a group whose name matches none of them', () => {
    const families = buildProductFamilies(
      [group(7, 'Epsilon Line')],
      [product({ id: 70, groupId: 7, name: 'Basic', pricing: { monthly: 4, annual: null } }), product({ id: 71, groupId: 7, name: 'Plus', pricing: { monthly: 8, annual: null } })],
      dollars,
      'USD',
    )

    expect(families[0]).toMatchObject({ id: 'epsilon-line', gid: 7, learnMoreUrl: '/products/epsilon-line' })
    expect(families[0]?.pricing.map((row) => row.id)).toEqual([70, 71])
  })

  it('caps the family card bullets at three lines', () => {
    const many = product({
      id: 30, groupId: 5, name: 'Gamma Tool', slug: 'gamma-tool',
      description: ['One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight'].map((word) => `✔ ${word}`).join('<br />'),
    })

    expect(buildProductFamilies(groups, [many], dollars, 'USD')[0]?.features).toHaveLength(3)
  })
})

describe('findProductDetail', () => {
  it('finds a family by the slug of a product and shapes its plans', () => {
    const detail = findProductDetail(groups, learnGroup, 'alpha-suite')

    expect(detail).toMatchObject({ id: 'alpha-suite', gid: 3, name: 'Alpha Suite', color: familyColor('alpha-suite'), description: 'Learn online' })
    expect(detail?.plans.map((plan) => plan.tier)).toEqual(['Starter', 'Pro'])
    expect(detail?.plans[0]).toMatchObject({ id: 11, pricing: { monthly: 10, annual: 100 }, features: ['One site'], description: '', isFeatured: false })
  })

  it('carries the product type of each plan, which the cart keeps to tell digital goods from hosting', () => {
    const plans = learnGroup.map((entry) => (entry.id === 11 ? { ...entry, type: ProductType.Digital } : entry))

    const detail = findProductDetail(groups, plans, 'alpha-suite')

    expect(detail?.plans.map((plan) => plan.type)).toEqual([ProductType.Digital, undefined])
  })

  it('names the page after the family, never after the plan the address matched', () => {
    const hosting = [group(1, 'Web Hosting', { description: 'Fast, secure hosting.', productCount: 2 })]
    const plans = [
      product({ id: 21, groupId: 1, name: 'Starter Hosting', slug: 'starter-hosting', description: 'Starter blurb<br />✔ 1 site' }),
      product({ id: 22, groupId: 1, name: 'Business Hosting', slug: 'business-hosting', description: 'Business blurb' }),
    ]

    for (const address of ['web-hosting', 'starter-hosting', 'business-hosting']) {
      expect(findProductDetail(hosting, plans, address)).toMatchObject({ name: 'Web Hosting', description: 'Fast, secure hosting.' })
    }
  })

  it('falls back to the parent product blurb only when the group has no description', () => {
    expect(findProductDetail(groups, learnGroup, 'alpha-suite')?.description).toBe('Learn online')
    expect(findProductDetail([group(3, 'Alpha Suite', { description: '  Our suite.  ', productCount: 3 })], learnGroup, 'alpha-suite')?.description).toBe('Our suite.')
  })

  it('falls back to the first product of the family group when the slug differs', () => {
    const detail = findProductDetail(groups, [product({ id: 40, groupId: 3, name: 'Odd name', slug: 'odd' })], 'alpha-suite')

    expect(detail?.id).toBe('alpha-suite')
    expect(detail?.name).toBe('Alpha Suite')
    expect(detail?.plans.map((plan) => plan.tier)).toEqual(['Odd name'])
  })

  it('takes the key bullets from the parent product, else from the cheapest plan that lists some', () => {
    expect(findProductDetail(groups, learnGroup, 'alpha-suite')?.features).toEqual(['Courses', 'Exams'])

    const noParent = findProductDetail(groups, learnGroup.slice(1), 'alpha-suite')
    expect(noParent?.features).toEqual(['One site'])
  })

  it('backs the bullets with the specification lines when no description lists any', () => {
    const lines: ProductFeature[] = [{ id: 1, productId: 11, label: 'Disk', value: '10 GB', sortOrder: 1 }]
    const bare = [product({ id: 11, groupId: 3, name: 'Starter', slug: 'starter', pricing: { monthly: 10, annual: null } })]

    expect(findProductDetail(groups, bare, 'alpha-suite', () => lines)?.features).toEqual(['Disk: 10 GB'])
  })

  it('lists the plans in the order of the plan cards: the operator order first, then the cheapest', () => {
    const list = [
      product({ id: 61, groupId: 3, name: 'Dear', slug: 'dear', pricing: { monthly: 30, annual: null } }),
      product({ id: 62, groupId: 3, name: 'Cheap', slug: 'cheap', pricing: { monthly: 5, annual: null } }),
      product({ id: 63, groupId: 3, name: 'Pinned', slug: 'pinned', pricing: { monthly: 50, annual: null }, sortOrder: 1 }),
    ]

    expect(findProductDetail(groups, list, 'cheap')?.plans.map((plan) => plan.tier)).toEqual(['Pinned', 'Cheap', 'Dear'])
  })

  it('shows the parent as the single plan of a family that has no other product', () => {
    const only = [product({ id: 10, groupId: 3, name: 'Alpha Suite', slug: 'alpha-suite', pricing: { monthly: 9, annual: null } })]

    expect(findProductDetail(groups, only, 'alpha-suite')?.plans.map((plan) => plan.tier)).toEqual(['Alpha Suite'])
  })

  it('answers null when nothing matches', () => {
    expect(findProductDetail(groups, learnGroup, 'no-such-product')).toBeNull()
    expect(findProductDetail(groups, [], 'alpha-suite')).toBeNull()
  })

  it('ignores products that are not in a family group', () => {
    expect(findProductDetail(groups, [product({ id: 50, groupId: 99, name: 'Stray', slug: 'stray' })], 'stray')).toBeNull()
  })
})

describe('recommendedPlanId', () => {
  /**
   * Builds a plan.
   *
   * @param id Product id.
   * @param overrides Fields that differ from the defaults.
   * @returns The plan.
   */
  const plan = (id: number, overrides: Partial<ProductPlan> = {}): ProductPlan => ({
    id, tier: `Plan ${id}`, pricing: { monthly: id, annual: null }, description: '', features: [], isFeatured: false, ...overrides,
  })

  it('prefers the plan the operator featured', () => {
    expect(recommendedPlanId([plan(1), plan(2), plan(3, { isFeatured: true })])).toBe(3)
  })

  it('never recommends a free plan as the featured one', () => {
    expect(recommendedPlanId([plan(1, { isFeatured: true, pricing: { monthly: 0, annual: null } }), plan(2), plan(3)])).toBe(2)
  })

  it('points at the middle plan of three or more when nobody featured one', () => {
    expect(recommendedPlanId([plan(1), plan(2), plan(3)])).toBe(2)
    expect(recommendedPlanId([plan(1), plan(2), plan(3), plan(4)])).toBe(2)
  })

  it('has no middle to point at with fewer than three plans', () => {
    expect(recommendedPlanId([plan(1), plan(2)])).toBeNull()
    expect(recommendedPlanId([])).toBeNull()
  })
})

describe('cycleAmount', () => {
  it('reads the price of the cycle', () => {
    expect(cycleAmount({ monthly: 5, annual: 50 }, PricingCycle.Annual)).toBe(50)
  })

  it('reads the price of each cycle added after monthly and annual', () => {
    const pricing = { monthly: 5, annual: 50, quarterly: 14, semiAnnual: 27, biennial: 90 }

    expect(cycleAmount(pricing, PricingCycle.Quarterly)).toBe(14)
    expect(cycleAmount(pricing, PricingCycle.SemiAnnual)).toBe(27)
    expect(cycleAmount(pricing, PricingCycle.Biennial)).toBe(90)
  })

  it('is null for a cycle an older API never sent', () => {
    expect(cycleAmount({ monthly: 5, annual: 50 }, PricingCycle.Quarterly)).toBeNull()
    expect(cycleAmount({ monthly: 5, annual: 50, quarterly: null }, PricingCycle.Quarterly)).toBeNull()
  })

  it('is null when the plan has no price for the cycle or no prices at all', () => {
    expect(cycleAmount({ monthly: 5, annual: null }, PricingCycle.Annual)).toBeNull()
    expect(cycleAmount(null, PricingCycle.Monthly)).toBeNull()
    expect(cycleAmount(undefined, PricingCycle.Monthly)).toBeNull()
  })
})

describe('familyCardBullets', () => {
  it('uses the cheapest plan when it lists something', () => {
    const cheap = product({ id: 1, groupId: 1, name: 'A', description: '✔ Cheap one', pricing: { monthly: 3, annual: null } })
    const dear = product({ id: 2, groupId: 1, name: 'B', description: '✔ Dear one', pricing: { monthly: 9, annual: null } })

    expect(familyCardBullets([dear, cheap], [])).toEqual(['Cheap one'])
  })

  it('walks up to the next plan when the cheapest lists nothing, so the card is not left bare', () => {
    const bare = product({ id: 1, groupId: 1, name: 'A', description: 'Just a sentence.', pricing: { monthly: 3, annual: null } })
    const listed = product({ id: 2, groupId: 1, name: 'B', description: '✔ Fast disks', pricing: { monthly: 9, annual: null } })

    expect(familyCardBullets([listed, bare], [])).toEqual(['Fast disks'])
  })

  it('answers no bullets when no plan lists anything', () => {
    expect(familyCardBullets([product({ id: 1, groupId: 1, name: 'A', description: 'Plain.' })], [])).toEqual([])
    expect(familyCardBullets([], [])).toEqual([])
  })
})

describe('family cover data', () => {
  it('reads the family type from its products, and hosting from its group', () => {
    const vps = product({ id: 1, groupId: 2, name: 'V', type: ProductType.Vps })

    expect(familyProductType(group(2, 'Cloud'), [vps])).toBe('Vps')
    expect(familyProductType(group(1, 'Web Hosting'), [vps])).toBe('SharedHosting')
    expect(familyProductType(group(2, 'Cloud'), [])).toBeNull()
  })

  it('carries the type, the operator image and the featured flag onto the family', () => {
    const plans = [
      product({ id: 11, groupId: 3, name: 'Starter', slug: 'starter', type: ProductType.Vps, pricing: { monthly: 10, annual: null } }),
      product({ id: 12, groupId: 3, name: 'Pro', slug: 'pro', type: ProductType.Vps, isFeatured: true, pricing: { monthly: 20, annual: null } }),
    ]
    const withImage = buildProductFamilies([group(3, 'Alpha Suite', { imageUrl: ' /uploads/a.webp ', productCount: 2 })], plans, dollars, 'USD')
    const without = buildProductFamilies([group(3, 'Alpha Suite', { productCount: 2 })], plans.slice(0, 1), dollars, 'USD')

    expect(withImage[0]).toMatchObject({ type: ProductType.Vps, image: '/uploads/a.webp', featured: true })
    expect(without[0]).toMatchObject({ type: ProductType.Vps, image: null, featured: false })
  })
})

describe('orderingPrice and comparePlans with the newer cycles', () => {
  /**
   * Builds a product with the given prices.
   *
   * @param id The product id.
   * @param pricing Its prices.
   * @returns The product.
   */
  const product = (id: number, pricing: PortalProduct['pricing']): PortalProduct => ({ id, groupId: 1, name: `P${id}`, description: '', pricing })

  it('prices a plan per month from the first cycle it is sold on', () => {
    expect(orderingPrice(product(1, { monthly: 5, annual: 50 }))).toBe(5)
    expect(orderingPrice(product(2, { monthly: null, annual: 60 }))).toBe(5)
    expect(orderingPrice(product(3, { monthly: null, annual: null, quarterly: 12 }))).toBe(4)
    expect(orderingPrice(product(4, { monthly: null, annual: null, semiAnnual: 18 }))).toBe(3)
    expect(orderingPrice(product(5, { monthly: null, annual: null, biennial: 48 }))).toBe(2)
  })

  it('sorts a plan with no price last', () => {
    expect(orderingPrice(product(6, { monthly: null, annual: null }))).toBe(Number.POSITIVE_INFINITY)
    expect(orderingPrice(product(7, null))).toBe(Number.POSITIVE_INFINITY)
  })

  it('orders plans by their per-month price whichever cycle they are sold on', () => {
    const plans = [
      product(1, { monthly: 5, annual: 50 }),
      product(2, { monthly: null, annual: null, biennial: 48 }),
      product(3, { monthly: null, annual: null, quarterly: 12 }),
    ]

    expect([...plans].sort(comparePlans).map((plan) => plan.id)).toEqual([2, 3, 1])
  })

  it('offers the first of the cycles a plan is sold on', () => {
    expect(firstOfferedPrice({ monthly: null, annual: null, semiAnnual: 18, biennial: 48 })).toEqual({ cycle: PricingCycle.SemiAnnual, amount: 18 })
  })
})
