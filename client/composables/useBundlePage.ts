/**
 * The script of the bundle page: the bundle, its component products and (for a bundle with a
 * domain) the TLD price table, read through the stores in the payer's currency.
 *
 * It owns the visitor's choices, a billing cycle per component and the domain. On Continue it fills
 * the cart with the bundle's lines through the cart store, which carries the bundle to the order,
 * and moves on to checkout. The page only lays the returned state out.
 *
 * @module composables/useBundlePage
 */

import { PRICING_CYCLES, PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useBundleDiscountLabel } from '~/composables/useBundleDiscountLabel'
import { useCartStore } from '~/stores/cart'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { useDomainsStore } from '~/stores/domains'
import type { BundleComponentRow } from '~/types/bundlecomponentrow'
import type { BundleCycleOption } from '~/types/bundlecycleoption'
import type { BundleDomainChoice } from '~/types/bundledomainchoice'
import type { BundleDomainLine } from '~/types/bundledomainline'
import type { BundleSummaryLine } from '~/types/bundlesummaryline'
import { DomainOrderAction } from '~/types/domainorderaction'
import type { PricingCycle } from '~/types/pricingcycle'
import { bundleDiscount } from '~/utils/bundlePricing'
import { bundleCartItems, isBundleComplete, toCartBundle } from '~/utils/bundleOrder'
import { tldOf } from '~/utils/domainTld'
import { detailTitle } from '~/utils/seoTitle'
import { formatMoney } from '~/utils/formatMoney'
import { cycleAmount } from '~/utils/productFamilies'

/**
 * Wires the bundle page. Call it once at the top of the page's `<script setup>` and `await` it: it
 * waits for the catalogue so the server renders the bundle.
 *
 * @param id The bundle id from the address.
 * @returns The state and actions the page lays out.
 */
