<template>
  <div>
    <!-- Hero -->
    <section class="py-10 md:py-24 bg-[#0a0a0f] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[450px] h-[450px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />
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
          {{ $t('faq.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl font-bold text-white mb-6">
          {{ $t('faq.title') }}
        </h1>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto mb-10">
          {{ $t('faq.subtitle') }}
        </p>

        <!-- Category links for SEO -->
        <div class="flex flex-wrap justify-center gap-3">
          <NuxtLink
            v-for="cat in faqCategories"
            :key="cat.value"
            :to="localePath(`/faq/${cat.value}`)"
            class="px-5 py-2 rounded-lg font-medium text-sm border border-white/10 bg-white/5 text-gray-300 hover:bg-primary-500/20 hover:border-primary-500/40 hover:text-white transition-all duration-200"
          >
            {{ cat.label }}
          </NuxtLink>
        </div>
      </div>

      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- All FAQs -->
    <SectionsFAQ :show-header="false" :show-categories="false" />
  </div>
</template>

<script setup lang="ts">
import { faqs } from '~/lib/data'

const { t } = useI18n()
const localePath = useLocalePath()

const faqCategories = computed(() => [
  { label: t('faq.categories.general'), value: 'general' },
  { label: t('faq.categories.development'), value: 'development' },
  { label: t('faq.categories.seo'), value: 'seo' },
  { label: t('faq.categories.products'), value: 'products' }
])

useSeo({
  title: t('seo.faq.title'),
  description: t('seo.faq.description'),
  keywords: t('seo.faq.keywords'),
  type: 'website',
  path: '/faq'
})

const { faqSchema, injectSchema } = useSchemaOrg()
const faqData = faqs.map(faq => ({
  question: t(`faq.items.${faq.id}.question`),
  answer: t(`faq.items.${faq.id}.answer`)
}))
injectSchema(faqSchema(faqData))
</script>
