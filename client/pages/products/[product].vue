<template>
  <div v-if="productData">
    <!-- Single Full Page Section -->
    <section class="py-10 md:py-24 bg-[#0a0a0f] relative overflow-hidden min-h-screen">
      <!-- Animated Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 right-1/4 w-[600px] h-[600px] bg-secondary-500/25 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 left-1/3 w-[500px] h-[500px] bg-cyan-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />
        <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[400px] h-[400px] bg-primary-500/15 rounded-full blur-[130px] animate-blob animation-delay-4000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.025]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 80px 80px;" class="w-full h-full" />
        </div>
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-6xl mx-auto">
          <!-- Breadcrumbs -->
          <UiBreadcrumbs :items="breadcrumbItems" />

          <!-- Complete Product Display -->
          <div class="space-y-8">
            <!-- Product Info -->
            <div class="space-y-6">
              <!-- Product Header -->
              <div>
                <div class="flex items-start gap-4 mb-4">
                  <div
                    class="w-16 h-16 rounded-xl flex items-center justify-center border-2 shadow-2xl"
                    :style="{
                      backgroundColor: `${productData.color}15`,
                      borderColor: `${productData.color}50`,
                      boxShadow: `0 0 40px ${productData.color}30`
                    }"
                  >
                    <Icon :name="`mdi:${productData.icon}`" class="text-4xl" :style="{ color: productData.color }" />
                  </div>
                  <div class="flex-1">
                    <span
                      class="inline-block px-3 py-1 rounded-full text-xs font-bold mb-2"
                      :style="{
                        backgroundColor: `${productData.color}25`,
                        color: productData.color
                      }"
                    >
                      {{ productData.tagline }}
                    </span>
                    <h1 class="text-2xl font-bold text-white">{{ productData.name }}</h1>
                  </div>
                </div>
                <p class="text-base text-gray-400 leading-relaxed">
                  {{ productData.description }}
                </p>
              </div>

              <!-- Features Grid -->
              <div>
                <h2 class="text-lg font-bold text-white mb-3 flex items-center gap-2">
                  <div class="w-6 h-6 rounded-lg bg-gradient-to-br flex items-center justify-center" :style="{ backgroundImage: `linear-gradient(135deg, ${productData.color}40, ${productData.color}20)` }">
                    <Sparkle :size="14" :stroke-width="2" :style="{ color: productData.color }" />
                  </div>
                  {{ $t('products.products.keyFeatures') }}
                </h2>

                <div class="grid md:grid-cols-2 gap-x-6 gap-y-3">
                  <div
                    v-for="(feature, fIndex) in productData.features"
                    :key="fIndex"
                    v-motion
                    :initial="{ opacity: 0, x: -20 }"
                    :visibleOnce="{ opacity: 1, x: 0, transition: { delay: fIndex * 50, duration: 300 } }"
                    class="flex items-center gap-2 group"
                  >
                    <CheckCircle :size="18" :stroke-width="2" class="flex-shrink-0 group-hover:scale-110 transition-transform" :style="{ color: productData.color }" />
                    <span class="text-sm text-gray-300 group-hover:text-white transition-colors">{{ feature }}</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Pricing Plans -->
            <div>
              <h2 class="text-xl font-bold text-white mb-6 flex items-center gap-2">
                <DollarSign :size="24" :stroke-width="2" :style="{ color: productData.color }" />
                {{ $t('products.pricing.title') }}
              </h2>

              <!-- Billing cycle tabs -->
              <div v-if="availableCycles.length > 1" class="flex flex-wrap gap-2 mb-6">
                <button
                  v-for="cycle in availableCycles"
                  :key="cycle"
                  type="button"
                  class="px-4 py-1.5 rounded-full text-sm font-semibold transition-all duration-200 border"
                  :style="selectedCycle === cycle
                    ? { backgroundColor: productData.color, borderColor: productData.color, color: '#fff' }
                    : { backgroundColor: 'transparent', borderColor: 'rgba(255,255,255,0.15)', color: '#9ca3af' }"
                  @click="selectedCycle = cycle"
                >
                  {{ $t(`products.pricing.cycles.${cycle}`) }}
                </button>
              </div>

              <div class="grid md:grid-cols-3 gap-6 mb-8">
                <div
                  v-for="(plan, tIndex) in productData.plans"
                  :key="plan.tier"
                  v-motion
                  :initial="{ opacity: 0, y: 30 }"
                  :visibleOnce="{ opacity: 1, y: 0, transition: { delay: tIndex * 100, duration: 500 } }"
                  class="relative group"
                >
                  <div
                    class="relative h-full p-6 rounded-2xl border-2 transition-all duration-300 hover:-translate-y-2 flex flex-col"
                    :class="tIndex === 1 ? 'md:scale-105' : ''"
                    :style="{
                      borderColor: tIndex === 1 ? productData.color : 'rgba(255,255,255,0.1)',
                      backgroundColor: tIndex === 1 ? productData.color + '10' : 'rgba(255,255,255,0.03)',
                      boxShadow: tIndex === 1 ? `0 0 50px ${productData.color}35` : 'none'
                    }"
                  >
                    <!-- Popular badge -->
                    <div v-if="tIndex === 1" class="absolute -top-[7px] left-1/2 -translate-x-1/2 z-10">
                      <span class="px-4 py-1 text-xs font-bold rounded-full text-white shadow-lg whitespace-nowrap" :style="{ backgroundColor: productData.color }">
                        {{ $t('products.pricing.popular') }}
                      </span>
                    </div>

                    <!-- Plan name -->
                    <div class="text-xs uppercase tracking-wider mb-2 font-bold" :style="{ color: tIndex === 1 ? productData.color : '#9ca3af' }">
                      {{ plan.tier }}
                    </div>

                    <!-- Price for selected cycle -->
                    <div class="text-4xl font-bold text-white mb-6">
                      {{ planPrice(plan) || '—' }}
                    </div>

                    <!-- Features -->
                    <div class="space-y-2.5 mb-6 flex-1">
                      <div v-for="feat in plan.features" :key="feat" class="flex items-start gap-2">
                        <CheckCircle :size="16" :stroke-width="2" class="flex-shrink-0 mt-0.5" :style="{ color: productData.color }" />
                        <span class="text-xs text-gray-300 leading-relaxed">{{ feat }}</span>
                      </div>
                    </div>

                    <!-- CTA Buttons -->
                    <div class="flex flex-col gap-2">
                      <!-- Add to Cart -->
                      <button
                        class="w-full py-2 rounded-xl text-sm font-semibold transition-all duration-300 flex items-center justify-center gap-1.5"
                        :class="cart.hasItem(plan.pid, selectedCycle)
                          ? 'bg-green-500/20 text-green-400 border border-green-500/30 cursor-default'
                          : 'border border-white/20 text-white hover:border-white/40 hover:bg-white/5 hover:scale-105'"
                        @click="addPlanToCart(plan)"
                      >
                        <Check v-if="cart.hasItem(plan.pid, selectedCycle)" :size="13" :stroke-width="2.5" />
                        <ShoppingCart v-else :size="13" :stroke-width="2" />
                        {{ cart.hasItem(plan.pid, selectedCycle) ? $t('hosting.inCart') : $t('hosting.addToCart') }}
                      </button>

                      <!-- Buy / Trial -->
                      <button
                        class="w-full py-2.5 rounded-xl font-semibold transition-all duration-300 hover:scale-105 text-sm"
                        :class="tIndex === 1 ? 'text-white shadow-lg' : 'border-2 hover:bg-white/5'"
                        :style="{
                          backgroundColor: tIndex === 1 ? productData.color : 'transparent',
                          borderColor: tIndex !== 1 ? productData.color + '60' : undefined,
                          color: tIndex !== 1 ? productData.color : undefined,
                          boxShadow: tIndex === 1 ? `0 8px 30px ${productData.color}40` : undefined
                        }"
                        @click="handlePricingCTA(plan)"
                      >
                        {{ $t('products.pricing.trial') }}
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              <!-- CTA + Guarantee Row -->
              <div class="flex flex-col sm:flex-row items-center justify-between gap-3 sm:gap-4 p-3 sm:p-4 md:p-6 rounded-lg sm:rounded-xl bg-white/5 border border-white/10 w-full">
                <div class="flex items-center gap-2 sm:gap-3 text-center sm:text-left">
                  <ShieldCheck :size="28" :stroke-width="2" class="text-green-400 flex-shrink-0" />
                  <div>
                    <div class="text-xs sm:text-sm font-semibold text-white break-words">{{ $t('products.pricing.guarantee.title') }}</div>
                    <div class="text-[10px] sm:text-xs text-gray-500 break-words">{{ $t('products.pricing.guarantee.subtitle') }}</div>
                  </div>
                </div>

                <div class="flex flex-col sm:flex-row gap-2 sm:gap-3 w-full sm:w-auto">
                  <button
                    class="w-full sm:w-auto px-3 py-2 sm:px-4 sm:py-2.5 md:px-6 md:py-3 rounded-lg sm:rounded-xl font-bold text-white transition-all duration-300 hover:scale-105 hover:shadow-xl relative overflow-hidden group text-xs sm:text-sm md:text-base whitespace-nowrap"
                    :style="{
                      backgroundColor: productData.color,
                      boxShadow: `0 8px 30px ${productData.color}40`
                    }"
                    @click="openDemo(productData.demoUrl)"
                  >
                    <div class="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-700 animate-shine" style="background-size: 200% 100%;" />
                    <span class="relative flex items-center justify-center gap-1.5 sm:gap-2">
                      <PlayCircle :size="20" :stroke-width="2" class="sm:w-5 sm:h-5 md:w-6 md:h-6" />
                      {{ $t('products.pricing.cta.viewDemo') }}
                    </span>
                  </button>

                  <NuxtLink
                    :to="localePath('/contact')"
                    class="w-full sm:w-auto px-3 py-2 sm:px-4 sm:py-2.5 md:px-6 md:py-3 rounded-lg sm:rounded-xl font-semibold border-2 transition-all duration-300 hover:scale-105 hover:bg-white/5 text-xs sm:text-sm md:text-base whitespace-nowrap flex items-center justify-center"
                    :style="{
                      borderColor: productData.color + '60',
                      color: productData.color
                    }"
                  >
                    {{ $t('products.pricing.contactSales') }}
                  </NuxtLink>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-cyan-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-secondary-500/30 pointer-events-none" />
    </section>

    <!-- CTA Section -->
    <SectionsCTA />
  </div>

  <!-- 404 State -->
  <div v-else class="min-h-screen flex items-center justify-center bg-[#0a0a0f]">
    <div class="text-center">
      <Icon name="mdi:package-variant" class="text-6xl text-gray-600 mx-auto mb-4" />
      <h1 class="text-2xl font-bold text-white mb-2">{{ $t('products.notFound') }}</h1>
      <p class="text-gray-400 mb-6">{{ $t('products.notFoundDescription') }}</p>
      <NuxtLink
        :to="localePath('/products')"
        class="inline-flex items-center gap-2 px-6 py-3 rounded-xl font-semibold bg-primary-500 text-white hover:bg-primary-600 transition-colors"
      >
        <Icon name="mdi:arrow-left" />
        {{ $t('common.backToProducts') }}
      </NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Product detail page — data sourced from WHMCS API (gids 3–9)
 * Route: /products/[product]
 */

