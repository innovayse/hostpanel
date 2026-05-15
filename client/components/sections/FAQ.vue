<template>
  <section class="py-20 bg-[#0d0d12] relative overflow-hidden">
    <!-- Background -->
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute inset-0 bg-gradient-to-br from-primary-500/5 via-transparent to-secondary-500/5" />

      <!-- Glowing orbs -->
      <div class="absolute top-1/4 right-1/4 w-96 h-96 bg-primary-500/15 rounded-full blur-[140px] animate-blob" />
      <div class="absolute bottom-1/3 left-1/3 w-80 h-80 bg-secondary-500/12 rounded-full blur-[130px] animate-blob animation-delay-2000" />

      <!-- Floating particles -->
      <div class="absolute top-20 left-1/4 w-2 h-2 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 0.6s;" />
      <div class="absolute top-1/3 right-1/3 w-3 h-3 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 1.4s;" />
      <div class="absolute bottom-1/4 left-1/2 w-2 h-2 bg-cyan-400/60 rounded-full animate-float" style="animation-delay: 2.1s;" />

      <!-- Grid pattern -->
      <div class="absolute inset-0 opacity-[0.015]">
        <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" class="w-full h-full" />
      </div>

      <!-- Diagonal accent lines -->
      <div class="absolute top-0 left-1/3 w-px h-full bg-gradient-to-b from-transparent via-primary-500/10 to-transparent" />
      <div class="absolute top-0 right-1/3 w-px h-full bg-gradient-to-b from-transparent via-secondary-500/10 to-transparent" />
    </div>

    <div class="container-custom relative z-10">
      <!-- Section header -->
      <div
        v-if="showHeader"
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="text-center mb-12"
      >
        <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-4">
          {{ $t('faq.badge') }}
        </span>
        <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
          {{ $t('faq.title') }}
        </h2>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('faq.subtitle') }}
        </p>
      </div>

      <!-- Category tabs -->
      <div v-if="showCategories" class="flex flex-wrap justify-center gap-2 mb-12">
        <button
          v-for="category in categories"
          :key="category.value"
          type="button"
          :class="[
            'px-5 py-2.5 rounded-lg font-medium text-sm transition-all',
            activeCategory === category.value
              ? 'bg-primary-500 text-white shadow-lg shadow-primary-500/30'
              : 'bg-white/5 text-gray-400 hover:bg-white/10 hover:text-white border border-white/10'
          ]"
          @click="activeCategory = category.value"
        >
          {{ category.label }}
        </button>
      </div>

      <!-- FAQ List -->
      <div class="max-w-3xl mx-auto">
        <div class="space-y-3 mb-12">
          <div
            v-for="(faq, index) in filteredFaqs"
            :key="faq.id"
            v-motion
            :initial="{ opacity: 0, x: -30 }"
            :visibleOnce="{ opacity: 1, x: 0, transition: { delay: index * 80, duration: 400 } }"
            class="rounded-xl border border-white/10 overflow-hidden hover:border-primary-500/30 transition-colors bg-white/5"
          >
            <button
              class="w-full p-5 flex items-start justify-between text-left hover:bg-white/10 transition-colors"
              @click="toggleFaq(faq.id)"
            >
              <span class="font-medium text-white pr-4 text-sm">{{ faq.question }}</span>
              <ChevronDown
                :size="20"
                :stroke-width="2"
                class="text-gray-400 transition-transform flex-shrink-0 mt-0.5"
                :class="{ 'rotate-180': openFaqId === faq.id }"
              />
            </button>
            <Transition name="faq-expand">
              <div
                v-show="openFaqId === faq.id"
                class="px-5 pb-5 text-gray-400 text-sm leading-relaxed"
              >
                {{ faq.answer }}
              </div>
            </Transition>
          </div>
        </div>

        <!-- View all FAQs button (if limited) -->
        <div v-if="limit" class="text-center mb-8">
          <UiButton
            variant="outline"
            size="lg"
            class="border-gray-700 hover:border-primary-500"
            @click="navigateTo(localePath('/faq'))"
          >
            <span>{{ $t('faq.viewAll') }}</span>
            <ArrowRight :size="18" :stroke-width="2" class="ml-2" />
          </UiButton>
        </div>

        <!-- Contact card -->
        <div class="relative p-8 rounded-2xl bg-gradient-to-br from-primary-500/20 to-secondary-500/10 border border-primary-500/30 text-center overflow-hidden group hover:border-primary-500/50 transition-all duration-300">
          <!-- Animated background glow -->
          <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 to-secondary-500/5 opacity-0 group-hover:opacity-100 transition-opacity duration-500" />

          <!-- Decorative shapes -->
          <div class="absolute top-0 right-0 w-32 h-32 bg-primary-500/10 rounded-full blur-3xl" />
          <div class="absolute bottom-0 left-0 w-24 h-24 bg-secondary-500/10 rounded-full blur-2xl" />

          <MessageCircle :size="48" :stroke-width="2" class="relative text-primary-400 mx-auto mb-4 group-hover:scale-110 group-hover:rotate-6 transition-transform duration-300" />
          <h3 class="relative text-xl font-semibold text-white mb-2">{{ $t('faq.cta.title') }}</h3>
          <p class="relative text-gray-400 text-sm mb-6 max-w-md mx-auto">
            {{ $t('faq.cta.description') }}
          </p>
          <UiButton class="relative bg-primary-500 hover:bg-primary-600 text-white hover:shadow-lg hover:shadow-primary-500/50 transition-all duration-300" @click="navigateTo(localePath('/contact'))">
            <MessageSquare :size="18" :stroke-width="2" class="mr-2" />
            {{ $t('faq.cta.button') }}
          </UiButton>

          <!-- Corner accent with rounded border -->
          <div class="absolute top-0 right-0 w-14 h-14 border-r-2 border-t-2 border-primary-500/50 rounded-tr-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
        </div>
      </div>
    </div>

    <!-- Section corner decorations -->
    <div class="absolute top-0 right-0 w-40 h-40 border-r-2 border-t-2 border-secondary-500/25 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-40 h-40 border-l-2 border-b-2 border-primary-500/25 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
