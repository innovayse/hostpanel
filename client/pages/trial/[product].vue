<template>
  <UiStack gap="none" class="bg-ui-page" data-testid="trial-page">
    <CatalogTrialHero
      :name="config.name"
      :icon="config.icon"
      :color="config.color"
      :trial-url="trialUrl"
      :learn-more-url="config.learnMoreUrl"
    />
    <CatalogTrialDetails
      :name="config.name"
      :color="config.color"
      :trial-url="trialUrl"
      :features="features"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Free-trial page of one product.
 * Route: /trial/[product]
 *
 * The page reads the catalogue's product groups and family products through the catalogue store
 * (in the payer's currency, like every other caller, so the cache key is shared), takes the
 * product's visual config from its group (`familyConfig`) and hands both to the trial components. It owns the head only.
 */
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { FAMILY_ICON, familyConfig, familyKey, parseDescription } from '~/utils/catalogGroups'

/** Colour of a product that matches no catalogue group: empty, so the trial components fall back to the portal brand accent. */
const UNKNOWN_PRODUCT_COLOR = ''

/** The current route. */
const route = useRoute()
/** Translation helpers and the active language. */
const { t, locale } = useI18n()
/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()

onMounted(() => {
  currencyStore.init()
  currencyStore.load()
})

/** Route param, e.g. `alpha-suite`. */
const productSlug = route.params.product as string

/** The catalogue group the route names, when it is a known family. */
const group = computed(() => catalogStore.familyGroups.find((candidate) => familyKey(candidate) === productSlug))

await usePayerAsyncData(`trial:${productSlug}`, () => catalogStore.loadFamilies(), {
  watch: [locale],
})

/** Display name, icon, accent colour and catalogue page of the product. */
const config = computed(() => (group.value ? familyConfig(group.value, catalogStore.familyProducts.filter((product) => Number(product.groupId) === group.value?.id)) : {
  name: productSlug,
  icon: FAMILY_ICON,
  color: UNKNOWN_PRODUCT_COLOR,
  learnMoreUrl: '/products',
  demoUrl: undefined,
}))

/** `ProductDto` has no pay type or order URL, so the group's first product stands in for the trial plan. */
const trialPlan = computed(() => catalogStore.familyProducts.find((product) => Number(product.groupId) === group.value?.id))

/** Where the trial call to action leads: always the family's own page. */
const trialUrl = computed(() => config.value.learnMoreUrl)

/** Feature lines parsed out of the trial plan's description. */
const features = computed(() => (trialPlan.value ? parseDescription(trialPlan.value.description || '').features : []))

useSeo({
  title: t('trial.seo.title', { product: config.value.name }),
  description: t('trial.seo.description', { product: config.value.name }),
  path: `/trial/${productSlug}`,
})
</script>
