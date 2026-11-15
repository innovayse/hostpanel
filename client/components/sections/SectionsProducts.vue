<template>
  <section :id="HomeSectionId.Services" :class="[HOME_SECTION_SCROLL_MARGIN_CLASS, 'py-14 md:py-20 bg-classic-page']" data-testid="sections-products">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-12"
        :eyebrow="$t('hostingSections.plans.badge')"
        :title="$t('hostingSections.plans.title')"
        :subtitle="$t('hostingSections.plans.subtitle')"
      />

      <!-- Products grid: the shared family card, so the home page and the products page match -->
      <UiBalancedGrid :count="products.length" :min-columns="HOME_PRODUCT_FAMILY_LIMIT" data-testid="home-products-grid">
        <CatalogProductFamilyCard v-for="(product, index) in products" :key="product.id" :product="product" :index="index" />
      </UiBalancedGrid>

      <!-- View all -->
      <div class="mt-12 text-center">
        <UiButton variant="primary" :to="localePath('/products')" data-testid="home-products-all">
          <span>{{ $t('hostingSections.plans.viewAll') }}</span>
          <ArrowRight :size="20" :stroke-width="2" class="ml-2" />
        </UiButton>
      </div>
    </UiContainer>
  </section>
</template>

<script setup lang="ts">
/**
 * Products section of the home page: the first {@link HOME_PRODUCT_FAMILY_LIMIT} product families
 * (featured first, then catalogue order), drawn by the shared `ProductFamilyCard` (generated
 * cover, Featured badge, always-visible bullets). "View all plans" leads to the rest. The grid
 * keeps the width of a three column card, so one or two cards are centred rather than stretched.
 *
 * Data: the catalogue API (product groups and their products, shaped by `buildProductFamilies`:
 * the group names the family, one `description` field gives the summary and the features) and
 * `familyConfig` (icon, accent colour, page). There is no tagline: the catalogue sends none, so
 * only the static domains card shows one. Under the classic template the card paints itself in the
 * single brand colour.
 */

import { HomeSectionId } from '~/types/homesectionid'
import { HOME_SECTION_SCROLL_MARGIN_CLASS } from '~/constants/homeSections'
import { ArrowRight } from 'lucide-vue-next'
import { HOME_PRODUCT_FAMILY_LIMIT } from '~/constants/homeProducts'
import { FAMILY_BULLET_LIMIT } from '~/constants/planFeatureLimit'
import { productTypeIcon } from '~/constants/productTypeIcon'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import type { ProductFamily } from '~/types/productfamily'
import { ProductType } from '~/types/producttype'
import { topProductFamilies } from '~/utils/homeProductFamilies'
import { buildProductFamilies } from '~/utils/productFamilies'

/** Id of the domains card, which is not a catalogue family. */
const DOMAINS_CARD_ID = 'domains'

/** Icon of the domains card: the icon of the domain product type. */
const DOMAINS_CARD_ICON = productTypeIcon(ProductType.Domain)

/** Accent of the domains card. */
const DOMAINS_CARD_COLOR = '#3b82f6'

/** How many feature lines a card lists. */
const CARD_FEATURE_LIMIT = FAMILY_BULLET_LIMIT

/** Translation helpers and the active language. */
const { t, locale } = useI18n()
/** Prefixes a path with the active language. */
const localePath = useLocalePath()
/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()

onMounted(() => {
  currencyStore.init()
  currencyStore.load()
})

/** Every card in order: the static domains entry, then one per catalogue family that has products. */
const allFamilies = computed<ProductFamily[]>(() => {
  const items: ProductFamily[] = [{
    id: DOMAINS_CARD_ID,
    gid: null,
    name: t('domains.card.name'),
    // No tagline, like every other family card, so the cards read alike.
    tagline: '',
    description: t('domains.card.description'),
    features: [t('domains.card.f1'), t('domains.card.f2'), t('domains.card.f3')],
    icon: DOMAINS_CARD_ICON,
    color: DOMAINS_CARD_COLOR,
    learnMoreUrl: SITE_DOMAINS_PATH,
    pricing: [],
    type: ProductType.Domain,
    image: null,
    featured: false,
  }]

  // Prices are not shown on a card, so the formatter is never asked for one.
  const families = buildProductFamilies(catalogStore.familyGroups, catalogStore.familyProducts, () => '', null, [], catalogStore.featuresFor)
  for (const family of families) {
    items.push({ ...family, features: family.features.slice(0, CARD_FEATURE_LIMIT) })
  }

  return items
})

/** The cards shown: the first few of {@link allFamilies}, featured first. */
const products = computed<ProductFamily[]>(() => topProductFamilies(allFamilies.value))

await useAsyncData('products:families', async () => {
  const loaded = await catalogStore.loadFamilies()
  // Specification lines back the bullets of a family whose descriptions list none. Only the
  // families the home shows need them, so the read stays at a few requests however many groups exist.
  const shownGroupIds = products.value.flatMap((family) => (family.gid === null ? [] : [family.gid]))
  await Promise.all(shownGroupIds.map((groupId) => catalogStore.loadProductFeatures(groupId)))
  return loaded
}, {
  watch: [() => currencyStore.payerCode, locale],
})
</script>
