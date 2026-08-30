<template>
  <div>
    <!-- Hero Section -->
    <section class="relative py-12 md:py-28 bg-[#0a0a0f] overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[400px] h-[400px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" class="w-full h-full" />
        </div>
        <div class="absolute top-1/4 left-1/3 w-2 h-2 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 0.5s;" />
        <div class="absolute bottom-1/3 right-1/4 w-3 h-3 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 1.5s;" />
      </div>
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />

      <div class="container-custom relative z-10">
        <!-- Back -->
        <NuxtLink
          :to="localePath('/hosting')"
          class="inline-flex items-center gap-2 text-gray-400 hover:text-white transition-colors mb-10"
        >
          <ArrowLeft :size="16" :stroke-width="2" />
          {{ $t('hosting.backToPlans') }}
        </NuxtLink>

        <!-- Loading -->
        <div v-if="pending" class="space-y-4">
          <div class="h-8 w-48 rounded-lg bg-white/5 animate-pulse" />
          <div class="h-16 w-80 rounded-xl bg-white/5 animate-pulse" />
          <div class="h-5 w-96 rounded-lg bg-white/5 animate-pulse" />
          <div class="h-14 w-40 rounded-xl bg-white/5 animate-pulse mt-4" />
        </div>

        <!-- Not found -->
        <div v-else-if="!currentPlan" class="text-center py-20">
          <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
          <p class="text-gray-400 mb-4">{{ $t('hosting.planNotFound') }}</p>
          <NuxtLink :to="localePath('/hosting')" class="text-primary-400 hover:text-primary-300 transition-colors">
            {{ $t('hosting.viewAllPlans') }}
          </NuxtLink>
        </div>

        <!-- Plan hero -->
        <div
          v-else
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :enter="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        >
          <!-- Popular badge -->
          <div v-if="currentPlan.is_featured === 'on'" class="mb-4">
            <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-xs font-bold rounded-full uppercase tracking-wider border border-primary-500/30">
              {{ $t('hosting.mostPopular') }}
            </span>
          </div>

          <!--
            No tagline line here: `ProductDto` has no headline/tagline field, so the paragraph
            this replaced never rendered for any plan. Restoring it needs a backend field.
          -->

          <!-- Name -->
          <h1 class="text-4xl md:text-6xl font-bold text-white mb-4 leading-tight">
            {{ currentPlan.name }}
          </h1>

          <!--
            Summary parsed out of `description`, not the `shortdescription` this replaced —
            that field does not exist, so the copy the catalogue does send never appeared here.
            Interpolated rather than `v-html`: the raw field is operator-authored HTML that no
            layer sanitises, and `parseDescription` has already stripped its tags.
          -->
          <p v-if="planSummary" class="text-gray-400 text-lg mb-8 max-w-2xl">
            {{ planSummary }}
          </p>

          <!-- Price + CTA -->
          <div class="flex flex-wrap items-end gap-6">
            <div class="flex items-baseline gap-1.5">
              <div class="text-5xl font-bold text-white">{{ getPlanPrice(currentPlan) }}</div>
              <div class="text-gray-500 text-sm">{{ $t('hosting.perMonthFull') }}</div>
            </div>
            <button
              type="button"
              class="px-8 py-4 rounded-xl font-bold text-white text-lg bg-gradient-to-r from-primary-600 to-primary-500 hover:from-primary-500 hover:to-primary-400 transition-all duration-300 hover:scale-[1.02] shadow-lg shadow-primary-500/30 flex items-center gap-2"
              @click="handleAddToCart"
            >
              <ShoppingCart :size="20" :stroke-width="2" />
              {{ cart.hasItem(currentPlan.pid, selectedCycle) ? $t('hosting.inCart') : $t('hosting.orderNow') }}
            </button>
          </div>
        </div>
      </div>
    </section>

    <!-- Content Section -->
    <section v-if="currentPlan" class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-secondary-500/5 via-transparent to-primary-500/5" />
        <div class="absolute top-1/3 left-1/4 w-[400px] h-[400px] bg-primary-500/10 rounded-full blur-[130px] animate-blob animation-delay-4000" />
        <div class="absolute bottom-1/4 right-1/4 w-[350px] h-[350px] bg-secondary-500/12 rounded-full blur-[120px] animate-blob" />
        <div class="absolute top-0 left-1/3 w-px h-full bg-gradient-to-b from-transparent via-primary-500/10 to-transparent" />
        <div class="absolute top-0 right-1/3 w-px h-full bg-gradient-to-b from-transparent via-secondary-500/10 to-transparent" />
        <div class="absolute inset-0 opacity-[0.015]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" class="w-full h-full" />
        </div>
      </div>
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/25 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/25 pointer-events-none" />

      <div class="container-custom relative z-10 max-w-4xl mx-auto space-y-6">

        <!-- Billing cycles -->
        <UiCard
          v-if="hasPricing"
          v-motion
          :initial="{ opacity: 0, y: 24 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          :title="$t('hosting.billingCycles')"
        >
          <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
            <div
              v-for="cycle in billingCycles"
              :key="cycle.key"
              class="p-3 rounded-xl border text-center cursor-pointer transition-all duration-200"
              :class="selectedCycle === cycle.key
                ? 'border-primary-500 bg-primary-500/10 text-white'
                : 'border-white/10 text-gray-400 hover:border-white/20 hover:text-white'"
              @click="selectedCycle = cycle.key"
            >
              <div class="text-xs font-semibold">{{ cycle.label }}</div>
              <div class="text-base font-bold mt-1">{{ cycle.price }}</div>
            </div>
          </div>
        </UiCard>

        <!-- Features -->
        <UiCard
          v-if="planFeatures.length"
          v-motion
          :initial="{ opacity: 0, y: 24 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 100, duration: 500 } }"
          padding="xl"
        >
          <h2 class="text-xl font-bold text-white mb-6">{{ $t('hosting.includedFeatures') }}</h2>
          <ul class="grid md:grid-cols-2 gap-3">
            <li
              v-for="(feature, index) in planFeatures"
              :key="feature"
              v-motion
              :initial="{ opacity: 0, x: -10 }"
              :visibleOnce="{ opacity: 1, x: 0, transition: { delay: index * 50, duration: 400 } }"
              class="flex items-center gap-3"
            >
              <CheckCircle :size="16" :stroke-width="2" class="text-primary-400 flex-shrink-0" />
              <span class="text-gray-300 text-sm">{{ feature }}</span>
            </li>
          </ul>
        </UiCard>

        <!-- Bottom CTA -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
          class="text-center pt-4"
        >
          <button
            type="button"
            class="inline-flex items-center gap-2 px-10 py-4 rounded-xl font-bold text-white text-lg bg-gradient-to-r from-primary-600 to-primary-500 hover:from-primary-500 hover:to-primary-400 transition-all duration-300 hover:scale-[1.02] shadow-lg shadow-primary-500/30"
            @click="handleAddToCart"
          >
            <ShoppingCart :size="20" :stroke-width="2" />
            {{ cart.hasItem(currentPlan.pid, selectedCycle) ? $t('hosting.inCart') : $t('hosting.orderNow') }}
          </button>
          <p class="text-gray-500 text-sm mt-3">
            <Lock :size="12" :stroke-width="2" class="inline mr-1" />
            {{ $t('cart.secureCheckout') }}
          </p>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, AlertCircle, CheckCircle, ShoppingCart, Lock } from 'lucide-vue-next'