import { Sparkle, CheckCircle, DollarSign, ShieldCheck, PlayCircle, ShoppingCart, Check } from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'

const { t, locale } = useI18n()
const localePath = useLocalePath()
const route = useRoute()
const { productSchema, breadcrumbSchema, injectSchema } = useSchemaOrg()

const _productsConfig = useRuntimeConfig()
const _productsBaseUrl = _productsConfig.public.baseUrl || 'https://innovayse.com'

/** Route param: e.g. "smartlearn-system" */
const productId = route.params.product as string

// productConfig, currencyByLocale, nameToKey, parseDescription — auto-imported from utils/whmcs.ts

// Fetch all SaaS product groups in one request (gids 3-9, fetched in parallel server-side)
const { data: whmcsRaw } = await useApi('/api/portal/public/products', {
  query: computed(() => ({ lang: locale.value, gids: productGids.join(',') }))
})

/** Find and shape the single product matching the route param */
const productData = computed(() => {
  const list = (whmcsRaw.value as any[] ?? []).filter((p: any) => productGids.includes(Number(p.gid)))

  // Primary: find by exact slug or normalized name
  let w = list.find((p: any) => {
    const k = (p.slug as string)?.trim() || nameToKey(p.name as string)
    return k === productId
  })

  // Fallback: use productGidToKey to locate by gid when WHMCS slug differs
  if (!w) {
    const targetGid = Number(Object.entries(productGidToKey).find(([, v]) => v === productId)?.[0] ?? 0)
    if (targetGid) {
      const group = list.filter((p: any) => Number(p.gid) === targetGid)
      w = group.find((p: any) => p.is_featured === 'on') ?? group[0] ?? null
    }
  }

  if (!w) return null

  // Use productGidToKey as canonical key; fall back to slug or name normalization
  const key = productGidToKey[Number(w.gid)] ?? (w.slug as string)?.trim() ?? nameToKey(w.name as string)
  const cfg = productConfig[key] ?? { icon: 'cube', color: '#6366f1', demoUrl: '', learnMoreUrl: '' }

  const preferred = currencyByLocale[locale.value] ?? 'USD'

  // group_translations may only be on the parent product — find it in the group
  const wGt = list.filter((p: any) => Number(p.gid) === Number(w.gid))
    .find((p: any) => p.group_translations?.translated_name) ?? w

  // Features: prefer structured group_features from the gt-bearing product
  const groupFeatures = getGroupFeatures(wGt)
  const { summary: tSummary, features: tFeatures } = parseDescription(w.translated_description || '')
  const { features: enFeatures } = parseDescription(w.description || '')
  const productFeatures = groupFeatures.length > 0 ? groupFeatures : (tFeatures.length > 0 ? tFeatures : enFeatures)

  // Child plans = same gid, different slug (e.g. Starter / Professional / Enterprise)
  const childPlans = list.filter((p: any) =>
    Number(p.gid) === Number(w.gid) && (p.slug as string)?.trim() !== key
  )

  const cycleKeys = ['monthly', 'quarterly', 'semiannually', 'annually', 'biennially', 'triennially'] as const

  const plans = childPlans.map((plan: any) => {
    const planCurrency = plan.pricing
      ? (plan.pricing[preferred] ?? Object.values(plan.pricing)[0] as any)
      : null
    const { features: pTFeatures } = parseDescription(plan.translated_description || '')
    const { features: pEnFeatures } = parseDescription(plan.description || '')
    return {
      pid: plan.pid as number,
      product_url: (plan.product_url as string) || '',
      tier: plan.translated_name || plan.name,
      currency: planCurrency as Record<string, string> | null,
      prefix: planCurrency?.prefix ?? '',
      features: pTFeatures.length > 0 ? pTFeatures : pEnFeatures
    }
  })

  return {
    ...cfg,
    id: key,
    pid: w.pid,
    name: getGt(wGt, 'name') || w.name,
    tagline: getGt(wGt, 'headline') || getGt(wGt, 'tagline'),
    description: w.translated_shortdescription || w.shortdescription || tSummary || getGt(wGt, 'headline') || w.translated_description,
    features: productFeatures,
    plans,
    cycleKeys
  }
})

