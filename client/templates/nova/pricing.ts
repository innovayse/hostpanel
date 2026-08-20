import type { PlanCard } from '~/templates/aurora/types'
import type { NovaPlan } from '~/templates/nova/types'

/**
 * Reads the amount back out of a price `usePortalPlans` has already formatted.
 *
 * The composable returns `${prefix}${value.toFixed(2)}${suffix}` and keeps the
 * raw number to itself, so a template that needs to compare two prices has only
 * the formatted strings to work with. Extending the composable's return shape
 * would have been cleaner, but `PlanCard` is aurora's type and aurora is not
 * being touched in this change.
 *
 * The first numeric run wins: a currency prefix is a symbol or a letter code,
 * never a digit. `—`, the composable's own marker for an unpriced product,
 * yields null, and so does anything else without a number in it.
 *
 * @param formatted Price as the card would display it.
 * @returns The amount, or null when the string carries no usable price.
 */
export const parseAmount = (formatted: string | undefined | null): number | null => {
  if (!formatted) return null

  const match = /\d+(?:\.\d+)?/.exec(formatted)
  if (!match) return null

  const value = Number(match[0])
  return Number.isFinite(value) && value >= 0 ? value : null
}

/**
 * Yearly saving against paying month to month, as a whole percent.
 *
 * Both inputs are per-month figures — `priceAnnual` is the annual price divided
 * by twelve — so comparing them is the same comparison as twelve monthly
 * payments against one annual one, without re-deriving either.
 *
 * @param priceMonthly Formatted monthly price.
 * @param priceAnnual Formatted annual price expressed per month.
 * @returns The rounded percent when it is a real saving, otherwise null. A
 *   missing price, a zero monthly price, or an annual price that is no cheaper
 *   all return null, because none of them is a discount worth announcing.
 */
export const yearlyDiscountPercent = (
  priceMonthly: string | undefined | null,
  priceAnnual: string | undefined | null,
): number | null => {
  const monthly = parseAmount(priceMonthly)
  const annual = parseAmount(priceAnnual)

  if (monthly === null || annual === null || monthly <= 0) return null

  const percent = Math.round((1 - annual / monthly) * 100)
  return percent > 0 ? percent : null
}

/**
 * Picks the plan that carries the "most popular" badge.
 *
 * The rule is the mid-priced plan by monthly price, which is the plan a visitor
 * is being nudged toward anyway, and it is derived rather than configured so no
 * operator has to maintain it. Plans without a usable monthly price take no
 * part — an unpriced product must not decide the badge for the priced ones.
 *
 * A free plan takes no part either. It is not competing with the others on
 * price, and letting a zero sit at the bottom of the ordering drags the middle
 * down onto the plan below the one a visitor is actually being pointed at.
 *
 * Ordering is by amount and then by id, so two plans at the same price can
 * never swap places between renders. Where that lower-middle plan shares its
 * price with another, the choice between them is arbitrary and the badge is
 * withheld instead of being awarded on a tiebreak the visitor cannot see.
 *
 * @param plans Plans in display order.
 * @returns The id to badge, or null when there is no unambiguous answer.
 */
export const popularPlanId = (plans: readonly PlanCard[]): number | null => {
  const priced = plans
    .filter(plan => !plan.isFree)
    .map(plan => ({ id: plan.id, amount: parseAmount(plan.priceMonthly) }))
    .filter((entry): entry is { id: number, amount: number } => entry.amount !== null)
    .sort((a, b) => a.amount - b.amount || a.id - b.id)

  // Below three plans there is no middle to point at: with two, the badge would
  // land on the cheaper one and read as a recommendation to spend less.
  if (priced.length < 3) return null

  const middle = priced[Math.floor((priced.length - 1) / 2)]!
  const tied = priced.filter(entry => entry.amount === middle.amount).length > 1

  return tied ? null : middle.id
}

/**
 * Turns the plans the page fetched into the shape the pricing cards render.
 *
 * @param plans Plans as `usePortalPlans` returned them, in display order.
 * @returns The same plans, in the same order, each carrying its badge and
 *   discount. An empty input gives an empty result; the section handles that.
 */
export const toNovaPlans = (plans: readonly PlanCard[]): NovaPlan[] => {
  const popularId = popularPlanId(plans)

  return plans.map(plan => ({
    ...plan,
    popular: plan.id === popularId,
    // A free plan has no yearly saving to state, whatever the two prices say.
    discountPercent: plan.isFree
      ? null
      : yearlyDiscountPercent(plan.priceMonthly, plan.priceAnnual),
  }))
}
