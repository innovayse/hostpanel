<template>
  <section class="py-20 bg-[#0a0a0f] relative overflow-hidden">
    <!-- Background decorations -->
    <div class="absolute inset-0 pointer-events-none">
      <!-- Subtle gradient overlay -->
      <div class="absolute inset-0 bg-gradient-to-tr from-secondary-500/5 via-transparent to-cyan-500/5" />

      <!-- Soft glow -->
      <div class="absolute top-0 left-0 w-1/2 h-1/2 bg-secondary-500/10 rounded-full blur-[150px]" />
      <div class="absolute bottom-0 right-0 w-1/3 h-1/3 bg-cyan-500/10 rounded-full blur-[120px]" />

      <!-- Floating particles -->
      <div class="absolute top-1/4 left-1/3 w-2 h-2 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 0.3s;" />
      <div class="absolute bottom-1/3 right-1/4 w-3 h-3 bg-cyan-400/40 rounded-full animate-float" style="animation-delay: 1.8s;" />
      <div class="absolute top-1/2 right-1/3 w-2 h-2 bg-primary-400/50 rounded-full animate-float" style="animation-delay: 2.5s;" />

      <!-- Decorative lines -->
      <div class="absolute top-1/3 left-0 w-full h-px bg-gradient-to-r from-transparent via-primary-500/10 to-transparent" />
    </div>

    <div class="container-custom relative z-10">
      <!-- Section header -->
      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="text-center mb-16"
      >
        <span class="inline-block px-4 py-1 bg-secondary-500/10 text-secondary-400 text-sm font-medium rounded-full mb-4">
          {{ $t('products.products.badge') }}
        </span>
        <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
          {{ $t('products.products.title') }}
        </h2>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('products.products.subtitle') }}
        </p>
      </div>

      <!-- Products grid -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <div
          v-for="(product, index) in products"
          :key="product.id"
          v-motion
          :initial="{ opacity: 0, y: 50 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
          class="group relative rounded-2xl overflow-hidden min-h-[450px]"
        >
          <!-- Product image background -->
          <NuxtImg
            :src="product.image"
            :alt="product.name"
            width="800"
            height="600"
            format="webp"
            quality="80"
            sizes="xs:100vw sm:100vw md:50vw lg:33vw"
            loading="lazy"
            class="absolute inset-0 w-full h-full object-cover transition-transform duration-700 group-hover:scale-110 pointer-events-none"
          />

          <!-- Dark gradient overlay -->
          <div class="absolute inset-0 bg-gradient-to-t from-black via-black/70 to-black/30 group-hover:from-black group-hover:via-black/80 transition-all duration-500 pointer-events-none" />

          <!-- Decorative glow on hover -->
          <div
            class="absolute inset-0 opacity-0 group-hover:opacity-30 transition-opacity duration-500 pointer-events-none"
            :style="{ background: `radial-gradient(circle at 50% 100%, ${product.color}40 0%, transparent 60%)` }"
          />

          <!-- Animated mesh overlay -->
          <div class="absolute inset-0 opacity-0 group-hover:opacity-10 transition-opacity duration-500 pointer-events-none">
            <div style="background-image: radial-gradient(circle, rgba(255,255,255,0.3) 1px, transparent 1px); background-size: 20px 20px;" class="w-full h-full" />
          </div>

          <!-- Card content -->
          <div class="relative h-full p-6 flex flex-col">
            <!-- Featured badge (for first product) -->
            <div v-if="index === 0" class="absolute top-4 left-4">
              <span class="px-3 py-1 bg-gradient-to-r from-cyan-500 to-primary-500 text-white text-xs font-bold rounded-full shadow-lg shadow-cyan-500/30 animate-pulse-glow">
                {{ $t('products.products.featured') }}
              </span>
            </div>

            <!-- Clickable content area -->
            <NuxtLink :to="localePath(product.learnMoreUrl)" class="flex flex-col flex-1 cursor-pointer">
              <!-- Product icon -->
              <div
                class="w-14 h-14 rounded-xl flex items-center justify-center mb-4 mt-8 backdrop-blur-sm transition-all duration-500 group-hover:scale-110 group-hover:rotate-3 border relative"
                :style="{
                  backgroundColor: `${product.color}25`,
                  borderColor: `${product.color}50`,
                  boxShadow: `0 0 30px ${product.color}30`
                }"
              >
                <!-- Icon glow ring -->
                <div class="absolute inset-0 rounded-xl opacity-0 group-hover:opacity-100 transition-opacity duration-500" :style="{ boxShadow: `0 0 20px ${product.color}50` }" />

                <Icon
                  :name="`mdi:${product.icon}`"
                  class="relative text-3xl transition-all duration-500"
                  :style="{ color: product.color }"
                />
              </div>

              <!-- Product name -->
              <h3 class="text-xl font-bold text-white mb-2 group-hover:text-cyan-300 transition-colors duration-300">
                {{ product.name }}
              </h3>

              <!-- Tagline badge -->
              <span
                class="inline-block px-3 py-1 rounded-full text-xs font-medium mb-3 w-fit backdrop-blur-sm"
                :style="{
                  backgroundColor: `${product.color}20`,
                  color: product.color
                }"
              >
                {{ product.tagline }}
              </span>

              <!-- Description -->
              <p class="text-gray-400 text-sm mb-4 line-clamp-3 group-hover:text-gray-300 transition-colors duration-300">
                {{ product.description }}
              </p>

              <!-- Features list -->
              <div class="flex-1 space-y-2 mb-4 opacity-0 group-hover:opacity-100 transition-all duration-300 delay-100">
                <div v-for="(feature, fIndex) in product.features" :key="fIndex" class="flex items-center gap-2">
                  <CheckCircle :size="14" :stroke-width="2" class="flex-shrink-0" :style="{ color: product.color }" />
                  <span class="text-xs text-gray-400">{{ feature }}</span>
                </div>
              </div>
            </NuxtLink>

            <!-- CTA button -->
            <div class="mt-auto">
              <component
                :is="product.demoUrl ? 'a' : 'NuxtLink'"
                v-bind="product.demoUrl
                  ? { href: product.demoUrl, target: '_blank', rel: 'noopener noreferrer' }
                  : { to: localePath(product.learnMoreUrl) }"
                class="w-full py-2 sm:py-2.5 md:py-3 rounded-lg sm:rounded-xl font-medium transition-all duration-300 flex items-center justify-center gap-1.5 sm:gap-2 border backdrop-blur-sm group-hover:shadow-lg relative overflow-hidden cursor-pointer text-sm sm:text-base"
                :style="{
                  backgroundColor: `${product.color}15`,
                  borderColor: `${product.color}40`,
                  color: product.color
                }"
              >
                <!-- Hover background effect -->
                <div
                  class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-300 rounded-lg sm:rounded-xl"
                  :style="{ backgroundColor: `${product.color}25` }"
                />

                <!-- Shine effect -->
                <div class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-700">
                  <div class="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent translate-x-[-200%] group-hover:translate-x-[200%] transition-transform duration-1000" />
                </div>

                <span class="relative">{{ product.demoUrl ? $t('products.products.viewDemo') : $t('products.pricing.cta.learnMore') }}</span>
                <ArrowRight :size="18" :stroke-width="2" class="relative group-hover:translate-x-1 transition-transform" />
              </component>
            </div>
          </div>

          <!-- Animated border on hover -->
          <div
            class="absolute inset-0 rounded-2xl opacity-0 group-hover:opacity-100 transition-all duration-500 pointer-events-none"
            :style="{ boxShadow: `inset 0 0 0 1px ${product.color}40` }"
          />

          <!-- Corner accents with rounded borders -->
          <div class="absolute top-0 left-0 w-16 h-16 border-l-2 border-t-2 rounded-tl-2xl transform scale-0 group-hover:scale-100 transition-transform duration-500 origin-top-left pointer-events-none" :style="{ borderColor: product.color + '60' }" />
          <div class="absolute bottom-0 right-0 w-16 h-16 border-r-2 border-b-2 rounded-br-2xl transform scale-0 group-hover:scale-100 transition-transform duration-500 origin-bottom-right pointer-events-none" :style="{ borderColor: product.color + '60' }" />
        </div>
      </div>

      <!-- View all button -->
      <div class="text-center mt-12 sm:mt-14 md:mt-16">
        <UiButton
          variant="outline"
          size="md"
          class="group border-gray-700 hover:border-secondary-500 hover:shadow-lg hover:shadow-secondary-500/20 transition-all duration-300 !px-4 !py-2 sm:!px-5 sm:!py-2.5 md:!px-6 md:!py-3 !text-sm sm:!text-base md:!text-lg"
          @click="navigateTo(localePath('/products'))"
        >
          <span>{{ $t('products.products.viewAll') }}</span>
          <ArrowRight :size="20" :stroke-width="2" class="ml-1.5 sm:ml-2 group-hover:translate-x-1 transition-transform" />
        </UiButton>
      </div>
    </div>

    <!-- Section corner decorations -->
    <div class="absolute top-0 right-0 w-40 h-40 border-r-2 border-t-2 border-cyan-500/20 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-40 h-40 border-l-2 border-b-2 border-secondary-500/20 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
