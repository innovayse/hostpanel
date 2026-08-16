<template>
  <component
    :is="domains"
    :price-rows="priceRows"
    :results="results"
    :pending="pending"
    :categories="categories"
    :active-category="activeCategory"
    :in-cart="cartDomains"
    @search="search"
    @update:category="value => (activeCategory = value)"
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
import { ALL_CATEGORY } from '~/templates/aurora/types'

/** Minimal shape of the product record used to find the domain product. */
interface DomainProduct { id: number, name: string }

const { t } = useI18n()
const { slot } = useTemplate()
const cart = useCartStore()

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

const { priceRows, categories, currencyCode, results, pending, search } = useDomainLookup()
const activeCategory = ref(ALL_CATEGORY)

onMounted(() => cart.init())

// The cart needs a product to hang a domain order on. Matching by name is what
// the original page did; there is no product type flag to key off.
const { data: products } = await useFetch<DomainProduct[]>('/api/portal/public/products')
const domainProductId = computed(() =>
  products.value?.find(p => p.name.toLowerCase().includes('domain'))?.id ?? null)

/** Domains already in the cart, so their row shows as added rather than repeating. */
const cartDomains = computed(() =>
  cart.items.filter(item => item.itemType === 'domain').map(item => item.domain ?? ''))

/**
 * Adds a domain registration to the cart.
 *
 * Silently does nothing when no domain product exists — the button is only shown
 * for available names, and an operator with no domain product configured has
 * nothing to sell here.
 *
 * @param domain Fully qualified domain the visitor chose.
 */
const addDomain = (domain: string) => {
  if (!domainProductId.value || cartDomains.value.includes(domain)) return

  const tld = domain.slice(domain.indexOf('.') + 1)
  const row = priceRows.value.find(r => r.tld === `.${tld}`)

  // The cart converts priceAmd from AMD; anything else has to go in as a plain
  // amount so the total is not run through a conversion that does not apply.
  const amount = row?.registerAmount ?? 0
  const inAmd = currencyCode.value === 'AMD'

  cart.addItem({
    pid: domainProductId.value,
    name: domain,
    billingcycle: 'annually',
    cycleLabel: '1 Year',
    price: row?.register ?? '',
    prefix: '',
    rawPrice: String(amount),
    ...(inAmd ? { priceAmd: amount } : {}),
    domain,
    itemType: 'domain',
    domainAction: 'register',
    tld,
    years: 1,
  })
}
</script>
