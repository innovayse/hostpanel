<template>
  <UiSection v-if="faqs.length" :contained="false" padding="lg" :class="sectionClasses" data-testid="sections-faq">
    <!-- Background (aurora and nova only; the classic template stays flat) -->
    <UiDecor v-if="!isClassic" class="inset-0">
      <UiDecor class="inset-0 bg-gradient-to-br from-primary-500/5 via-transparent to-secondary-500/5" />
      <UiDecor class="top-1/4 right-1/4 w-96 h-96 bg-primary-500/15 rounded-full blur-[140px] animate-blob" />
      <UiDecor class="bottom-1/3 left-1/3 w-80 h-80 bg-secondary-500/10 rounded-full blur-[130px] animate-blob animation-delay-2000" />
      <UiDecor class="top-20 left-1/4 w-2 h-2 bg-primary-400/60 rounded-full animate-float [animation-delay:0.6s]" />
      <UiDecor class="top-1/3 right-1/3 w-3 h-3 bg-secondary-600/50 dark:bg-secondary-400/50 rounded-full animate-float [animation-delay:1.4s]" />
      <UiDecor class="bottom-1/4 left-1/2 w-2 h-2 bg-cyan-400/60 rounded-full animate-float [animation-delay:2.1s]" />
      <UiDecor class="inset-0 opacity-[0.015] pattern-grid-50" />
      <UiDecor class="top-0 left-1/3 w-px h-full bg-gradient-to-b from-transparent via-primary-500/10 to-transparent" />
      <UiDecor class="top-0 right-1/3 w-px h-full bg-gradient-to-b from-transparent via-secondary-500/10 to-transparent" />
    </UiDecor>

    <UiContainer class="relative z-10">
      <ClassicSectionHeader
        v-if="showHeader && isClassic"
        class="mb-12"
        :eyebrow="$t('faq.badge')"
        :title="$t('faq.title')"
        :subtitle="$t('faq.subtitle')"
      />
      <MarketingSectionHeader
        v-else-if="showHeader"
        class="mb-12"
        :badge="$t('faq.badge')"
        :title="$t('faq.title')"
        :subtitle="$t('faq.subtitle')"
      />

      <!-- Category tabs -->
      <UiChipTabs
        v-if="showCategories"
        v-model="activeCategory"
        class="mb-12"
        :tabs="categoryTabs"
        :label="$t('faq.filterLabel')"
      />

      <UiStack gap="none">
        <!-- Questions -->
        <UiAccordion
          :key="activeCategory"
          class="mb-12"
          :items="accordionItems"
          :default-open="categoryChosen ? [0] : []"
          keep-content
          data-testid="faq-list"
        />

        <!-- View all FAQs (when limited) -->
        <UiStack v-if="hasMore" align="center" gap="none" class="mb-8">
          <UiButton variant="outline" size="lg" :to="localePath(SITE_FAQ_PATH)" data-testid="faq-view-all">
            {{ $t('faq.viewAll') }}
            <ArrowRight :size="18" :stroke-width="2" class="ml-2" />
          </UiButton>
        </UiStack>

        <!-- Contact card, classic: flat, with a solid brand button -->
        <ClassicCard v-if="isClassic" class="text-center" padding="xl" data-testid="faq-contact">
          <UiStack align="center" gap="sm">
            <ClassicIconTile icon="mdi:message-text-outline" />
            <UiHeading :level="3" size="bare" class="text-xl font-semibold text-classic-ink">{{ $t('faq.cta.title') }}</UiHeading>
            <UiText tone="inherit" size="sm" class="mb-3 max-w-[75ch] text-classic-muted">{{ $t('faq.cta.description') }}</UiText>
            <UiButton :to="localePath(SITE_CONTACT_PATH)" variant="primary" size="md" data-testid="faq-contact-button">
              <MessageSquare :size="18" :stroke-width="2" class="mr-2" />
              {{ $t('common.contactUs') }}
            </UiButton>
          </UiStack>
        </ClassicCard>

        <!-- Contact card, aurora and nova -->
        <UiCard v-else class="relative text-center overflow-hidden group hover:border-primary-500/50 transition-all duration-300" padding="xl" data-testid="faq-contact">
          <UiDecor class="inset-0 bg-gradient-to-br from-primary-500/20 to-secondary-500/10" />
          <UiDecor class="inset-0 bg-gradient-to-br from-primary-500/10 to-secondary-500/5 opacity-0 group-hover:opacity-100 transition-opacity duration-500" />
          <UiDecor class="top-0 right-0 w-32 h-32 bg-primary-500/10 rounded-full blur-3xl" />
          <UiDecor class="bottom-0 left-0 w-24 h-24 bg-secondary-500/10 rounded-full blur-2xl" />

          <MessageCircle :size="48" :stroke-width="2" class="relative text-primary-600 dark:text-primary-400 mx-auto mb-4 group-hover:scale-110 group-hover:rotate-6 transition-transform duration-300" />
          <UiHeading :level="3" size="subsection" class="relative text-xl mb-2">{{ $t('faq.cta.title') }}</UiHeading>
          <UiText tone="muted" size="sm" class="relative mb-6 max-w-[75ch] mx-auto">{{ $t('faq.cta.description') }}</UiText>
          <UiButton class="relative" :to="localePath(SITE_CONTACT_PATH)" variant="primary" size="md" data-testid="faq-contact-button">
            <MessageSquare :size="18" :stroke-width="2" class="mr-2" />
            {{ $t('common.contactUs') }}
          </UiButton>

          <UiDecor class="top-0 right-0 w-14 h-14 border-r-2 border-t-2 border-primary-500/50 rounded-tr-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
        </UiCard>
      </UiStack>
    </UiContainer>

    <!-- Section corner decorations -->
    <UiDecor v-if="!isClassic" class="top-0 right-0 w-40 h-40 border-r-2 border-t-2 border-secondary-500/25" />
    <UiDecor v-if="!isClassic" class="bottom-0 left-0 w-40 h-40 border-l-2 border-b-2 border-primary-500/25" />
  </UiSection>
