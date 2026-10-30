/**
 * Shapes the catalogue's flat product list into what the products pages show.
 *
 * The catalogue endpoint returns every plan of every group as one list; the storefront shows one
 * card per family (a group) on the products page and one detail page per family. The grouping,
 * the choice of the "parent" product that names a family, the first offered price of a plan and
 * the parsing of description copy live here as pure functions so they are tested without a page.
 *
 * @module utils/productFamilies
 */

import { FAMILY_BULLET_LIMIT } from '~/constants/planFeatureLimit'
import { PRICING_CYCLES, PRICING_CYCLE_MONTHS } from '~/constants/pricingCycle'
import { PricingCycle } from '~/types/pricingcycle'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import type { PortalProductPricing } from '~/types/portalproductpricing'
import type { ProductDetail } from '~/types/productdetail'
import type { ProductPlan } from '~/types/productplan'
import type { ProductFamily } from '~/types/productfamily'
import type { ProductPricingRow } from '~/types/productpricingrow'
import type { ProductFeature } from '~/types/productfeature'
import { HOSTING_GROUP_ID } from '~/constants/catalogGroup'
import { ProductType } from '~/types/producttype'
import { dominantProductType, familyConfig, familyKey, isDomainGroup, isFamilyGroup, nameToKey, parseDescription } from '~/utils/catalogGroups'

/** How many bullets a family card lists; every card shows the same number so a row lines up. */
const FAMILY_FEATURE_LIMIT = FAMILY_BULLET_LIMIT

/** Fewest plans a family needs before one of them can be called the middle one. */
const MIN_PLANS_FOR_MIDDLE = 3

/** Formats an amount in the payer's currency. */
export type MoneyFormatter = (amount: number) => string

/** A billing cycle together with the amount a plan is priced at for it. */
export interface OfferedPrice {
  /** The billing cycle. */
  cycle: PricingCycle
  /** The price for that cycle. */
  amount: number
}

/**
 * A plan's price for one billing cycle, in the caller's own currency, as the API sent it.
 *
 * @param pricing The plan's prices, or null/undefined when it has none.
 * @param cycle The billing cycle.
 * @returns The amount, or null when the plan carries no price for that cycle.
 */
export const cycleAmount = (pricing: PortalProductPricing | null | undefined, cycle: PricingCycle): number | null =>
  pricing?.[cycle] ?? null

/**
 * The first cycle a plan is priced for, and that price.
 *
 * @param pricing The plan's prices, or null/undefined when it has none.
 * @returns The cycle and amount, or null when the plan carries no price at all.
 */
export const firstOfferedPrice = (pricing: PortalProductPricing | null | undefined): OfferedPrice | null => {
  for (const cycle of PRICING_CYCLES) {
    const amount = cycleAmount(pricing, cycle)
    if (amount !== null) {
      return { cycle, amount }
    }
  }
  return null
}

/**
 * The cheapest per-month price a product offers, for ordering; products with no price sort last.
 *
 * @param product The product.
 * @returns The price per month of the first cycle the product is priced for (shortest first), else infinity.
 */
export const orderingPrice = (product: PortalProduct): number => {
  const offered = firstOfferedPrice(product.pricing)
  return offered ? offered.amount / PRICING_CYCLE_MONTHS[offered.cycle] : Number.POSITIVE_INFINITY
}

/**
 * Orders plans for display. The API returns products in no meaningful order (the database's), so
 * the operator's `sortOrder` wins when the product carries one and the monthly price, cheapest
 * first, decides otherwise.
 *
 * @param a First plan.
 * @param b Second plan.
 * @returns Negative when `a` comes first.
 */
export const comparePlans = (a: PortalProduct, b: PortalProduct): number => {
  const orderA = a.sortOrder ?? Number.POSITIVE_INFINITY
  const orderB = b.sortOrder ?? Number.POSITIVE_INFINITY
  if (orderA !== orderB) {
    return orderA < orderB ? -1 : 1
  }
  const priceA = orderingPrice(a)
  const priceB = orderingPrice(b)
  if (priceA === priceB) {
    return 0
  }
  return priceA < priceB ? -1 : 1
}

