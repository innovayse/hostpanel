<template>
  <div>
    <!-- Hero -->
    <section class="py-10 md:py-24 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[450px] h-[450px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />
        <div class="absolute top-1/3 right-1/4 w-2 h-2 bg-primary-400/70 rounded-full animate-float" style="animation-delay: 0.8s;" />
        <div class="absolute bottom-1/4 left-1/3 w-3 h-3 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 1.8s;" />
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
          {{ $t('writeForUs.hero.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl font-bold text-white mb-6">
          {{ $t('writeForUs.hero.title') }}
          <span class="bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent animate-gradient bg-300">
            {{ $t('writeForUs.hero.titleHighlight') }}
          </span>
        </h1>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('writeForUs.hero.subtitle') }}
        </p>
      </div>

      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Stats Bar -->
    <section class="py-6 bg-[#0c0c11] border-y border-white/5">
      <div class="container-custom">
        <div class="grid grid-cols-2 md:grid-cols-4 gap-6">
          <div
            v-for="(stat, i) in wfuStats"
            :key="i"
            v-motion
            :initial="{ opacity: 0, y: 10 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: i * 100, duration: 400 } }"
            class="text-center p-4 rounded-xl bg-white/3 border border-white/5"
          >
            <div class="text-2xl font-bold text-primary-400">{{ stat.value }}</div>
            <div class="text-xs text-gray-400 mt-1">{{ stat.label }}</div>
          </div>
        </div>
      </div>
    </section>

    <!-- Why Write Section -->
    <section class="py-12 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="container-custom">
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          class="text-center mb-12"
        >
          <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">{{ $t('writeForUs.whyWrite.title') }}</h2>
          <p class="text-gray-400 max-w-2xl mx-auto">{{ $t('writeForUs.whyWrite.subtitle') }}</p>
        </div>

        <div class="grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
          <div
            v-for="(item, index) in whyItems"
            :key="index"
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
            class="group p-6 rounded-2xl bg-white/5 border border-white/10 hover:border-primary-500/30 transition-all duration-300 hover:-translate-y-1"
          >
            <div
              class="w-12 h-12 rounded-xl flex items-center justify-center mb-4"
              :style="{ backgroundColor: item.color + '20' }"
            >
              <Icon :name="item.icon" class="text-2xl" :style="{ color: item.color }" />
            </div>
            <h3 class="text-base font-bold text-white mb-2">{{ item.title }}</h3>
            <p class="text-gray-400 text-sm leading-relaxed">{{ item.desc }}</p>
          </div>
        </div>
      </div>
    </section>

    <!-- Topics Section -->
    <section class="py-12 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-primary-500/20 to-transparent" />
      </div>
      <div class="container-custom">
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          class="text-center mb-12"
        >
          <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">{{ $t('writeForUs.topics.title') }}</h2>
          <p class="text-gray-400 max-w-2xl mx-auto">{{ $t('writeForUs.topics.subtitle') }}</p>
        </div>

        <div class="grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
          <div
            v-for="(cat, index) in topicCategories"
            :key="index"
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
            class="p-6 rounded-2xl bg-white/5 border border-white/10"
          >
            <div
              class="w-10 h-10 rounded-xl flex items-center justify-center mb-4"
              :style="{ backgroundColor: cat.color + '20' }"
            >
              <Icon :name="cat.icon" class="text-xl" :style="{ color: cat.color }" />
            </div>
            <h3 class="text-base font-bold text-white mb-3">{{ cat.title }}</h3>
            <ul class="space-y-2">
              <li
                v-for="(topicItem, i) in cat.items"
                :key="i"
                class="flex items-start gap-2 text-sm text-gray-400"
              >
                <Icon name="mdi:check-circle-outline" class="text-sm flex-shrink-0 mt-0.5" :style="{ color: cat.color }" />
                {{ topicItem }}
              </li>
            </ul>
          </div>
        </div>
      </div>
    </section>

    <!-- Guidelines Section -->
    <section class="py-12 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="container-custom">
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          class="text-center mb-12"
        >
          <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">{{ $t('writeForUs.guidelines.title') }}</h2>
          <p class="text-gray-400 max-w-2xl mx-auto">{{ $t('writeForUs.guidelines.subtitle') }}</p>
        </div>

        <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <div
            v-for="(guide, index) in guidelineItems"
            :key="index"
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 80, duration: 500 } }"
            class="flex gap-4 p-5 rounded-xl bg-white/5 border border-white/10"
          >
            <div
              class="flex-shrink-0 w-10 h-10 rounded-xl flex items-center justify-center"
              :style="{ backgroundColor: guide.color + '20' }"
            >
              <Icon :name="guide.icon" class="text-xl" :style="{ color: guide.color }" />
            </div>
            <div>
              <h3 class="text-sm font-bold text-white mb-1">{{ guide.title }}</h3>
              <p class="text-gray-400 text-xs leading-relaxed">{{ guide.desc }}</p>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Process Section -->
    <section class="py-12 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="container-custom">
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          class="text-center mb-12"
        >
          <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">{{ $t('writeForUs.process.title') }}</h2>
        </div>

        <div class="grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
          <div
            v-for="(step, index) in processSteps"
            :key="index"
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 120, duration: 500 } }"
            class="relative p-6 rounded-2xl bg-white/5 border border-white/10"
          >
            <!-- Step number -->
            <div class="text-4xl font-black text-primary-500/20 mb-4 leading-none">{{ step.number }}</div>
            <h3 class="text-base font-bold text-white mb-2">{{ step.title }}</h3>
            <p class="text-gray-400 text-sm leading-relaxed">{{ step.desc }}</p>

            <!-- Connector (hidden on last) -->
            <div
              v-if="index < processSteps.length - 1"
              class="hidden lg:block absolute top-10 -right-3 w-6 h-0.5 bg-gradient-to-r from-primary-500/40 to-transparent"
            />
          </div>
        </div>
      </div>
    </section>

    <!-- Form CTA + Not Accepted -->
    <section class="py-12 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="container-custom">
        <div class="grid lg:grid-cols-2 gap-8 items-start">
          <!-- Submit CTA -->
          <div
            v-motion
            :initial="{ opacity: 0, x: -30 }"
            :visibleOnce="{ opacity: 1, x: 0, transition: { duration: 600 } }"
            class="p-8 md:p-10 rounded-2xl bg-gradient-to-br from-primary-500/10 to-secondary-500/10 border border-white/10"
          >
            <Icon name="mdi:pencil-plus" class="text-5xl text-primary-400 mb-6" />
            <h2 class="text-2xl md:text-3xl font-bold text-white mb-3">{{ $t('writeForUs.form.title') }}</h2>
            <p class="text-gray-400 mb-6">{{ $t('writeForUs.form.subtitle') }}</p>
            <NuxtLink
              :to="localePath('/contact')"
              class="inline-flex items-center gap-2 px-8 py-4 bg-primary-500 hover:bg-primary-600 text-white font-semibold rounded-xl transition-all duration-300 hover:shadow-lg hover:shadow-primary-500/30 mb-4"
            >
              <Icon name="mdi:send" class="text-xl" />
              {{ $t('writeForUs.form.button') }}
            </NuxtLink>
            <p class="text-xs text-gray-500 mt-4">
              <Icon name="mdi:information-outline" class="inline text-sm mr-1" />
              {{ $t('writeForUs.form.note') }}
            </p>
          </div>

          <!-- Not Accepted -->
          <div
            v-motion
            :initial="{ opacity: 0, x: 30 }"
            :visibleOnce="{ opacity: 1, x: 0, transition: { duration: 600 } }"
            class="p-8 md:p-10 rounded-2xl bg-white/3 border border-white/8"
          >
            <div class="flex items-center gap-3 mb-6">
              <div class="w-10 h-10 rounded-xl bg-red-500/15 flex items-center justify-center flex-shrink-0">
                <Icon name="mdi:close-circle" class="text-xl text-red-400" />
              </div>
              <h2 class="text-xl font-bold text-white">{{ $t('writeForUs.notAccepted.title') }}</h2>
            </div>
            <ul class="space-y-3">
              <li
                v-for="(notItem, i) in notAcceptedItems"
                :key="i"
                class="flex items-start gap-3 text-sm text-gray-400"
              >
                <Icon name="mdi:close" class="text-red-400 flex-shrink-0 mt-0.5 text-base" />
                {{ notItem }}
              </li>
            </ul>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
