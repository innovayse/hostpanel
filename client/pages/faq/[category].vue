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
        <!-- Breadcrumb -->
        <nav class="flex items-center justify-center gap-2 text-sm text-gray-500 mb-6">
          <NuxtLink :to="localePath('/faq')" class="hover:text-primary-400 transition-colors">
            {{ $t('faq.title') }}
          </NuxtLink>
          <span>/</span>
          <span class="text-white">{{ categoryLabel }}</span>
        </nav>

        <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6">
          {{ $t('faq.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl font-bold text-white mb-6">
          {{ $t(`seo.faqCategory.${category}.h1`) }}
        </h1>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto mb-10">
          {{ $t(`seo.faqCategory.${category}.description`) }}
        </p>

        <!-- Other categories -->
        <div class="flex flex-wrap justify-center gap-3">
          <NuxtLink
            :to="localePath('/faq')"
            class="px-5 py-2 rounded-lg font-medium text-sm border border-white/10 bg-white/5 text-gray-300 hover:bg-white/10 transition-all duration-200"
          >
            {{ $t('faq.categories.all') }}
          </NuxtLink>
          <NuxtLink
            v-for="cat in otherCategories"
            :key="cat.value"
            :to="localePath(`/faq/${cat.value}`)"
            class="px-5 py-2 rounded-lg font-medium text-sm border transition-all duration-200"
            :class="cat.value === category
              ? 'bg-primary-500 text-white border-primary-500 shadow-lg shadow-primary-500/30'
              : 'border-white/10 bg-white/5 text-gray-300 hover:bg-primary-500/20 hover:border-primary-500/40 hover:text-white'"
          >
            {{ cat.label }}
          </NuxtLink>
        </div>
      </div>

      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- FAQs for this category -->
    <section class="py-12 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-1/4 right-1/4 w-96 h-96 bg-primary-500/10 rounded-full blur-[140px]" />
        <div class="absolute bottom-1/3 left-1/3 w-80 h-80 bg-secondary-500/10 rounded-full blur-[130px]" />
      </div>

      <div class="container-custom max-w-3xl relative z-10">
        <!-- Empty state -->
        <div v-if="categoryFaqs.length === 0" class="text-center py-16">
          <p class="text-gray-400">{{ $t('faq.noResults') }}</p>
          <NuxtLink :to="localePath('/faq')" class="mt-4 inline-block text-primary-400 hover:underline">
            {{ $t('faq.viewAll') }}
          </NuxtLink>
        </div>

        <!-- FAQ accordion -->
        <div v-else class="space-y-3 mb-12">
          <div
            v-for="(faq, index) in categoryFaqs"
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
              <div v-show="openFaqId === faq.id" class="px-5 pb-5 text-gray-400 text-sm leading-relaxed">
                {{ faq.answer }}
              </div>
            </Transition>
          </div>
        </div>

        <!-- Back to all FAQs -->
        <div class="text-center">
          <NuxtLink
            :to="localePath('/faq')"
            class="inline-flex items-center gap-2 px-6 py-3 rounded-xl border border-white/10 text-gray-300 hover:text-white hover:border-primary-500/30 transition-all duration-200 text-sm font-medium"
          >
            <ArrowLeft :size="16" :stroke-width="2" />
            {{ $t('faq.backToAll') }}
          </NuxtLink>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ChevronDown, ArrowLeft } from 'lucide-vue-next'
import { faqs as rawFaqs } from '~/lib/data'

const route = useRoute()
const { t } = useI18n()
const localePath = useLocalePath()

const validCategories = ['general', 'development', 'seo', 'products'] as const
type FaqCategory = typeof validCategories[number]

const category = computed(() => route.params.category as string)

// 404 if invalid category
if (!validCategories.includes(category.value as FaqCategory)) {
  throw createError({ statusCode: 404, statusMessage: 'FAQ category not found' })
}

const allCategories = computed(() => [
  { label: t('faq.categories.general'), value: 'general' },
  { label: t('faq.categories.development'), value: 'development' },
  { label: t('faq.categories.seo'), value: 'seo' },
  { label: t('faq.categories.products'), value: 'products' }
])

const otherCategories = computed(() => allCategories.value)
const categoryLabel = computed(() => allCategories.value.find(c => c.value === category.value)?.label ?? '')

// FAQs for this category with translations
const categoryFaqs = computed(() =>
  rawFaqs
    .filter(faq => faq.category === category.value)
    .map(faq => ({
      ...faq,
      question: t(`faq.items.${faq.id}.question`),
      answer: t(`faq.items.${faq.id}.answer`)
    }))
)

const openFaqId = ref<string | null>(categoryFaqs.value[0]?.id ?? null)
const toggleFaq = (id: string) => { openFaqId.value = openFaqId.value === id ? null : id }

// Per-category SEO
useSeo({
  title: t(`seo.faqCategory.${category.value}.title`),
  description: t(`seo.faqCategory.${category.value}.description`),
  keywords: t(`seo.faqCategory.${category.value}.keywords`),
  type: 'website',
  path: `/faq/${category.value}`
})

// FAQ schema.org for this category only
const { faqSchema, injectSchema } = useSchemaOrg()
injectSchema(faqSchema(categoryFaqs.value.map(f => ({ question: f.question, answer: f.answer }))))
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
