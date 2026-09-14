<template>
  <component
    :is="domains"
    :price-rows="priceRows"
    :results="results"
    :pending="pending"
    :categories="categories"
    :active-category="activeCategory"
    :in-cart="cartDomains"
    :currency-blocked="currencyBlockedDomains"
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
 * so does everything that touches the network or the cart — the template only
 * renders and reports what the visitor clicked.
 */
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import { ALL_CATEGORY } from '~/templates/aurora/types'

/** Minimal shape of the product record used to find the domain product. */
interface DomainProduct { id: number, name: string }

const { t } = useI18n()
const { slot } = useTemplate()
const cart = useCartStore()
const currencyStore = useCurrencyStore()

const domains = slot('domains')

// SEO
const { baseUrl } = useSeo({
  title: t('seo.domains.title'),
  description: t('seo.domains.description'),
  keywords: t('seo.domains.keywords'),
  type: 'website',
  path: '/domains'
})

// Schema.org
const { organizationSchema, injectSchema } = useSchemaOrg()
injectSchema([
  organizationSchema(),
  {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    '@id': `${baseUrl}/domains#domainspage`,
    url: `${baseUrl}/domains`,
    name: t('seo.domains.title'),
    description: t('seo.domains.description'),
    inLanguage: ['en', 'ru', 'hy'],
    publisher: { '@id': `${baseUrl}/#organization` }
  }
])

const { priceRows, categories, results, pending, search } = useDomainLookup()
const activeCategory = ref(ALL_CATEGORY)

onMounted(() => {
  cart.init()
  currencyStore.init()
  currencyStore.load()
})

// The cart needs a product to hang a domain order on. Matching by name is what
// the original page did; there is no product type flag to key off.
const { data: products } = await useFetch<DomainProduct[]>('/api/portal/public/products')
const domainProductId = computed(() =>
  products.value?.find(p => p.name.toLowerCase().includes('domain'))?.id ?? null)

/** Domains already in the cart, so their row shows as added rather than repeating. */
const cartDomains = computed(() =>
  cart.items.filter(item => item.itemType === 'domain').map(item => item.domain ?? ''))

/**
 * Result names whose TLD sells in a currency other than the payer's — {@link addDomain} already
 * refuses these silently; this drives the aurora template's button so a visitor sees *why*
 * nothing happens instead of clicking a live-looking "Add" that does nothing, the way the
 * classic template's `canAddDomain`/`currencyMismatch` title already does.
 */
const currencyBlockedDomains = computed(() =>
  results.value
    .filter((r) => {
      if (r.status !== 'available' || !currencyStore.payerCode) return false
      const tld = r.name.slice(r.name.indexOf('.') + 1)
      const row = priceRows.value.find(pr => pr.tld === `.${tld}`)
      return !row || row.sellCurrency !== currencyStore.payerCode
    })
    .map(r => r.name))

/**
 * Adds a domain registration to the cart.
 *
 * Silently does nothing when no domain product exists — the button is only shown
 * for available names, and an operator with no domain product configured has
 * nothing to sell here. Also refuses when the TLD's own sell currency is not the
 * payer's current currency: the frontend never converts a stored price, so such a domain
 * cannot be sold in what the payer is billed in right now.
 *
 * @param domain Fully qualified domain the visitor chose.
 */
const addDomain = (domain: string) => {
  if (!domainProductId.value || cartDomains.value.includes(domain) || !currencyStore.payerCode) return

  const tld = domain.slice(domain.indexOf('.') + 1)
  const row = priceRows.value.find(r => r.tld === `.${tld}`)
  if (!row || row.sellCurrency !== currencyStore.payerCode) return

  cart.addItem({
    pid: domainProductId.value,
    name: domain,
    billingcycle: 'annually',
    cycleLabel: '1 Year',
    amount: row.registerAmount,
    currency: currencyStore.payerCode,
    domain,
    itemType: 'domain',
    domainAction: 'register',
    tld,
    years: 1,
  })
}
</script>