import { useCartStore } from '~/stores/cart'
import { useCatalogApi } from '~/composables/apis/useCatalogApi'

const { t: $t, locale } = useI18n()
const route = useRoute()
const localePath = useLocalePath()
const cart = useCartStore()

const currencyByLocale: Record<string, string> = { en: 'USD', hy: 'AMD', ru: 'RUB' }

// Straight from the API composable rather than through a store: this page fetches once and
// owns the result alone, which is the named exception to component -> store -> api. A store
// would also cost the SSR dedup and the locale re-fetch that `useApi()` gives for free, and
// this page is server-rendered and indexed.
const { data: plans, pending } = await useCatalogApi().loadProducts(
  () => ({ gid: 1 })
)

const selectedCycle = ref('monthly')

function planSlug(name: string): string {
  const clean = (name || '')
    .replace(/Businnes/g, 'Business')
    .replace(/businnes/g, 'business')
  return clean.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
}

const currentPlan = computed(() => {
  if (!plans.value) return null
  const param = String(route.params.plan)
  return (plans.value as any[]).find(
    (p: any) => String(p.pid) === param || p.slug === param || planSlug(p.name) === param
  ) ?? null
})

/**
 * Summary paragraph and feature lines parsed out of the plan's `description`.
 *
 * Split on newlines this used to be, which is wrong for this catalogue: the copy is HTML
 * (`✔ 600 MB Disk Space <br />`, `<strong>…</strong>`), so a raw split printed the markup
 * inside the feature list and merged a whole prose paragraph into a single "feature".
 * `parseDescription` (utils/whmcs.ts, auto-imported) unwraps the tags, splits the leading
 * prose off as `summary`, and strips the bullet markers.
 */
const parsedPlan = computed(() => parseDescription(currentPlan.value?.description || ''))

/** Leading prose of the plan description; empty for plans whose copy is bullets only. */
const planSummary = computed(() => parsedPlan.value.summary)

/** Bullet lines of the plan description, markers stripped. */
const planFeatures = computed(() => parsedPlan.value.features)