const allCycleKeys = ['monthly', 'quarterly', 'semiannually', 'annually', 'biennially', 'triennially'] as const
type CycleKey = typeof allCycleKeys[number]

const availableCycles = computed(() =>
  allCycleKeys.filter(k =>
    productData.value?.plans.some(p =>
      p.currency?.[k] && p.currency[k] !== '-1.00' && p.currency[k] !== '0.00'
    )
  )
)

const selectedCycle = ref<CycleKey>('monthly')

watch(availableCycles, (cycles) => {
  if (cycles.length && !cycles.includes(selectedCycle.value)) {
    selectedCycle.value = cycles[0] as CycleKey
  }
}, { immediate: true })

function planPrice(plan: { currency: Record<string, string> | null; prefix: string }): string {
  const val = plan.currency?.[selectedCycle.value]
  if (!val || val === '-1.00' || val === '0.00') return ''
  return `${plan.prefix}${val}`
}

// Breadcrumb items
const breadcrumbItems = computed(() => [
  { label: t('common.products'), to: localePath('/products') },
  { label: productData.value?.name || '' }
])

const cart = useCartStore()
onMounted(() => cart.init())

const openDemo = (url?: string) => {
  if (url) window.open(url, '_blank')
}

const handlePricingCTA = (plan: { product_url: string; pid: number }) => {
  const url = plan.product_url || `${useRuntimeConfig().public.whmcsUrl}/cart.php?a=add&pid=${plan.pid}&billingcycle=${selectedCycle.value}`
  window.open(url, '_blank')
}

