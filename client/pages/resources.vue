<template>
  <div>
    <!-- Hero -->
    <section class="py-10 md:py-24 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[450px] h-[450px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />
        <div class="absolute top-1/4 right-1/3 w-2 h-2 bg-primary-400/70 rounded-full animate-float" style="animation-delay: 0.6s;" />
        <div class="absolute bottom-1/3 left-1/4 w-3 h-3 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 1.5s;" />
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 70px 70px;" class="w-full h-full" />
        </div>
      </div>

      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :enter="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="container-custom relative z-10 text-center"
      >
        <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6">
          {{ $t('resources.hero.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl font-bold text-white mb-6">
          {{ $t('resources.hero.title') }}
          <span class="bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent animate-gradient bg-300">
            {{ $t('resources.hero.titleHighlight') }}
          </span>
        </h1>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('resources.hero.subtitle') }}
        </p>
      </div>

      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Stats Bar -->
    <section class="py-6 bg-[#0c0c11] border-y border-white/5">
      <div class="container-custom">
        <div class="flex flex-wrap justify-center gap-8 md:gap-16">
          <div
            v-for="(stat, i) in stats"
            :key="i"
            v-motion
            :initial="{ opacity: 0, y: 10 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: i * 100, duration: 400 } }"
            class="text-center"
          >
            <div class="text-2xl md:text-3xl font-bold text-primary-400">{{ stat.value }}</div>
            <div class="text-sm text-gray-400 mt-1">{{ stat.label }}</div>
          </div>
        </div>
      </div>
    </section>

    <!-- Resources Section -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/5 via-transparent to-secondary-500/5" />
      </div>

      <div class="container-custom relative z-10">
        <!-- Category Filter -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          class="flex flex-wrap justify-center gap-3 mb-12"
        >
          <button
            v-for="cat in categoryIds"
            :key="cat"
            :class="[
              'px-5 py-2.5 rounded-full text-sm font-medium transition-all duration-300',
              selectedCategory === cat
                ? 'bg-primary-500 text-white shadow-lg shadow-primary-500/30'
                : 'bg-white/5 text-gray-400 hover:bg-white/10 hover:text-white border border-white/10'
            ]"
            @click="selectedCategory = cat"
          >
            {{ $t(`resources.categories.${cat}`) }}
          </button>
        </div>

        <!-- Resource Cards Grid -->
        <div class="grid md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
          <div
            v-for="(item, index) in filteredItems"
            :key="item.id"
            v-motion
            :initial="{ opacity: 0, y: 40 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 80, duration: 500 } }"
            class="group relative bg-white/5 rounded-2xl overflow-hidden border border-white/10 hover:border-primary-500/30 transition-all duration-500 hover:-translate-y-1 flex flex-col"
          >
            <!-- Card Header -->
            <div
              class="relative h-36 flex items-center justify-center overflow-hidden"
              :style="{ background: `linear-gradient(135deg, ${item.color}25, ${item.color}08)` }"
            >
              <Icon :name="item.icon" class="text-5xl opacity-40 transition-transform duration-500 group-hover:scale-110" :style="{ color: item.color }" />
              <!-- Free badge -->
              <div class="absolute top-3 right-3">
                <span class="px-2 py-1 rounded text-xs font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                  {{ $t('resources.free') }}
                </span>
              </div>
              <!-- Format badge -->
              <div class="absolute top-3 left-3">
                <span
                  class="px-2 py-1 rounded text-xs font-medium"
                  :style="{ backgroundColor: item.color + '20', color: item.color }"
                >
                  {{ item.badge }}
                </span>
              </div>
            </div>

            <!-- Card Body -->
            <div class="p-5 flex flex-col flex-1">
              <!-- Meta -->
              <div class="flex items-center gap-3 text-xs text-gray-500 mb-3">
                <span v-if="item.pages" class="flex items-center gap-1">
                  <Icon name="mdi:file-multiple" class="text-sm" />
                  {{ item.pages }} {{ $t('resources.pages') }}
                </span>
                <span v-if="item.items" class="flex items-center gap-1">
                  <Icon name="mdi:format-list-checks" class="text-sm" />
                  {{ item.items }} {{ $t('resources.items') }}
                </span>
                <span class="flex items-center gap-1 ml-auto">
                  <Icon name="mdi:calendar-refresh" class="text-sm" />
                  {{ item.updated }}
                </span>
              </div>

              <h2 class="text-base font-bold text-white mb-2 group-hover:text-primary-400 transition-colors leading-snug">
                {{ item.title }}
              </h2>
              <p class="text-gray-400 text-sm mb-4 flex-1 leading-relaxed">
                {{ item.desc }}
              </p>

              <!-- Tags -->
              <div class="flex flex-wrap gap-1.5 mb-4">
                <span
                  v-for="tag in item.tags.slice(0, 3)"
                  :key="tag"
                  class="px-2 py-0.5 rounded-full text-xs bg-white/5 text-gray-400 border border-white/5"
                >
                  {{ tag }}
                </span>
              </div>

              <!-- CTA Button -->
              <NuxtLink
                :to="localePath('/contact')"
                class="flex items-center justify-center gap-2 w-full py-2.5 rounded-xl text-sm font-semibold transition-all duration-300 border"
                :style="{
                  backgroundColor: item.color + '15',
                  borderColor: item.color + '40',
                  color: item.color
                }"
              >
                <Icon name="mdi:download" class="text-base" />
                {{ $t('resources.downloadCta') }}
              </NuxtLink>
            </div>

            <!-- Hover glow -->
            <div
              class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none"
              :style="{ boxShadow: `inset 0 0 60px ${item.color}10` }"
            />
          </div>
        </div>

        <!-- CTA Section -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 600 } }"
          class="mt-20 p-8 md:p-12 rounded-2xl bg-gradient-to-br from-primary-500/10 to-secondary-500/10 border border-white/10 text-center"
        >
          <Icon name="mdi:magnify-scan" class="text-5xl text-primary-400 mb-4 mx-auto" />
          <h3 class="text-2xl md:text-3xl font-bold text-white mb-4">{{ $t('resources.ctaSection.title') }}</h3>
          <p class="text-gray-400 mb-8 max-w-2xl mx-auto">{{ $t('resources.ctaSection.subtitle') }}</p>
          <NuxtLink
            :to="localePath('/contact')"
            class="inline-flex items-center gap-2 px-8 py-4 bg-primary-500 hover:bg-primary-600 text-white font-semibold rounded-xl transition-all duration-300 hover:shadow-lg hover:shadow-primary-500/30"
          >
            <Icon name="mdi:check-circle" class="text-xl" />
            {{ $t('resources.ctaSection.button') }}
          </NuxtLink>
        </div>

        <!-- Partnership / Link to This Page -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 100, duration: 500 } }"
          class="mt-10 p-6 md:p-8 rounded-2xl bg-white/3 border border-white/5 flex flex-col md:flex-row items-center gap-6 text-center md:text-left"
        >
          <div class="flex-shrink-0 w-14 h-14 rounded-2xl bg-secondary-500/15 flex items-center justify-center">
            <Icon name="mdi:link-variant" class="text-3xl text-secondary-400" />
          </div>
          <div>
            <h3 class="text-lg font-bold text-white mb-1">{{ $t('resources.partnership.title') }}</h3>
            <p class="text-gray-400 text-sm">{{ $t('resources.partnership.subtitle') }}</p>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
