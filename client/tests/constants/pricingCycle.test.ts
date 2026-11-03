/**
 * Unit tests for the pricing-cycle constants: every cycle is offered, shortest first, and each one maps to
 * the spelling the locale files use, its ISO duration and the months one payment covers.
 */
import { describe, expect, it } from 'vitest'
import { BILLING_CYCLE_I18N_KEY, PRICING_CYCLES, PRICING_CYCLE_I18N_KEY, PRICING_CYCLE_ISO_DURATION, PRICING_CYCLE_MONTHS, MONTHS_PER_YEAR } from '~/constants/pricingCycle'
import { BillingCycle } from '~/types/billingcycle'
import { PricingCycle } from '~/types/pricingcycle'

describe('pricing cycle constants', () => {
  it('offers the cycles shortest first', () => {
    expect(PRICING_CYCLES).toEqual([
      PricingCycle.Monthly,
      PricingCycle.Quarterly,
      PricingCycle.SemiAnnual,
      PricingCycle.Annual,
      PricingCycle.Biennial,
    ])
  })

  it('offers every cycle the enum names, once', () => {
    expect([...PRICING_CYCLES].sort()).toEqual(Object.values(PricingCycle).sort())
  })

  it('maps every cycle to its locale key, spelling the yearly one "annually"', () => {
    expect(PRICING_CYCLE_I18N_KEY[PricingCycle.Monthly]).toBe('monthly')
    expect(PRICING_CYCLE_I18N_KEY[PricingCycle.Annual]).toBe('annually')
    expect(PRICING_CYCLE_I18N_KEY[PricingCycle.Quarterly]).toBe('quarterly')
    expect(PRICING_CYCLE_I18N_KEY[PricingCycle.SemiAnnual]).toBe('semiannually')
    expect(PRICING_CYCLE_I18N_KEY[PricingCycle.Biennial]).toBe('biennially')
    for (const cycle of Object.values(PricingCycle)) {
      expect(PRICING_CYCLE_I18N_KEY[cycle]).toBeTruthy()
    }
  })

  it('maps every billing cycle of a service to its locale key', () => {
    expect(BILLING_CYCLE_I18N_KEY[BillingCycle.Monthly]).toBe('monthly')
    expect(BILLING_CYCLE_I18N_KEY[BillingCycle.Annual]).toBe('annually')
    expect(BILLING_CYCLE_I18N_KEY[BillingCycle.Quarterly]).toBe('quarterly')
    expect(BILLING_CYCLE_I18N_KEY[BillingCycle.SemiAnnual]).toBe('semiannually')
    expect(BILLING_CYCLE_I18N_KEY[BillingCycle.Biennial]).toBe('biennially')
    for (const cycle of Object.values(BillingCycle)) {
      expect(BILLING_CYCLE_I18N_KEY[cycle]).toBeTruthy()
    }
  })

  it('maps every cycle to the ISO duration one payment covers', () => {
    expect(PRICING_CYCLE_ISO_DURATION[PricingCycle.Monthly]).toBe('P1M')
    expect(PRICING_CYCLE_ISO_DURATION[PricingCycle.Annual]).toBe('P1Y')
    expect(PRICING_CYCLE_ISO_DURATION[PricingCycle.Quarterly]).toBe('P3M')
    expect(PRICING_CYCLE_ISO_DURATION[PricingCycle.SemiAnnual]).toBe('P6M')
    expect(PRICING_CYCLE_ISO_DURATION[PricingCycle.Biennial]).toBe('P2Y')
  })

  it('counts the months one payment of each cycle covers', () => {
    expect(PRICING_CYCLES.map((cycle) => PRICING_CYCLE_MONTHS[cycle])).toEqual([1, 3, 6, 12, 24])
  })

  it('keeps the billing cycles of a service and the pricing cycles of a plan in step', () => {
    expect(Object.values(BillingCycle).map((cycle) => cycle.toLowerCase()).sort())
      .toEqual(Object.values(PricingCycle).map((cycle) => cycle.toLowerCase()).sort())
  })

  it('counts twelve months in a year', () => {
    expect(MONTHS_PER_YEAR).toBe(12)
  })
})