/** Add a plan to cart with the currently-selected billing cycle */
function addPlanToCart(plan: { pid: number; tier: string; currency: Record<string, string> | null; prefix: string }) {
  const cycleKey = selectedCycle.value
  const rawPrice = plan.currency?.[cycleKey] ?? '0'
  const cycleLabel = t(`hosting.cycles.${cycleKey}`)
  const price = (rawPrice && rawPrice !== '-1.00' && rawPrice !== '0.00')
    ? `${plan.prefix}${rawPrice}`
    : t('hosting.custom')

  cart.addItem({
    pid: plan.pid,
    name: plan.tier,
    billingcycle: cycleKey,
    cycleLabel,
    price,
    prefix: plan.prefix,
    rawPrice
  })
}

// SEO
const _productPath = computed(() => `/products/${productId}`)
const _productCanonical = computed(() =>
  locale.value === 'en'
    ? `${_productsBaseUrl}${_productPath.value}`
    : `${_productsBaseUrl}/${locale.value}${_productPath.value}`
)

watchEffect(() => {
  if (productData.value) {
    const fullTitle = `${productData.value.name} — ${productData.value.tagline} | Innovayse`
    
    useSeoMeta({
      title: fullTitle,
      description: productData.value.description,
      ogTitle: fullTitle,
      ogDescription: productData.value.description,
      ogImage: `${_productsBaseUrl}/og-image.jpg`,
      ogType: 'website',
      ogUrl: _productCanonical.value,
      twitterCard: 'summary_large_image',
      twitterTitle: fullTitle,
      twitterDescription: productData.value.description,
      twitterImage: `${_productsBaseUrl}/og-image.jpg`
    })

    injectSchema([
      productSchema({ name: productData.value.name, description: productData.value.description, url: _productCanonical.value }),
      breadcrumbSchema([
        { name: 'Home', url: _productsBaseUrl },
        { name: t('common.products'), url: `${_productsBaseUrl}/products` },
        { name: productData.value.name, url: _productCanonical.value }
      ])
    ])
  }
})

useHead(() => ({
  link: [
    { rel: 'canonical', href: _productCanonical.value },
    { rel: 'alternate', hreflang: 'en', href: `${_productsBaseUrl}${_productPath.value}` },
    { rel: 'alternate', hreflang: 'ru', href: `${_productsBaseUrl}/ru${_productPath.value}` },
    { rel: 'alternate', hreflang: 'hy', href: `${_productsBaseUrl}/hy${_productPath.value}` },
    { rel: 'alternate', hreflang: 'x-default', href: `${_productsBaseUrl}${_productPath.value}` }
  ]
}))
</script>

<style scoped>
@keyframes shine {
  0% {
    transform: translateX(-100%);
  }
  100% {
    transform: translateX(100%);
  }
}

.animate-shine {
  animation: shine 3s linear infinite;
}
</style>