const localePath = useLocalePath()
const { t } = useI18n()
const selectedCategory = ref('all')

const categoryIds = ['all', 'seo', 'webdev', 'ads', 'mobile']

// Load resource items from locale
const allItems = computed(() => {
  const raw = t('resources.items_list', [], { returnObjects: true })
  return Array.isArray(raw) ? raw as ResourceItem[] : []
})

const stats = computed(() => {
  const raw = t('resources.stats', [], { returnObjects: true })
  return Array.isArray(raw) ? raw as { value: string; label: string }[] : []
})

interface ResourceItem {
  id: string
  category: string
  icon: string
  color: string
  badge: string
  pages?: number
  items?: number
  title: string
  desc: string
  tags: string[]
  updated: string
}

const filteredItems = computed(() => {
  if (selectedCategory.value === 'all') return allItems.value
  return allItems.value.filter((item: ResourceItem) => item.category === selectedCategory.value)
})

// SEO
const { baseUrl: resBaseUrl } = useSeo({
  title: t('seo.resources.title'),
  description: t('seo.resources.description'),
  keywords: t('seo.resources.keywords'),
  type: 'website',
  path: '/resources'
})

// Schema.org
const { injectSchema: resInjectSchema } = useSchemaOrg()
resInjectSchema([
  {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    '@id': `${resBaseUrl}/resources#collectionpage`,
    url: `${resBaseUrl}/resources`,
    name: t('seo.resources.title'),
    description: t('seo.resources.description'),
    publisher: { '@id': `${resBaseUrl}/#organization` }
  }
])
</script>
