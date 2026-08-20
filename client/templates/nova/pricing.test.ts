import { describe, expect, it } from 'vitest'
import { parseAmount, popularPlanId, toNovaPlans, yearlyDiscountPercent } from './pricing'
import type { PlanCard } from '~/templates/aurora/types'

/**
 * Builds a plan card with the two prices under test.
 *
 * @param id Product id.
 * @param priceMonthly Formatted monthly price.
 * @param priceAnnual Formatted annual price expressed per month.
 */
const plan = (id: number, priceMonthly: string, priceAnnual = '—'): PlanCard => ({
  id,
  name: `Plan ${id}`,
  description: '',
  features: [],
  priceMonthly,
  priceAnnual,
  href: `/configure/${id}`,
})

describe('parseAmount', () => {
  it('reads the amount back out of a formatted price', () => {
    expect(parseAmount('$4.99')).toBe(4.99)
    expect(parseAmount('4.99 USD')).toBe(4.99)
    expect(parseAmount('֏12000.00')).toBe(12000)
  })

  it('treats the unpriced marker as no price, not as zero', () => {
    expect(parseAmount('—')).toBeNull()
    expect(parseAmount('')).toBeNull()
    expect(parseAmount(undefined)).toBeNull()
    expect(parseAmount(null)).toBeNull()
  })
})

describe('yearlyDiscountPercent', () => {
  it('states the saving of the annual price against twelve monthly ones', () => {
    // $10 a month against $8 a month billed yearly is a fifth off.
    expect(yearlyDiscountPercent('$10.00', '$8.00')).toBe(20)
  })

  it('rounds to a whole percent', () => {
    expect(yearlyDiscountPercent('$9.99', '$8.49')).toBe(15)
  })

  it('says nothing when the yearly price is no cheaper', () => {
    expect(yearlyDiscountPercent('$10.00', '$10.00')).toBeNull()
  })

  it('says nothing when the yearly price is dearer', () => {
    expect(yearlyDiscountPercent('$10.00', '$12.00')).toBeNull()
  })

  it('says nothing when either price is missing', () => {
    expect(yearlyDiscountPercent('—', '$8.00')).toBeNull()
    expect(yearlyDiscountPercent('$10.00', '—')).toBeNull()
  })

  it('says nothing when the monthly price is zero, which no percentage describes', () => {
    expect(yearlyDiscountPercent('$0.00', '$0.00')).toBeNull()
  })
})

describe('popularPlanId', () => {
  it('picks the mid-priced plan', () => {
    expect(popularPlanId([plan(1, '$3.00'), plan(2, '$6.00'), plan(3, '$9.00')])).toBe(2)
  })

  it('picks by price, not by the order the plans arrived in', () => {
    expect(popularPlanId([plan(1, '$9.00'), plan(2, '$3.00'), plan(3, '$6.00')])).toBe(3)
  })

  it('takes the lower middle of an even number of plans, and takes it every time', () => {
    const plans = [plan(1, '$3.00'), plan(2, '$6.00'), plan(3, '$9.00'), plan(4, '$12.00')]

    expect(popularPlanId(plans)).toBe(2)
    expect(popularPlanId([...plans].reverse())).toBe(2)
  })

  it('badges nothing below three plans, where the middle would be the cheapest', () => {
    expect(popularPlanId([])).toBeNull()
    expect(popularPlanId([plan(1, '$3.00')])).toBeNull()
    expect(popularPlanId([plan(1, '$3.00'), plan(2, '$6.00')])).toBeNull()
  })

  it('ignores unpriced plans rather than letting them decide the badge', () => {
    const plans = [plan(1, '—'), plan(2, '$3.00'), plan(3, '$6.00'), plan(4, '$9.00')]

    expect(popularPlanId(plans)).toBe(3)
  })

  it('drops below three usable prices once the unpriced plans are excluded', () => {
    expect(popularPlanId([plan(1, '—'), plan(2, '$3.00'), plan(3, '$6.00')])).toBeNull()
  })

  it('withholds the badge when the middle price is shared, which makes the choice arbitrary', () => {
    expect(popularPlanId([plan(1, '$3.00'), plan(2, '$6.00'), plan(3, '$6.00')])).toBeNull()
  })
})

describe('toNovaPlans', () => {
  it('keeps the order it was given and badges exactly one plan', () => {
    const result = toNovaPlans([plan(1, '$9.00'), plan(2, '$3.00'), plan(3, '$6.00')])

    expect(result.map(entry => entry.id)).toEqual([1, 2, 3])
    expect(result.filter(entry => entry.popular).map(entry => entry.id)).toEqual([3])
  })

  it('carries each plan\'s own discount, and none where there is none', () => {
    const result = toNovaPlans([plan(1, '$10.00', '$8.00'), plan(2, '$10.00')])

    expect(result[0]?.discountPercent).toBe(20)
    expect(result[1]?.discountPercent).toBeNull()
  })

  it('gives an empty list back for an empty catalogue', () => {
    expect(toNovaPlans([])).toEqual([])
  })
})