const localePath = useLocalePath()
const { t } = useI18n()

interface WfuStat { value: string; label: string }
interface WhyItem { icon: string; color: string; title: string; desc: string }
interface TopicCategory { icon: string; color: string; title: string; items: string[] }
interface GuidelineItem { icon: string; color: string; title: string; desc: string }
interface ProcessStep { number: string; title: string; desc: string }

const wfuStats = computed(() => {
  const raw = t('writeForUs.stats', [], { returnObjects: true })
  return Array.isArray(raw) ? raw as WfuStat[] : []
})

const whyItems = computed(() => {
  const raw = t('writeForUs.whyWrite.items', [], { returnObjects: true })
  return Array.isArray(raw) ? raw as WhyItem[] : []
})

const topicCategories = computed(() => {
  const raw = t('writeForUs.topics.categories', [], { returnObjects: true })
  return Array.isArray(raw) ? raw as TopicCategory[] : []
})

const guidelineItems = computed(() => {
  const raw = t('writeForUs.guidelines.items', [], { returnObjects: true })
  return Array.isArray(raw) ? raw as GuidelineItem[] : []
})

const processSteps = computed(() => {
  const raw = t('writeForUs.process.steps', [], { returnObjects: true })
  return Array.isArray(raw) ? raw as ProcessStep[] : []
})

const notAcceptedItems = computed(() => {
  const raw = t('writeForUs.notAccepted.items', [], { returnObjects: true })
  return Array.isArray(raw) ? raw as string[] : []
})

// SEO
const { baseUrl: wfuBaseUrl } = useSeo({
  title: t('seo.writeForUs.title'),
  description: t('seo.writeForUs.description'),
  keywords: t('seo.writeForUs.keywords'),
  type: 'website',
  path: '/write-for-us'
})

// Schema.org
const { injectSchema: wfuInjectSchema } = useSchemaOrg()
wfuInjectSchema([
  {
    '@context': 'https://schema.org',
    '@type': 'WebPage',
    '@id': `${wfuBaseUrl}/write-for-us#webpage`,
    url: `${wfuBaseUrl}/write-for-us`,
    name: t('seo.writeForUs.title'),
    description: t('seo.writeForUs.description'),
    publisher: { '@id': `${wfuBaseUrl}/#organization` }
  }
])
</script>