</template>

<script setup lang="ts">
/**
 * FAQ section with category filtering.
 *
 * The questions come from the storefront store (`GET /api/portal/public/faqs`, localised by the API); with
 * none the section renders nothing. Choosing a
 * category reloads the list and opens its first question; before any choice every question is
 * closed. With a `limit` the section is a preview that links to the full FAQ page, but only
 * when the limit actually hides some questions. The aurora and nova templates keep their glow
 * background and gradient contact card; the classic template gets a flat surface and a plain card.
 */
import { ArrowRight, MessageCircle, MessageSquare } from 'lucide-vue-next'
import { useStorefrontStore } from '~/stores/storefront'
import { FaqCategory } from '~/types/faqcategory'
import { faqCategorySlug } from '~/utils/faqCategory'
import type { ChipTab } from '~/types/chiptab'
import { TemplateName } from '~/types/templatename'
import { SITE_CONTACT_PATH, SITE_FAQ_PATH } from '~/constants/siteRoutes'

/** Props of SectionsFAQ. */
interface Props {
  /** Limit number of FAQs to show (for the home page preview). */
  limit?: number
  /** Show category tabs. */
  showCategories?: boolean
  /** Show the section header. */
  showHeader?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  limit: undefined,
  showCategories: true,
  showHeader: true,
})

/** The active portal template. */
const { name: templateName } = useTemplate()

/** Whether the classic template is active, which paints the section flat. */
const isClassic = computed(() => templateName.value === TemplateName.Classic)

/** Classes of the section: flat charcoal-or-white for classic, the decorated surface otherwise. */
const sectionClasses = computed(() => isClassic.value
  ? 'bg-classic-surface border-y border-classic-border'
  : 'bg-ui-surface relative overflow-hidden')

/** The "every category" choice of the tabs. */
const ALL_CATEGORIES = 'all'

/** The choice a tab stands for: a topic or all of them. */
type CategoryChoice = FaqCategory | typeof ALL_CATEGORIES

/** Translation helpers and the active language. */
const { t, locale } = useI18n()
/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:faqs', () => storefront.loadFaqs(), { watch: [locale] })

/** Every question, in display order. */
const faqs = computed(() => storefront.faqs)

/** The tabs: all, then one per topic; `slug` spells the locale key and the test id. */
const categories = computed(() =>
  ([ALL_CATEGORIES, ...Object.values(FaqCategory)] satisfies CategoryChoice[]).map((value) => {
    const slug = value === ALL_CATEGORIES ? ALL_CATEGORIES : faqCategorySlug(value)
    return {
      value,
      slug,
      label: t(`faq.categories.${slug}`),
    }
  }))

/** The tabs as chips, each keeping its `faq-category-<slug>` test id. */
const categoryTabs = computed<ChipTab<CategoryChoice>[]>(() =>
  categories.value.map(category => ({
    value: category.value,
    label: category.label,
    testid: `faq-category-${category.slug}`,
  })))

/** The category chosen in the tabs. */
const activeCategory = ref<CategoryChoice>(ALL_CATEGORIES)

/** Whether the visitor has chosen a category yet; the first question opens only after that. */
const categoryChosen = ref(false)

watch(activeCategory, () => {
  categoryChosen.value = true
})

/** The questions of the active category, before any `limit` is applied. */
const categoryFaqs = computed(() =>
  faqs.value.filter((faq) => activeCategory.value === ALL_CATEGORIES || faq.category === activeCategory.value))

/** Whether a `limit` hides some questions, so the link to the full FAQ page is worth showing. */
const hasMore = computed(() => Boolean(props.limit) && categoryFaqs.value.length > (props.limit ?? 0))

/** The questions of the active category, cut to `limit` when one is set, with their translated text. */
const accordionItems = computed(() => {
  const result = props.limit ? categoryFaqs.value.slice(0, props.limit) : categoryFaqs.value
  return result.map((faq) => ({
    title: faq.question,
    content: faq.answer,
  }))
})
</script>
