<template>
  <UiStack gap="none" data-testid="products-page">
    <CatalogProductsHero />
    <CatalogProductFamilyGrid :products="products" />
    <BundleGrid :bundles="catalogStore.bundles" :discount-label="discountLabel" />
    <CatalogProductsComparison :groups="comparisonGroups" />
    <CatalogProductsBenefits />
    <CatalogProductsTestimonials />
    <SectionsCTA />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Products page: the product families with their plans and prices.
 *
 * The page reads the catalogue through the catalogue store (prices are in the payer's currency, so
 * the read runs again when the payer's currency or the language changes), groups it into
 * families (one per catalogue product group) with `buildProductFamilies`, and hands the result to the catalogue components, followed by the
 * bundle offers the operator sells. It owns
 * the head (title, canonical, hreflang, schema.org) and nothing visual.
 */
import { APP_LOCALES } from '~/constants/appLocales'
import { productTypeIcon } from '~/constants/productTypeIcon'
import { SCHEMA_ORG_CONTEXT } from '~/constants/schemaOrg'
import { useBundleDiscountLabel } from '~/composables/useBundleDiscountLabel'
import { useCartStore } from '~/stores/cart'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { formatMoney } from '~/utils/formatMoney'
import { buildComparisonGroups } from '~/utils/comparisonGroups'
import { buildProductFamilies } from '~/utils/productFamilies'
import type { ComparisonGroup } from '~/types/comparisongroup'
import type { ProductFamily } from '~/types/productfamily'
import { ProductType } from '~/types/producttype'
import { SeoPageType } from '~/types/seopagetype'

/** Translation helpers and the active language. */
const { t, locale } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()
/** The cart: its lines, totals and persistence. */
const cart = useCartStore()
/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()

onMounted(() => {
  cart.init()
  currencyStore.init()
  currencyStore.load()
})

/** Words a bundle's discount. */
const discountLabel = useBundleDiscountLabel()

/** The payer's currency to format prices with, never chosen by the page's language. */
const payerCurrency = computed(() => currencyStore.moneyCurrencyFor(currencyStore.payerCode))

await useAsyncData('products:families', async () => {
  const list = await catalogStore.loadFamilies()
  // Specification lines back the bullets of a family whose descriptions list none.
  await Promise.all([
    ...catalogStore.familyGroups.map((group) => catalogStore.loadProductFeatures(group.id)),
    catalogStore.loadBundles(),
  ])
  return list
}, {
  watch: [() => currencyStore.payerCode, locale],
})

/** The static domains card, the one family that is not a catalogue group. */
const domainsFamily = computed<ProductFamily>(() => ({
  id: 'domains',
  gid: null,
  icon: productTypeIcon(ProductType.Domain),
  color: '#3b82f6',
  demoUrl: '',
  learnMoreUrl: '/domains',
  name: t('domains.card.name'),
  // No tagline, like every other family card, so the cards read alike.
  tagline: '',
  description: t('domains.card.description'),
  features: [t('domains.card.f1'), t('domains.card.f2'), t('domains.card.f3')],
  pricing: [],
  type: ProductType.Domain,
}))

/** The domains card followed by one family per catalogue group. */
const products = computed<ProductFamily[]>(() =>
  buildProductFamilies(
    catalogStore.familyGroups,
    catalogStore.familyProducts,
    (amount) => formatMoney(amount, payerCurrency.value),
    currencyStore.payerCode,
    [domainsFamily.value],
    catalogStore.featuresFor,
  ))

/** The groups whose plans are comparable, for the comparison table: real plans, prices and specification lines. */
const comparisonGroups = computed<ComparisonGroup[]>(() =>
  buildComparisonGroups(
    catalogStore.familyGroups,
    catalogStore.familyProducts,
    (amount) => formatMoney(amount, payerCurrency.value),
    catalogStore.featuresFor,
  ))

/** Public site address, from the SEO helper that also sets the head. */
const { baseUrl: productsBaseUrl } = useSeo({
  title: t('seo.products.title', { site: siteName.value }),
  description: t('seo.products.description', { site: siteName.value }),
  keywords: t('seo.products.keywords', { site: siteName.value }),
  type: SeoPageType.Website,
  path: '/products',
})

/** Schema.org builders and the head injector. */
const { organizationSchema: productsOrgSchema, injectSchema: productsInjectSchema } = useSchemaOrg()
productsInjectSchema([
  productsOrgSchema(),
  {
    '@context': SCHEMA_ORG_CONTEXT,
    '@type': 'CollectionPage',
    '@id': `${productsBaseUrl}/products#productspage`,
    url: `${productsBaseUrl}/products`,
    name: t('seo.products.title'),
    description: t('seo.products.description', { site: siteName.value }),
    inLanguage: [...APP_LOCALES],
    publisher: { '@id': `${productsBaseUrl}/#organization` },
  },
])
</script>
