/**
 * Pinia store for the public catalogue: products, plan cards, specification lines, marketing
 * slides, the country list and the TLD price table.
 *
 * Every public page reads from here. A server-rendered page wraps one of the load actions in
 * `useAsyncData(key, () => catalog.loadX(), { watch })` and renders store state, which
 * `@pinia/nuxt` carries from the server render to the browser. The store owns the loading flag
 * and the failure sentence, so a page holds no `catch`; it never builds a URL either:
 * transport belongs to {@link useCatalogApi}.
 *
 * Prices are always requested in the payer's own currency (`stores/currency.ts`), never the
 * page's language.
 *
 * @module stores/catalog
 */

import { defineStore } from 'pinia'
import { useCatalogApi } from '~/composables/apis/useCatalogApi'
import { HOSTING_GROUP_ID } from '~/constants/catalogGroup'
import { MONTHS_PER_YEAR, PRICING_CYCLES, PRICING_CYCLE_MONTHS } from '~/constants/pricingCycle'
import { apiErrorMessage } from '~/lib/errorHandler'
import { useCurrencyStore } from '~/stores/currency'
import type { CatalogBundle } from '~/types/catalogbundle'
import type { PlanCard } from '~/types/plancard'
import type { PricingCycle } from '~/types/pricingcycle'
import type { CatalogFilter } from '~/types/catalogfilter'
import type { CountryOption } from '~/types/countryoption'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import type { ProductFeature } from '~/types/productfeature'
import type { SlidePublic } from '~/types/slidepublic'
import type { TldPricing } from '~/types/tldpricing'
import { isFamilyGroup, parseDescription } from '~/utils/catalogGroups'
import { formatMoney } from '~/utils/formatMoney'
import { comparePlans, specificationBullets } from '~/utils/productFamilies'

/**
 * Central store for the public catalogue.
 *
 * @returns The catalogue state and the actions that change it.
 */
