<template>
  <UiStack gap="none" data-testid="faq-category-page">
    <UiPageHero
      :badge="t('faq.badge')"
      :title="t(`seo.faqCategory.${slug}.h1`)"
      :subtitle="t(`seo.faqCategory.${slug}.description`)"
    >
      <template #breadcrumbs>
        <UiBreadcrumbs
          :items="breadcrumbs"
          container-class="mb-6 justify-center"
        />
      </template>

      <!-- Other categories -->
      <FaqCategoryNav :active="category" show-all class="mt-10" />
    </UiPageHero>

    <!-- FAQs for this category -->
    <UiSection padding="lg">
      <UiStack class="w-full" gap="xl">
        <!-- Empty state -->
        <UiEmptyState
          v-if="categoryFaqs.length === 0"
          :title="t('faq.noResults')"
          data-testid="faq-empty"
        >
          <UiButton :to="localePath(SITE_FAQ_PATH)" variant="ghost" data-testid="faq-view-all">
            {{ t('faq.viewAll') }}
          </UiButton>
        </UiEmptyState>

        <!-- FAQ accordion -->
        <UiAccordion
          v-else
          :items="accordionItems"
          :default-open="[0]"
          data-testid="faq-accordion"
        />

        <!-- Back to all FAQs -->
        <UiStack align="center">
          <UiButton
            :to="localePath(SITE_FAQ_PATH)"
            variant="subtle"
            size="lg"
            data-testid="faq-back-to-all"
          >
            <ArrowLeft :size="16" :stroke-width="2" class="mr-2" />
            {{ t('faq.backToAll') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiSection>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * FAQ topic page: the questions of one topic as an accordion, with its own SEO tags and
 * structured data. An unknown topic is a 404.
 */
import { ArrowLeft } from 'lucide-vue-next'
import { useStorefrontStore } from '~/stores/storefront'
import type { FaqCategory } from '~/types/faqcategory'
import { SeoPageType } from '~/types/seopagetype'
import { faqCategoryFromParam, faqCategorySlug } from '~/utils/faqCategory'
import { SITE_FAQ_PATH, siteFaqCategoryPath } from '~/constants/siteRoutes'

/** The current route. */
const route = useRoute()
/** Translates a key in the active language. */
const { t, locale } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// An unknown topic is a 404 on every navigation, including a param change on the same page
definePageMeta({
  validate: to => faqCategoryFromParam(to.params.category as string | string[] | undefined) !== undefined
})

/** The topic named by the first render's route slug, or `undefined` when the slug names none. */
const initial = faqCategoryFromParam(route.params.category as string | string[] | undefined)

// 404 if the route names a topic that does not exist
if (initial === undefined) {
  throw createError({ statusCode: 404, statusMessage: 'FAQ category not found' })
}

/** The topic named by the route; follows the route when its parameter changes. */
const category = computed<FaqCategory>(() => faqCategoryFromParam(route.params.category as string | string[] | undefined) ?? initial)

/** The topic's lowercase slug: the URL segment and the locale-key suffix. */
const slug = computed(() => faqCategorySlug(category.value))

// The questions are read before the structured data is built, and again when the language changes.
await useAsyncData('storefront:faqs', () => storefront.loadFaqs(), { watch: [locale] })

/** The trail above the title: the FAQ landing page, then this topic. */
const breadcrumbs = computed(() => [
  { label: t('faq.title'), to: localePath(SITE_FAQ_PATH) },
  { label: t(`faq.categories.${slug.value}`) }
])

/** The questions of this topic, already worded in the active language by the API. */
const categoryFaqs = computed(() => storefront.faqsIn(category.value))

/** The questions in the shape the accordion takes. */
const accordionItems = computed(() => categoryFaqs.value.map(faq => ({
  title: faq.question,
  content: faq.answer
})))

// Per-category SEO
useSeo({
  title: t(`seo.faqCategory.${slug.value}.title`, { site: siteName.value }),
  description: t(`seo.faqCategory.${slug.value}.description`, { site: siteName.value }),
  keywords: t(`seo.faqCategory.${slug.value}.keywords`, { site: siteName.value }),
  type: SeoPageType.Website,
  path: siteFaqCategoryPath(slug.value)
})

// FAQ schema.org for this category only
/** Builder and publisher of the FAQ structured data. */
const { faqSchema, injectSchema } = useSchemaOrg()
injectSchema(faqSchema(categoryFaqs.value.map(faq => ({ question: faq.question, answer: faq.answer }))))
</script>