function getPlanPrice(plan: any): string {
  const preferred = currencyByLocale[locale.value] ?? 'USD'
  const currency = plan.pricing[preferred] ?? Object.values(plan.pricing)[0] as any
  if (!currency) return $t('hosting.custom')
  const amount = currency[selectedCycle.value] || currency.monthly
  if (!amount || amount === '-1.00' || amount === '0.00') return $t('hosting.custom')
  return `${currency.prefix}${amount}`
}

function handleAddToCart() {
  if (!currentPlan.value) return
  const plan = currentPlan.value
  const preferred = currencyByLocale[locale.value] ?? 'USD'
  const pricing = plan.pricing[preferred] ?? Object.values(plan.pricing)[0] as any
  
  // Determine best cycle: if selected cycle has price > 0, use it. 
  // Otherwise, look for 'free' or 0.00 pricing.
  let amount = pricing[selectedCycle.value] || pricing.monthly
  let finalCycle = selectedCycle.value
  
  if (!amount || amount === '-1.00' || amount === '0.00') {
    // Check if it's a free plan or has no price for the selected cycle
    if (plan.name.toLowerCase().includes('free')) {
      finalCycle = 'free'
      amount = '0.00'
    } else {
      // Fallback to monthly or first available
      amount = pricing.monthly || '0.00'
    }
  }
  
  cart.addItem({
    pid: plan.pid,
    name: plan.name,
    billingcycle: finalCycle,
    cycleLabel: finalCycle === 'free' ? $t('hosting.cycles.free') : $t(`hosting.cycles.${finalCycle}`),
    price: (finalCycle === 'free' || amount === '0.00') ? $t('hosting.custom') : `${pricing.prefix}${amount}`,
    prefix: pricing.prefix,
    rawPrice: amount
  })
}

const billingCycles = computed(() => {
  const plan = currentPlan.value
  if (!plan?.pricing) return []

  const preferred = currencyByLocale[locale.value] ?? 'USD'
  const currency = plan.pricing[preferred] ?? Object.values(plan.pricing)[0] as any
  if (!currency) return []

  const cycleKeys = ['monthly', 'quarterly', 'semiannually', 'annually', 'biennially', 'triennially'] as const
  return cycleKeys
    .filter(key => currency[key] && currency[key] !== '-1.00' && currency[key] !== '0.00')
    .map(key => ({
      key,
      label: $t(`hosting.cycles.${key}`),
      price: `${currency.prefix}${currency[key]}`
    }))
})

const hasPricing = computed(() => billingCycles.value.length > 0)

// Auto-select first available cycle
watch(billingCycles, (cycles) => {
  const first = cycles[0]
  if (first && !cycles.find(c => c.key === selectedCycle.value)) {
    selectedCycle.value = first.key
  }
}, { immediate: true })

// Dynamic SEO based on plan name
const planSlugParam = computed(() => String(route.params.plan))

useSeoMeta({
  title: () => currentPlan.value
    ? `${currentPlan.value.name} - Web Hosting Plan | Innovayse`
    : $t('seo.hostingPlan.title'),
  description: () => planSummary.value || $t('seo.hostingPlan.description'),
  keywords: $t('seo.hostingPlan.keywords'),
  ogTitle: () => currentPlan.value
    ? `${currentPlan.value.name} - Web Hosting Plan | Innovayse`
    : $t('seo.hostingPlan.title'),
  ogDescription: () => planSummary.value || $t('seo.hostingPlan.description'),
  ogType: 'website',
  twitterCard: 'summary_large_image',
  twitterTitle: () => currentPlan.value
    ? `${currentPlan.value.name} | Innovayse`
    : $t('seo.hostingPlan.title'),
  twitterDescription: () => planSummary.value || $t('seo.hostingPlan.description')
})

const config = useRuntimeConfig()
const _baseUrl = config.public.baseUrl || 'https://innovayse.com'

useHead(() => ({
  link: [
    {
      rel: 'canonical',
      href: locale.value === 'en'
        ? `${_baseUrl}/hosting/${planSlugParam.value}`
        : `${_baseUrl}/${locale.value}/hosting/${planSlugParam.value}`
    },
    { rel: 'alternate', hreflang: 'en', href: `${_baseUrl}/hosting/${planSlugParam.value}` },
    { rel: 'alternate', hreflang: 'ru', href: `${_baseUrl}/ru/hosting/${planSlugParam.value}` },
    { rel: 'alternate', hreflang: 'hy', href: `${_baseUrl}/hy/hosting/${planSlugParam.value}` },
    { rel: 'alternate', hreflang: 'x-default', href: `${_baseUrl}/hosting/${planSlugParam.value}` }
  ]
}))
</script>
