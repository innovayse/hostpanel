<template>
  <div class="min-h-screen bg-[#0a0a0f]">
    <!-- Hero -->
    <section class="relative py-20 md:py-32 overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 right-1/4 w-[500px] h-[500px] rounded-full blur-[140px] animate-blob" :style="{ backgroundColor: cfg.color + '20' }" />
        <div class="absolute bottom-0 left-1/3 w-[400px] h-[400px] bg-cyan-500/10 rounded-full blur-[130px] animate-blob animation-delay-2000" />
      </div>

      <div class="container-custom relative z-10 text-center">
        <!-- Product icon -->
        <div
          class="w-20 h-20 rounded-2xl flex items-center justify-center border-2 shadow-2xl mx-auto mb-6"
          :style="{ backgroundColor: cfg.color + '15', borderColor: cfg.color + '50', boxShadow: `0 0 60px ${cfg.color}30` }"
        >
          <Icon :name="`mdi:${cfg.icon}`" class="text-5xl" :style="{ color: cfg.color }" />
        </div>

        <!-- Badge -->
        <div class="inline-flex items-center gap-2 px-4 py-2 mb-6 rounded-full border backdrop-blur-sm" :style="{ borderColor: cfg.color + '30', backgroundColor: cfg.color + '10' }">
          <Zap :size="14" :stroke-width="2" :style="{ color: cfg.color }" />
          <span class="text-sm font-medium text-gray-300">{{ $t('trial.badge') }}</span>
        </div>

        <h1 class="text-4xl md:text-6xl font-bold text-white mb-4 leading-tight">
          {{ $t('trial.title', { product: cfg.name }) }}
        </h1>
        <p class="text-lg text-gray-400 mb-10 max-w-2xl mx-auto">
          {{ $t('trial.subtitle') }}
        </p>

        <!-- Trial CTA -->
        <div class="flex flex-col sm:flex-row items-center justify-center gap-4">
          <a
            :href="trialUrl"
            target="_blank"
            rel="noopener"
            class="group relative px-8 py-4 rounded-xl font-bold text-white overflow-hidden transition-all duration-300 hover:scale-105 hover:shadow-2xl"
            :style="{ backgroundColor: cfg.color, boxShadow: `0 8px 30px ${cfg.color}40` }"
          >
            <div class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-700">
              <div class="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent animate-shine" style="background-size: 200% 100%;" />
            </div>
            <span class="relative flex items-center gap-2">
              <Rocket :size="20" :stroke-width="2" />
              {{ $t('trial.cta') }}
            </span>
          </a>

          <NuxtLink
            :to="localePath(cfg.learnMoreUrl)"
            class="px-8 py-4 rounded-xl font-semibold border-2 text-gray-300 hover:text-white hover:bg-white/5 transition-all duration-300"
            :style="{ borderColor: cfg.color + '40' }"
          >
            {{ $t('trial.learnMore') }}
          </NuxtLink>
        </div>

        <!-- No CC note -->
        <p class="mt-4 text-sm text-gray-500 flex items-center justify-center gap-2">
          <ShieldCheck :size="16" :stroke-width="2" class="text-green-400" />
          {{ $t('trial.noCc') }}
        </p>
      </div>
    </section>

    <!-- Features -->
    <section v-if="features.length > 0" class="py-16 bg-[#0d0d12]">
      <div class="container-custom max-w-4xl">
        <h2 class="text-2xl md:text-3xl font-bold text-white text-center mb-10">
          {{ $t('trial.featuresTitle', { product: cfg.name }) }}
        </h2>
        <div class="grid md:grid-cols-2 gap-4">
          <div
            v-for="(feature, i) in features"
            :key="i"
            v-motion
            :initial="{ opacity: 0, x: -20 }"
            :visibleOnce="{ opacity: 1, x: 0, transition: { delay: i * 60, duration: 300 } }"
            class="flex items-center gap-3 p-4 rounded-xl bg-white/5 border border-white/10"
          >
            <CheckCircle :size="18" :stroke-width="2" :style="{ color: cfg.color }" class="flex-shrink-0" />
            <span class="text-sm text-gray-300">{{ feature }}</span>
          </div>
        </div>
      </div>
    </section>

    <!-- Trial perks -->
    <section class="py-16">
      <div class="container-custom max-w-4xl">
        <div class="grid md:grid-cols-3 gap-6">
          <div
            v-for="(perk, i) in perks"
            :key="perk.key"
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: i * 100, duration: 400 } }"
            class="text-center p-6 rounded-2xl bg-white/5 border border-white/10"
          >
            <component :is="perk.icon" :size="32" :stroke-width="2" class="mx-auto mb-3" :style="{ color: cfg.color }" />
            <div class="font-semibold text-white mb-1">{{ $t(`trial.perks.${perk.key}.title`) }}</div>
            <div class="text-sm text-gray-500">{{ $t(`trial.perks.${perk.key}.desc`) }}</div>
          </div>
        </div>

        <!-- Bottom CTA -->
        <div class="mt-12 text-center">
          <a
            :href="trialUrl"
            target="_blank"
            rel="noopener"
            class="inline-flex items-center gap-2 px-10 py-4 rounded-xl font-bold text-white transition-all duration-300 hover:scale-105"
            :style="{ backgroundColor: cfg.color, boxShadow: `0 8px 30px ${cfg.color}40` }"
          >
            <Rocket :size="20" :stroke-width="2" />
            {{ $t('trial.cta') }}
          </a>
          <p class="mt-3 text-sm text-gray-500">{{ $t('trial.noCc') }}</p>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { Zap, Rocket, ShieldCheck, CheckCircle, Clock, CreditCard, Headphones } from 'lucide-vue-next'

const route = useRoute()
const { t, locale } = useI18n()
const localePath = useLocalePath()

const productSlug = route.params.product as string

// Get config from utils/whmcs.ts (auto-imported)
const cfg = computed(() => productConfig[productSlug] ?? {
  name: productSlug,
  icon: 'cube',
  color: '#6366f1',
  learnMoreUrl: '/products',
  demoUrl: undefined
})

// Fetch product data from API to get features and trial product URL
const { data: products } = await useApi('/api/portal/public/products', {
  query: computed(() => {
    const gid = Object.entries(productGidToKey).find(([, key]) => key === productSlug)?.[0]
    return { lang: locale.value, gid: gid ? Number(gid) : undefined }
  }),
  default: () => []
})

// Find the free trial plan
const trialPlan = computed(() =>
  (products.value as any[]).find((p: any) => p.paytype === 'free') ?? (products.value as any[])[0]
)

const trialUrl = computed(() => trialPlan.value?.product_url || cfg.value.learnMoreUrl)

// Features from group_features or description
const features = computed(() => {
  const p = trialPlan.value
  if (!p) return []
  const gf: any[] = p.group_features ?? []
  const groupFeatures = gf.map((f: any) => f.translated_feature || f.feature).filter(Boolean)
  if (groupFeatures.length > 0) return groupFeatures
  const { features: f } = parseDescription(p.translated_description || p.description || '')
  return f
})

const perks = [
  { key: 'noCc', icon: CreditCard },
  { key: 'duration', icon: Clock },
  { key: 'support', icon: Headphones }
]

useSeo({
  title: t('trial.seo.title', { product: cfg.value.name }),
  description: t('trial.seo.description', { product: cfg.value.name }),
  path: `/trial/${productSlug}`
})
</script>