/**
 * Maps the catalogue products of one family onto the pricing rows a card renders.
 *
 * @param plans Products belonging to one family, as the catalogue endpoint returned them.
 * @param format Formats an amount in the payer's currency.
 * @param payerCode The payer's currency code; empty while it is still resolving.
 * @returns One row per plan: id, tier name, first offered price and its billing cycle, and the
 * feature lines parsed out of the description.
 */
export const buildPricingRows = (plans: PortalProduct[], format: MoneyFormatter, payerCode: string | null): ProductPricingRow[] =>
  plans.map((plan) => {
    const offered = firstOfferedPrice(plan.pricing)
    const { features } = parseDescription(plan.description || '')
    return {
      id: plan.id,
      tier: plan.name,
      slug: plan.slug || '',
      price: offered ? format(offered.amount) : '',
      amount: offered?.amount ?? 0,
      currency: payerCode ?? '',
      billingCycle: offered?.cycle ?? PricingCycle.Monthly,
      features,
    }
  })

/**
 * Turns a group's specification lines into card bullets: one "label: value" per distinct label.
 *
 * @param lines The group's `ProductFeature` lines, every plan's.
 * @returns The bullets, in display order, the first plan's value winning for a repeated label.
 */
export const specificationBullets = (lines: readonly ProductFeature[]): string[] => {
  const seen = new Set<string>()
  const bullets: string[] = []
  for (const line of [...lines].sort((a, b) => a.sortOrder - b.sortOrder)) {
    const label = line.label.trim()
    if (!label || seen.has(label)) {
      continue
    }
    seen.add(label)
    bullets.push(line.value.trim() ? `${label}: ${line.value.trim()}` : label)
  }
  return bullets
}

/**
 * The plan whose features speak for a whole family: the cheapest one that has a price.
 *
 * Mixing lines from different plans ("Websites: 1" beside "Accounts: 50") would describe no
 * real plan; one plan's lines always agree with each other. Ties and plans
 * with no price keep catalogue order, so the choice is stable.
 *
 * @param plans The family's plans.
 * @returns The representative plan, or undefined for an empty family.
 */
export const representativePlan = (plans: readonly PortalProduct[]): PortalProduct | undefined => {
  let best: PortalProduct | undefined
  let bestAmount = Number.POSITIVE_INFINITY
  for (const plan of plans) {
    const amount = firstOfferedPrice(plan.pricing)?.amount ?? Number.POSITIVE_INFINITY
    if (!best || amount < bestAmount) {
      best = plan
      bestAmount = amount
    }
  }
  return best
}

/**
 * The bullets of a family card, all taken from its representative plan.
 *
 * @param plan The representative plan.
 * @param specifications Specification lines of the plan's group.
 * @returns At most {@link FAMILY_FEATURE_LIMIT} bullets: the plan's own specification lines, or
 * the feature lines of its description when it has none; empty when it has neither.
 */
export const familyBullets = (plan: PortalProduct | undefined, specifications: readonly ProductFeature[]): string[] => {
  if (!plan) {
    return []
  }
  const own = specificationBullets(specifications.filter(line => line.productId === plan.id))
  const bullets = own.length > 0 ? own : parseDescription(plan.description || '').features.filter(Boolean)
  return bullets.slice(0, FAMILY_FEATURE_LIMIT)
}

/**
 * The plan whose bullets speak for a family: the cheapest one that has any.
 *
 * A cheapest plan with neither specification lines nor feature lines in its description would
 * leave the whole card without bullets while a sibling plan has them, so the choice walks the plans
 * from the cheapest up and takes the first that lists something. When none does, the cheapest wins
 * and the card simply shows no list.
 *
 * @param plans The family's plans.
 * @param specifications Specification lines of the family's group.
 * @returns The bullets of the chosen plan, at most {@link FAMILY_FEATURE_LIMIT}.
 */