export const useCatalogStore = defineStore('catalog', () => {
  const api = useCatalogApi()
  const currencyStore = useCurrencyStore()

  /** Product lists already read, keyed by {@link productKey} (filter + currency). */
  const productsByKey = ref<Record<string, PortalProduct[]>>({})

  /** The catalogue's product groups; empty until {@link loadProductGroups}. */
  const productGroups = ref<PortalProductGroup[]>([])

  /** The active product bundles; empty until {@link loadBundles}. */
  const bundles = ref<CatalogBundle[]>([])

  /** Specification lines already read, keyed by product group id. */
  const featuresByGroup = ref<Record<number, ProductFeature[]>>({})

  /** The home-page marketing slides; empty until {@link loadSlides}. */
  const slides = ref<SlidePublic[]>([])

  /** The countries the address forms offer; empty until {@link loadCountries}. */
  const countries = ref<CountryOption[]>([])

  /** The TLD price table for the payer's currency; null until {@link loadTldPricing}. */
  const tldPricing = ref<TldPricing | null>(null)

  /** Number of catalogue reads in flight. */
  const inFlight = ref(0)

  /** Whether any catalogue read is in flight. */
  const loading = computed(() => inFlight.value > 0)

  /** The sentence for the last failed read; empty when there is none to show. */
  const error = ref('')

  /**
   * Builds the cache key of a product list.
   *
   * @param filter - The caller's filter.
   * @returns A stable key that also carries the payer's currency, so a currency change reads
   * through to a fresh list instead of showing the previous currency's prices.
   */
  const productKey = (filter: CatalogFilter): string =>
    JSON.stringify([filter.pid ?? '', filter.gid ?? '', filter.gids ?? '', currencyStore.payerCode ?? ''])

  /**
   * Runs one catalogue read with the loading flag and the failure sentence handled once.
   *
   * @param read - The read to run.
   * @param fallback - What to return when the read fails.
   * @returns The read's result, or the fallback after a failure ({@link error} then holds the
   * sentence).
   */
  const guarded = async <T>(read: () => Promise<T>, fallback: T): Promise<T> => {
    inFlight.value += 1
    error.value = ''
    try {
      return await read()
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      return fallback
    } finally {
      inFlight.value -= 1
    }
  }

  /**
   * Reads a product list in the payer's currency and keeps it.
   *
   * @param filter - Which products to read.
   * @returns The products; after a failure ({@link error} holds the sentence) the list already
   * kept for this filter and currency, empty when there is none. A failed read never replaces a
   * list that was read before, so a refused refresh cannot empty a page that is already showing it.
   */
  const loadProducts = async (filter: CatalogFilter = {}): Promise<PortalProduct[]> => {
    const key = productKey(filter)
    const list = await guarded(
      () => api.fetchProducts({ ...filter, currency: currencyStore.payerCode ?? undefined }),
      null
    )
    if (list === null) {
      return productsByKey.value[key] ?? []
    }
    productsByKey.value = { ...productsByKey.value, [key]: list }
    return list
  }

  /**
   * Looks up a product list {@link loadProducts} already read.
   *
   * @param filter - The same filter the read used.
   * @returns The products; empty when none were read for the payer's current currency.
   */
  const productsFor = (filter: CatalogFilter = {}): PortalProduct[] =>
    productsByKey.value[productKey(filter)] ?? []

  /** The hosting plans in display order, in the payer's currency; what the plan cards and the home page show. */
  const hostingPlans = computed(() => [...productsFor({ gid: HOSTING_GROUP_ID })].sort(comparePlans))

  /**
   * The id of the product a domain cart line hangs on, from the whole catalogue read by
   * `loadProducts()`. Matched by name because the catalogue carries no flag that says a product
   * is the domain one; null when none is configured (the cart then cannot take a domain).
   */
  const domainProductId = computed<number | null>(() =>
    productsFor().find(p => p.name.toLowerCase().includes('domain'))?.id ?? null)

  /**
   * Reads the hosting plans.
   *
   * @returns The hosting products.
   */
  const loadHostingPlans = (): Promise<PortalProduct[]> => loadProducts({ gid: HOSTING_GROUP_ID })

  /**
   * Maps the hosting plans onto the cards the storefront renders.
   *
   * Prices are formatted here, in the payer's own currency, never the page's language.
   *
   * @param hrefFor - Builds a plan's order link from its id; the page supplies its localised
   * path builder because a store has no router.
   * @param specifications - The group's specification lines. A plan whose description carries no
   * feature lines lists its own specification lines instead, so a card is never bare.
   * @returns One card per hosting plan.
   */
  const planCards = (hrefFor: (productId: number) => string, specifications: readonly ProductFeature[] = []): PlanCard[] =>
    hostingPlans.value.map((product, _index, ordered) => {
      // The domain keeps one featured product per group, but legacy rows may hold several; the
      // first featured, non-free plan in display order is the one that gets the badge.
      const featuredId = ordered.find(candidate => candidate.isFeatured === true && candidate.pricing?.monthly !== 0)?.id
      const currency = currencyStore.moneyCurrencyFor(currencyStore.payerCode)

      /**
       * Formats an amount in the payer's currency, or a dash when the backend reports no price.
       *
       * @param raw - Amount as the API returned it, or null when not offered.
       * @param divisor - Divide before formatting; the months of a cycle turn its price into a monthly one.
       * @returns The formatted amount.
       */
      const format = (raw: number | null | undefined, divisor = 1): string => {
        if (raw === null || raw === undefined) {
          return '—'
        }
        return formatMoney(raw / divisor, currency)
      }

      // A description is authored either as plain text or as HTML with <br /> between items,
      // and both are in the catalogue; `parseDescription` understands both and separates the
      // summary from the feature lines the card renders as a list.
      const { summary, features: described } = parseDescription(product.description ?? '')
      const features = described.length > 0 ? described : specificationBullets(specifications.filter(line => line.productId === product.id))

      // Free is decided by the price, not by the name: matching "free" in a name would feature
      // a "Freelancer Hosting" that costs money and miss a free plan called anything else.
      const monthly = product.pricing?.monthly

      // Every cycle the product is priced on, per month, so a storefront toggle can offer all of them.
      const cyclePrices: Partial<Record<PricingCycle, string>> = {}
      for (const cycle of PRICING_CYCLES) {
        const amount = product.pricing?.[cycle]
        if (amount !== null && amount !== undefined) {
          cyclePrices[cycle] = format(amount, PRICING_CYCLE_MONTHS[cycle])
        }
      }

      return {
        id: product.id,
        name: product.name,
        description: summary,
        features,
        isFree: monthly === 0,
        isFeatured: product.id === featuredId,
        priceMonthly: format(monthly),
        priceAnnual: format(product.pricing?.annual, MONTHS_PER_YEAR),
        cyclePrices,
        href: hrefFor(product.id)
      }
    })

  /**
   * The groups the storefront shows as product families: active and holding at least one product,
   * hosting first and the rest in the order the API sent them.
   */
  const familyGroups = computed(() =>
    productGroups.value
      .filter(isFamilyGroup)
      .sort((a, b) => Number(b.id === HOSTING_GROUP_ID) - Number(a.id === HOSTING_GROUP_ID)))

  /** The filter that selects the products of every family group; empty gids while there is none. */
  const familyFilter = computed<CatalogFilter>(() => ({ gids: familyGroups.value.map(group => group.id).join(',') }))

  /** The products of every family group, in the payer's currency; empty until {@link loadFamilies}. */
  const familyProducts = computed(() => (familyFilter.value.gids ? productsFor(familyFilter.value) : []))

  /**
   * Reads the product groups and keeps them.
   *
   * @returns The groups; after a failure ({@link error} holds the sentence) the groups already
   * kept, empty when there are none. A failed read never replaces groups that were read before:
   * the home page refreshes them after hydration, and an empty list there removed every family
   * card but the static domains one.
   */
  const loadProductGroups = async (): Promise<PortalProductGroup[]> => {
    const groups = await guarded(() => api.fetchProductGroups(), null)
    if (groups !== null) {
      productGroups.value = groups
    }
    return productGroups.value
  }

  /**
   * Reads the active product bundles and keeps them.
   *
   * @returns The bundles; after a failure ({@link error} holds the sentence) the ones already kept,
   * so a refused refresh cannot empty a page that is already showing them.
   */
  const loadBundles = async (): Promise<CatalogBundle[]> => {
    const list = await guarded(() => api.fetchProductBundles(), null)
    if (list !== null) {
      bundles.value = list
    }
    return bundles.value
  }

  /**
   * Looks up a bundle {@link loadBundles} already read.
   *
   * @param id - Bundle primary key.
   * @returns The bundle, or null when none has that id.
   */
  const bundleFor = (id: number): CatalogBundle | null => bundles.value.find(bundle => bundle.id === id) ?? null

  /**
   * The family read in flight and the currency it was started for, shared by every caller that
   * asks for the same currency until it settles; null when none runs.
   */
  let familiesInFlight: { currency: string | null, read: Promise<PortalProduct[]> } | null = null

  /**
   * Reads the product groups, then the products of every family group.
   *
   * The second read is skipped while there is no family group: an empty `gids` filter would ask
   * the endpoint for the whole catalogue.
   *
   * @returns The family products; empty when there is no family or a read failed.
   */
  const loadFamilies = (): Promise<PortalProduct[]> => {
    // The header and the page both ask for the families while one render runs; they share the
    // read in flight instead of sending it twice. The promise lives in the store, so under SSR it
    // is per request, never shared between visitors.
    // A read started for another currency is not shared: it would fill the list of that currency
    // and leave the one the caller now needs empty.
    if (familiesInFlight && familiesInFlight.currency === currencyStore.payerCode) {
      return familiesInFlight.read
    }
    const flight: { currency: string | null, read: Promise<PortalProduct[]> } = {
      currency: currencyStore.payerCode,
      read: (async () => {
        await loadProductGroups()
        return familyFilter.value.gids ? await loadProducts(familyFilter.value) : []
      })().finally(() => {
        // Only the read that is still the shared one clears it; a newer read for another currency stays.
        if (familiesInFlight === flight) {
          familiesInFlight = null
        }
      }),
    }
    familiesInFlight = flight
    return flight.read
  }

  /**
   * Reads the specification lines of a product group and keeps them.
   *
   * @param groupId - Product group whose plans are compared.
   * @returns The lines; empty after a failure.
   */
  const loadProductFeatures = async (groupId: number): Promise<ProductFeature[]> => {
    const lines = await guarded(() => api.fetchProductFeatures(groupId), [] as ProductFeature[])
    featuresByGroup.value = { ...featuresByGroup.value, [groupId]: lines }
    return lines
  }

  /**
   * Looks up the specification lines {@link loadProductFeatures} already read.
   *
   * @param groupId - Product group.
   * @returns The lines; empty when none were read.
   */
  const featuresFor = (groupId: number): ProductFeature[] => featuresByGroup.value[groupId] ?? []

  /**
   * Reads the home-page slides in a language.
   *
   * @param lang - Locale to return slide copy in.
   * @returns The slides; empty after a failure.
   */
  const loadSlides = async (lang: string): Promise<SlidePublic[]> => {
    slides.value = await guarded(() => api.fetchSlides(lang), [] as SlidePublic[])
    return slides.value
  }

  /**
   * Reads the country list.
   *
   * @returns The countries; empty after a failure.
   */
  const loadCountries = async (): Promise<CountryOption[]> => {
    countries.value = await guarded(() => api.fetchCountries(), [] as CountryOption[])
    return countries.value
  }

  /**
   * Reads the TLD price table in the payer's currency.
   *
   * @returns The table; null after a failure.
   */
  const loadTldPricing = async (): Promise<TldPricing | null> => {
    tldPricing.value = await guarded(
      () => api.fetchTldPricing(currencyStore.payerCode ?? undefined),
      null as TldPricing | null
    )
    return tldPricing.value
  }

  return {
    productGroups,
    bundles,
    familyGroups,
    familyProducts,
    productsByKey,
    featuresByGroup,
    slides,
    countries,
    tldPricing,
    loading,
    error,
    loadProducts,
    loadProductGroups,
    loadBundles,
    bundleFor,
    loadFamilies,
    productsFor,
    hostingPlans,
    domainProductId,
    loadHostingPlans,
    planCards,
    loadProductFeatures,
    featuresFor,
    loadSlides,
    loadCountries,
    loadTldPricing
  }
})