/**
 * Products section with image-based cards.
 * Data: WHMCS API (name, tagline, description, features) + lib/data.ts (image) + productConfig (icon, color, demoUrl)
 */

import { CheckCircle, ArrowRight } from 'lucide-vue-next'
import { products as visualData } from '~/lib/data'

const { t, locale } = useI18n()
const localePath = useLocalePath()

// image map: product id → local image path
const imageMap = Object.fromEntries(visualData.map(p => [p.id, p.image]))

// Hosting visual config (not in productConfig)
const hostingVisual = {
  icon: 'cloud-check',
  color: '#0ea5e9',
  image: '/images/products/hosting.jpg',
  demoUrl: 'https://hosting.innovayse.com',
  learnMoreUrl: '/hosting'
}

// Fetch all products (hosting gid=1 + SaaS gids 3-9) in one request
const { data: whmcsRaw } = await useApi<unknown[]>('/api/portal/public/products', {
  query: computed(() => ({ lang: locale.value, gids: productGids.join(',') })),
  default: () => []
})

/** Build product cards from WHMCS data + visual config */
const products = computed(() => {
  const raw = (whmcsRaw.value ?? []) as any[]
  const items: {
    id: string; name: string; tagline: string; description: string; features: string[]
    icon: string; color: string; image: string; demoUrl: string; learnMoreUrl: string
  }[] = []

  // ── Domains (static — no WHMCS product gid) ──
  items.push({
    id: 'domains',
    name: t('domains.card.name'),
    tagline: t('domains.card.tagline'),
    description: t('domains.card.description'),
    features: [t('domains.card.f1'), t('domains.card.f2'), t('domains.card.f3')],
    icon: 'earth',
    color: '#3b82f6',
    image: imageMap['domains'] || '/images/products/domains.jpg',
    demoUrl: '',
    learnMoreUrl: '/domains'
  })

  // ── All product families (gids 1, 3-9) ──
  for (const gid of productGids) {
    const cfgKey = productGidToKey[gid]
    const cfg = productConfig[cfgKey]
    if (!cfg) continue

    const group = raw.filter(p => Number(p.gid) === gid)
    const parent = group.find(p => p.slug?.trim() === cfgKey)
      ?? group.find(p => nameToKey(p.name) === cfgKey)
      ?? group[0]
    if (!parent) continue

    const pGt = group.find(p => p.group_translations?.translated_name) ?? parent
    const descText = parent.translated_description || parent.description || ''
    const { summary, features: parsedFeatures } = parseDescription(descText)
    const groupFeatures = getGroupFeatures(pGt)

    // Hosting (gid=1) uses hostingVisual overrides for icon/color/image/demoUrl
    const visual = gid === 1 ? hostingVisual : { icon: cfg.icon, color: cfg.color, image: imageMap[cfgKey] || '', demoUrl: cfg.demoUrl, learnMoreUrl: cfg.learnMoreUrl }

    items.push({
      id: cfgKey,
      name: getGt(pGt, 'name') || cfg.name,
      tagline: getGt(pGt, 'headline') || getGt(pGt, 'tagline') || cfg.name,
      description: parent.translated_shortdescription || parent.shortdescription || summary || getGt(pGt, 'headline'),
      features: (groupFeatures.length > 0 ? groupFeatures : parsedFeatures).slice(0, 3),
      ...visual
    })
  }

  return items
})
</script>

<style scoped>
.line-clamp-3 {
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>
