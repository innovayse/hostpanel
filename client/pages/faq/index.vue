<template>
  <UiStack gap="none" data-testid="faq-page">
    <UiPageHero
      :badge="t('faq.badge')"
      :title="t('faq.title')"
      :subtitle="t('faq.subtitle')"
    >
      <!-- Category links for SEO -->
      <FaqCategoryNav class="mt-10" />
    </UiPageHero>

    <!-- All FAQs -->
    <SectionsFAQ :show-header="false" :show-categories="false" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * FAQ landing page: the hero with the topic links, then every question.
 *
 * The page owns the SEO tags and the structured data; the answers are rendered by `SectionsFAQ`.
 */
import { useStorefrontStore } from '~/stores/storefront'
import { SeoPageType } from '~/types/seopagetype'
import { SITE_FAQ_PATH } from '~/constants/siteRoutes'

/** Translates a key in the active language. */
const { t, locale } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()

useSeo({
  title: t('seo.faq.title', { site: siteName.value }),
  description: t('seo.faq.description', { site: siteName.value }),
  keywords: t('seo.faq.keywords', { site: siteName.value }),
  type: SeoPageType.Website,
  path: SITE_FAQ_PATH
})

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// The questions are read before the structured data is built, and again when the language changes.
await useAsyncData('storefront:faqs', () => storefront.loadFaqs(), { watch: [locale] })

/** Builder and publisher of the FAQ structured data. */
const { faqSchema, injectSchema } = useSchemaOrg()

/** Every question and answer in the active language, for the FAQ structured data. */
const faqData = computed(() => storefront.faqs.map(faq => ({ question: faq.question, answer: faq.answer })))
injectSchema(faqSchema(faqData.value))
</script>
