<template>
  <component
    :is="domains"
    :price-rows="priceRows"
    :results="results"
    :pending="pending"
    :query="lookupTerm"
    :categories="categories"
    :active-category="activeCategory"
    :in-cart="cartDomains"
    :error="notice"
    @search="search"
    @update:category="(value: string) => (activeCategory = value)"
    @add="addDomain"
  />
</template>

<script setup lang="ts">
/**
 * Domains page.
 *
 * SEO and structured data belong here so both templates emit the same head, and
 * so does everything that touches the network or the cart: the template only
 * renders and reports what the visitor clicked.
 */
import { APP_LOCALES } from '~/constants/appLocales'
import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import { SCHEMA_ORG_CONTEXT } from '~/constants/schemaOrg'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useCartStore } from '~/stores/cart'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { useDomainsStore } from '~/stores/domains'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { SeoPageType } from '~/types/seopagetype'
import { TemplateSlot } from '~/types/templateslot'
import { TemplateName } from '~/types/templatename'
import { DOMAIN_BILLING_CYCLE } from '~/constants/pricingCycle'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import { tldOf } from '~/utils/domainTld'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'

/** Translation helpers and the active language. */
const { t, locale } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()
/** The template registry: resolves the component the active template provides for a slot. */
const { slot, name: templateName } = useTemplate()
/** The current route; `?domain=` carries a name typed in a search bar elsewhere. */
const route = useRoute()
/** The cart: its lines, totals and persistence. */
const cart = useCartStore()
/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()
/** The domains store: price table, categories and name lookup. */
const domainsStore = useDomainsStore()

/** The template's domains component (classic or aurora), resolved by the registry. */
const domains = slot(TemplateSlot.Domains)

/** Public site address, from the SEO helper that also sets the head. */
const { baseUrl } = useSeo({
  title: t('seo.domains.title', { site: siteName.value }),
  description: t('seo.domains.description', { site: siteName.value }),
  keywords: t('seo.domains.keywords', { site: siteName.value }),
  type: SeoPageType.Website,
  path: SITE_DOMAINS_PATH,
})

/** Schema.org builders and the head injector. */
const { organizationSchema, injectSchema } = useSchemaOrg()
injectSchema([
  organizationSchema(),
  {
    '@context': SCHEMA_ORG_CONTEXT,
    '@type': 'CollectionPage',
    '@id': `${baseUrl}/domains#domainspage`,
    url: `${baseUrl}/domains`,
    name: t('seo.domains.title'),
    description: t('seo.domains.description', { site: siteName.value }),
    inLanguage: [...APP_LOCALES],
    publisher: { '@id': `${baseUrl}/#organization` },
  },
])

// Prices follow the payer's currency, so the table is read again when it resolves or changes.
await usePayerAsyncData('domains:tld-pricing', () => catalogStore.loadTldPricing(), {
  watch: [locale],
})

/** Price table, categories and name lookup state of the domains store. */
const { priceRows, categories, lookupResults: results, lookupPending: pending, lookupTerm, lookupError } = storeToRefs(domainsStore)

/** The sentence for the last add to the cart that could not be done; empty when there is none to show. */
const addError = ref('')

/** What the template shows above the results: a refused search, else a refused add to the cart. */
const notice = computed(() => lookupError.value || addError.value)

/**
 * Runs a lookup for the typed name across the offered extensions; a refusal of an earlier add to the
 * cart no longer applies to the new results, so it is cleared.
 *
 * @param term Raw text the visitor typed.
 * @returns Resolves once the rows are in the store.
 */
const search = (term: string): Promise<void> => {
  addError.value = ''
  return domainsStore.searchOfferedTlds(term)
}

/** The extension category the price table is filtered to. */
const activeCategory = ref(ALL_CATEGORY)

onMounted(() => {
  cart.init()
  currencyStore.init()
  currencyStore.load()

  // A name arriving in the address is looked up at once. The classic template reads the same
  // parameter itself and runs its own single-name search, so it is left to do that.
  const typed = route.query[DOMAIN_SEARCH_QUERY_PARAM]
  if (templateName.value !== TemplateName.Classic && typeof typed === 'string' && typed.trim()) {
    void search(typed)
  }
})

// The cart needs a product to hang a domain order on; the backend finds it by its product type.
await useAsyncData('domains:product', () => catalogStore.loadDomainProduct(), { watch: [locale] })

/** The id of the product a domain cart line hangs on; null when the operator has none. */
const domainProductId = computed(() => catalogStore.domainProductId)

/** Domains already in the cart, so their row shows as added rather than repeating. */
const cartDomains = computed(() =>
  cart.items.filter((item) => item.itemType === CartItemType.Domain).map((item) => item.domain ?? ''))

/**
 * Adds a domain registration to the cart.
 *
 * A name already in the cart is left as it is (its row shows as added). Every other reason the
 * line cannot be built is told to the visitor in {@link addError}: no domain product (the API's
 * own sentence for it), or no payer currency or price for the extension. The price row is already
 * converted to the payer's currency by the API (the TLD's own sell currency does not matter), so
 * the cart line carries that amount.
 *
 * @param domain Fully qualified domain the visitor chose.
 */
const addDomain = (domain: string): void => {
  if (cartDomains.value.includes(domain)) {
    return
  }

  addError.value = ''
  if (!domainProductId.value) {
    addError.value = catalogStore.domainProductError || t('domains.cannotAdd')
    return
  }

  const tld = tldOf(domain)
  const row = priceRows.value.find((priceRow) => priceRow.tld === `.${tld}`)
  if (!currencyStore.payerCode || !row) {
    addError.value = t('domains.cannotAdd')
    return
  }

  cart.addItem({
    pid: domainProductId.value,
    name: domain,
    billingcycle: DOMAIN_BILLING_CYCLE,
    cycleLabel: '1 Year',
    amount: row.registerAmount,
    currency: currencyStore.payerCode,
    domain,
    itemType: CartItemType.Domain,
    domainAction: DomainOrderAction.Register,
    tld,
    years: 1,
  })
}
</script>