/**
 * FAQ section with category filtering
 * Uses data from lib/data.ts with i18n translations
 */

import { ChevronDown, ArrowRight, MessageCircle, MessageSquare } from 'lucide-vue-next'
import { faqs as rawFaqs } from '~/lib/data'

interface Props {
  /** Limit number of FAQs to show (for home page preview) */
  limit?: number
  /** Show category tabs */
  showCategories?: boolean
  /** Show section header */
  showHeader?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  limit: undefined,
  showCategories: true,
  showHeader: true
})

const { t } = useI18n()
const localePath = useLocalePath()

/** FAQs with i18n translations */
const faqs = computed(() => rawFaqs.map(faq => ({
  ...faq,
  question: t(`faq.items.${faq.id}.question`),
  answer: t(`faq.items.${faq.id}.answer`)
})))

const categories = computed(() => [
  { label: t('faq.categories.all'), value: 'all' },
  { label: t('faq.categories.general'), value: 'general' },
  { label: t('faq.categories.development'), value: 'development' },
  { label: t('faq.categories.seo'), value: 'seo' },
  { label: t('faq.categories.products'), value: 'products' }
])

const activeCategory = ref<string>('all')
const openFaqId = ref<string | null>(null)

const filteredFaqs = computed(() => {
  let result = faqs.value

  // Filter by category
  if (activeCategory.value !== 'all') {
    result = faqs.value.filter(faq => faq.category === activeCategory.value)
  }

  // Limit results if specified
  if (props.limit) {
    result = result.slice(0, props.limit)
  }

  return result
})

const toggleFaq = (faqId: string) => {
  openFaqId.value = openFaqId.value === faqId ? null : faqId
}

// Reset to first FAQ when category changes
watch(activeCategory, () => {
  openFaqId.value = filteredFaqs.value[0]?.id || null
})
</script>

<style scoped>
.faq-expand-enter-active,
.faq-expand-leave-active {
  transition: all 0.3s ease;
  overflow: hidden;
}

.faq-expand-enter-from,
.faq-expand-leave-to {
  max-height: 0;
  opacity: 0;
}

.faq-expand-enter-to,
.faq-expand-leave-from {
  max-height: 500px;
  opacity: 1;
}
</style>