export const familyCardBullets = (plans: readonly PortalProduct[], specifications: readonly ProductFeature[]): string[] => {
  const cheapest = representativePlan(plans)
  const first = familyBullets(cheapest, specifications)
  if (first.length > 0) {
    return first
  }
  const byPrice = [...plans].sort((a, b) =>
    (firstOfferedPrice(a.pricing)?.amount ?? Number.POSITIVE_INFINITY) - (firstOfferedPrice(b.pricing)?.amount ?? Number.POSITIVE_INFINITY))
  for (const plan of byPrice) {
    const bullets = familyBullets(plan, specifications)
    if (bullets.length > 0) {
      return bullets
    }
  }
  return []
}

/**
 * The service type a family sells, which picks its generated cover.
 *
 * @param group The family's group.
 * @param products The group's products.
 * @returns The hosting type for the hosting group, otherwise the products' dominant type; null when unknown.
 */
export const familyProductType = (group: PortalProductGroup, products: readonly PortalProduct[]): ProductType | null =>
  group.id === HOSTING_GROUP_ID ? ProductType.SharedHosting : dominantProductType(products)

/**
 * The key a product answers to in an address: its slug, or its normalised name when it has none.
 *
 * @param product The catalogue product.
 * @returns The key.
 */
const productKeyOf = (product: PortalProduct): string => product.slug?.trim() || nameToKey(product.name)

/**
 * Groups the catalogue into one entry per product family.
 *
 * Each family group becomes a family named by its group and its parent product (the one whose slug
 * or name is the family key, when there is one) and priced by its other plans; a group whose only
 * product is the parent is priced by the parent alone, and a group with no products is left out.
 * The card's description is the group's own; its bullets all come from one representative plan
 * (the cheapest, see {@link representativePlan}) so they agree with each other. A group that sells domain names is left out: the `leading` domains card stands for it, so
 * domains are shown once.
 *
 * @param groups The catalogue's product groups.
 * @param raw Every product the catalogue returned for the family groups.
 * @param format Formats an amount in the payer's currency.
 * @param payerCode The payer's currency code; empty while it is still resolving.
 * @param leading Entries that come before the catalogue families (the static domains card).
 * @param specifications The specification lines read for a group, by its id.
 * @returns The families, `leading` first.
 */
export const buildProductFamilies = (
  groups: PortalProductGroup[],
  raw: PortalProduct[],
  format: MoneyFormatter,
  payerCode: string | null,
  leading: ProductFamily[] = [],
  specifications: (groupId: number) => readonly ProductFeature[] = () => [],
): ProductFamily[] => {
  const items: ProductFamily[] = [...leading]
  for (const group of groups.filter(isFamilyGroup)) {
    const key = familyKey(group)
    const inGroup = raw.filter((product) => Number(product.groupId) === group.id)
    if (isDomainGroup(inGroup)) {
      continue
    }
    const parent = inGroup.find((product) => productKeyOf(product) === key)
    const subPlans = inGroup.filter((product) => product !== parent)
    // With no sub-plans the parent product is the single plan.
    const plans = subPlans.length > 0 ? subPlans : (parent ? [parent] : [])
    if (plans.length === 0) {
      continue
    }

    items.push({
      id: key,
      gid: group.id,
      ...familyConfig(group, inGroup),
      // A family has no tagline: the catalogue endpoint sends none. Only the static domains card
      // carries one, and it comes from the locale files.
      tagline: '',
      // The family's own sentence comes from its group; a plan's blurb is only the fallback.
      description: group.description?.trim() || parseDescription((parent ?? plans[0])?.description || '').summary,
      features: familyCardBullets(plans, specifications(group.id)),
      pricing: buildPricingRows(plans, format, payerCode),
      type: familyProductType(group, inGroup),
      image: group.imageUrl?.trim() || null,
      featured: plans.some((plan) => plan.isFeatured === true),
    })
  }
  return items
}