export const useBundlePage = async (id: number) => {
  /** Translation helpers and the active language. */
  const { t, locale } = useI18n()
  /** Site name the operator set; the browser tab title ends with it. */
  const { name: siteName } = useSiteIdentity()
  /** Prefixes a path with the active language. */
  const localePath = useLocalePath()
  /** The cart: its lines, totals and persistence. */
  const cart = useCartStore()
  /** The payer's currency and the currencies prices can be shown in. */
  const currencyStore = useCurrencyStore()
  /** The catalogue: bundles, products and TLD prices. */
  const catalogStore = useCatalogStore()
  /** The domains store: price table and name lookup. */
  const domainsStore = useDomainsStore()
  /** Words a bundle's discount. */
  const discountLabel = useBundleDiscountLabel()

  onMounted(() => {
    cart.init()
    currencyStore.init()
    currencyStore.load()
  })

  /** Whether the catalogue reads are still running. */
  const { pending } = await usePayerAsyncData(`bundle:${id}`, async () => {
    await catalogStore.loadBundles()
    await catalogStore.loadProducts()
    if (catalogStore.bundleFor(id)?.includesDomain) {
      await catalogStore.loadTldPricing()
    }
  }, {
    watch: [locale],
  })

  /** The bundle, or null when the id matches none. */
  const bundle = computed(() => catalogStore.bundleFor(id))

  /** Price table and name lookup state of the domains store. */
  const { priceRows, lookupResults, lookupPending } = storeToRefs(domainsStore)

  /** The payer's currency to format and price with, never chosen by the page's language. */
  const payerCurrency = computed(() => currencyStore.moneyCurrencyFor(currencyStore.payerCode))

  /**
   * Formats an amount in the payer's currency.
   *
   * @param amount The amount.
   * @returns The formatted amount.
   */
  const money = (amount: number): string => formatMoney(amount, payerCurrency.value)

  /** The cycle the visitor chose for each component, by product id; a component without a choice takes its first cycle. */
  const chosenCycles = ref<Record<number, PricingCycle>>({})

  /**
   * Remembers the cycle the visitor chose for a component.
   *
   * @param productId The component's product.
   * @param cycle The chosen cycle.
   */
  const chooseCycle = (productId: number, cycle: PricingCycle): void => {
    chosenCycles.value = { ...chosenCycles.value, [productId]: cycle }
  }

  /** The bundle's components with the cycles their products are priced on and the choice made. */
  const rows = computed<BundleComponentRow[]>(() =>
    (bundle.value?.components ?? []).map((component) => {
      const product = catalogStore.productsFor().find((candidate) => candidate.id === component.productId)
      const options: BundleCycleOption[] = []
      for (const cycle of PRICING_CYCLES) {
        const amount = cycleAmount(product?.pricing, cycle)
        if (amount !== null) {
          options.push({ cycle, label: t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[cycle]}`), amount, price: money(amount) })
        }
      }
      return {
        productId: component.productId,
        name: component.name,
        isPrimary: component.isPrimary,
        productType: product?.type,
        options,
        selected: options.find((option) => option.cycle === chosenCycles.value[component.productId]) ?? options[0] ?? null,
      }
    }))

  /** The domain the visitor chose; null while none is chosen. */
  const domainChoice = ref<BundleDomainChoice | null>(null)

  /** Why the last domain choice was refused. */
  const domainError = ref('')

  /**
   * Accepts a domain for the bundle when its name is well formed, its extension is priced and the
   * operator has a domain product to hang the line on.
   *
   * @param choice The domain the visitor entered or found.
   */
  const chooseDomain = (choice: BundleDomainChoice): void => {
    const tld = tldOf(choice.name)
    if (!choice.name.includes('.') || !tld) {
      domainError.value = t('domains.invalidDomain')
      return
    }
    if (!priceRows.value.some((row) => row.tld === `.${tld}`)) {
      domainError.value = t('domains.tldNotSupported')
      return
    }
    if (!catalogStore.domainProductId) {
      domainError.value = t('bundles.domain.unavailable')
      return
    }
    domainError.value = ''
    domainChoice.value = choice
  }

  /** Drops the chosen domain. */
  const clearDomain = (): void => {
    domainChoice.value = null
  }

  /** The chosen domain priced for the bundle's years; null until one is chosen and priced. */
  const domainLine = computed<BundleDomainLine | null>(() => {
    const choice = domainChoice.value
    const years = bundle.value?.domainYears
    const productId = catalogStore.domainProductId
    if (!choice || !years || !productId) {
      return null
    }
    const tld = tldOf(choice.name)
    const row = priceRows.value.find((candidate) => candidate.tld === `.${tld}`)
    if (!row) {
      return null
    }
    const yearly = choice.action === DomainOrderAction.Transfer ? row.transferAmount : row.registerAmount
    return {
      productId,
      name: choice.name,
      action: choice.action,
      eppCode: choice.eppCode,
      tld,
      amount: yearly * years,
      cycleLabel: t('domains.years', years),
    }
  })

  /** The priced lines of the summary: every component with a cycle, then the domain. */
  const lines = computed<BundleSummaryLine[]>(() => {
    const summary: BundleSummaryLine[] = []
    for (const row of rows.value) {
      if (row.selected) {
        summary.push({ id: `product-${row.productId}`, name: row.name, detail: row.selected.label, price: money(row.selected.amount) })
      }
    }
    if (domainLine.value) {
      summary.push({ id: 'domain', name: domainLine.value.name, detail: domainLine.value.cycleLabel, price: money(domainLine.value.amount) })
    }
    return summary
  })

  /** The sum of the summary lines, before the discount. */
  const subtotal = computed(() =>
    rows.value.reduce((sum, row) => sum + (row.selected?.amount ?? 0), 0) + (domainLine.value?.amount ?? 0))

  /** The estimated discount; the backend applies the real one. */
  const discountAmount = computed(() =>
    bundle.value ? bundleDiscount(toCartBundle(bundle.value), subtotal.value, currencyStore.payerCode) : 0)

  /** The estimated discount, formatted. */
  const discountText = computed(() => money(discountAmount.value))

  /** The estimated total, formatted. */
  const totalText = computed(() => money(subtotal.value - discountAmount.value))

  /** Whether everything the bundle needs is chosen and the payer's currency is known. */
  const complete = computed(() =>
    bundle.value !== null && currencyStore.payerCode !== null && isBundleComplete(bundle.value, rows.value, domainLine.value))

  /** Fills the cart with the bundle's lines and moves the visitor to checkout. */
  const addToCart = (): void => {
    const current = bundle.value
    const currency = currencyStore.payerCode
    if (!current || !currency || !complete.value) {
      return
    }
    cart.startBundle(toCartBundle(current), bundleCartItems(current, rows.value, domainLine.value, currency))
    navigateTo(localePath('/checkout'))
  }

  useSeo(() => ({
    title: detailTitle(t('bundles.title'), bundle.value?.name ?? siteName.value),
    description: bundle.value?.description ?? t('bundles.subtitle'),
    path: `/bundles/${id}`,
  }))

  return {
    pending,
    bundle,
    rows,
    lines,
    complete,
    discountLabel,
    discountText,
    totalText,
    priceRows,
    lookupResults,
    lookupPending,
    domainChoice,
    domainError,
    chooseCycle,
    chooseDomain,
    clearDomain,
    searchTlds: domainsStore.searchOfferedTlds,
    addToCart,
  }
}