/**
 * Finds the product family a detail page is about and shapes it.
 *
 * The family is matched by the slug (or normalised name) of one of its products, then by the key of
 * its group. The page shows the FAMILY: its name and description are the group's, never those of
 * whichever product happened to match; the group's description falls back to the parent product's
 * blurb only when the operator wrote none. Its plans are the products of the group other than the
 * parent (the parent alone when it is the only product), in the order of the plan cards.
 *
 * @param groups The catalogue's product groups.
 * @param list The catalogue products of the family groups.
 * @param productId The route parameter, e.g. `alpha-suite`.
 * @param specifications The specification lines read for a group, by its id; they back the key
 * bullets of a family whose descriptions list none.
 * @returns The family, or null when no product matches.
 */
export const findProductDetail = (
  groups: PortalProductGroup[],
  list: PortalProduct[],
  productId: string,
  specifications: (groupId: number) => readonly ProductFeature[] = () => [],
): ProductDetail | null => {
  const families = groups.filter(isFamilyGroup)
  const inFamilies = list.filter((product) => families.some((group) => group.id === Number(product.groupId)))

  let found = inFamilies.find((product) => productKeyOf(product) === productId)
  if (!found) {
    const targetGroup = families.find((group) => familyKey(group) === productId)
    if (targetGroup) {
      // `ProductDto` has no featured flag, so the first product of the group is the one shown.
      found = inFamilies.find((product) => Number(product.groupId) === targetGroup.id)
    }
  }
  const group = found ? families.find((candidate) => candidate.id === Number(found?.groupId)) : undefined
  if (!found || !group) {
    return null
  }

  const key = familyKey(group)
  const inGroup = inFamilies.filter((product) => Number(product.groupId) === group.id)
  const config = familyConfig(group, inGroup)
  const parent = inGroup.find((product) => productKeyOf(product) === key)
  const subPlans = inGroup.filter((product) => product !== parent)
  // With no sub-plans the parent product is the single plan.
  const planProducts = (subPlans.length > 0 ? subPlans : (parent ? [parent] : [])).sort(comparePlans)
  const parentFeatures = parent ? parseDescription(parent.description || '').features.filter(Boolean) : []

  const plans: ProductPlan[] = planProducts.map((plan) => ({
    id: plan.id,
    tier: plan.name,
    pricing: plan.pricing ?? null,
    description: parseDescription(plan.description || '').summary,
    features: parseDescription(plan.description || '').features,
    isFeatured: plan.isFeatured === true,
  }))

  return {
    id: key,
    gid: group.id,
    // The family is named by its group, as on its card in the products list.
    name: group.name,
    icon: config.icon,
    color: config.color,
    demoUrl: config.demoUrl,
    learnMoreUrl: config.learnMoreUrl,
    description: group.description?.trim() || parseDescription((parent ?? planProducts[0] ?? found).description || '').summary,
    features: parentFeatures.length > 0 ? parentFeatures : familyCardBullets(planProducts, specifications(group.id)),
    plans,
  }
}

/**
 * The plan a detail page draws as the recommended one.
 *
 * The operator's featured plan wins (the first one that is not free); without one, the middle plan
 * of three or more is recommended, as on the other pricing pages. Fewer than three plans have no
 * middle to point at, so none is recommended.
 *
 * @param plans The family's plans, in display order.
 * @returns The id of the recommended plan, or null when there is none.
 */
export const recommendedPlanId = (plans: readonly ProductPlan[]): number | null => {
  const featured = plans.find((plan) => plan.isFeatured && plan.pricing?.monthly !== 0)
  if (featured) {
    return featured.id
  }
  if (plans.length < MIN_PLANS_FOR_MIDDLE) {
    return null
  }
  return plans[Math.floor((plans.length - 1) / 2)]?.id ?? null
}
