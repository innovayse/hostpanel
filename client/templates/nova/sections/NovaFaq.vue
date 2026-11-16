<template>
  <UiSection v-if="faqs.length" padding="lg" class="border-b border-nova-border">
    <NovaSectionHeader :eyebrow="t('nova.faq.eyebrow')" :title="t('nova.faq.title')" />

    <!-- Native disclosure on purpose: see the script header. -->
    <UiStack gap="sm" class="mt-9">
      <details
        v-for="item in faqs"
        :key="item.id"
        class="group rounded-2xl border border-nova-border bg-nova-surface px-5"
        :data-testid="`nova-faq-${item.id}`"
      >
        <summary
          class="flex min-h-[56px] cursor-pointer list-none items-center justify-between gap-4 py-4 text-[16px] font-semibold text-nova-ink marker:content-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-nova-brand"
        >
          <UiHeading :level="3" size="bare" class="min-w-0 text-[16px] font-semibold">
            {{ item.question }}
          </UiHeading>
          <Icon
            name="lucide:chevron-down"
            class="h-5 w-5 shrink-0 text-nova-muted transition-transform group-open:rotate-180"
            aria-hidden="true"
          />
        </summary>
        <UiText size="inherit" tone="inherit" class="pb-5 text-[15px] leading-[1.7] text-nova-muted">
          {{ item.answer }}
        </UiText>
      </details>
    </UiStack>

    <UiStack v-if="hasMore" direction="row" class="mt-6">
      <UiLink
        :to="localePath(SITE_FAQ_PATH)"
        tone="inherit"
        :underline="false"
        class="inline-flex items-center gap-2 text-[15px] font-semibold text-nova-brand"
        testid="nova-faq-view-all"
      >
        {{ t('nova.faq.viewAll') }}
        <Icon name="lucide:arrow-right" class="h-4 w-4" aria-hidden="true" />
      </UiLink>
    </UiStack>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova FAQ accordion.
 *
 * Built on <details>/<summary> rather than on JavaScript state, so it is not a kit
 * component: the element is already operable by keyboard, already announces its
 * expanded state, and already works before hydration: three things a hand-rolled
 * accordion has to reimplement and usually gets partly wrong. The headings and
 * answers inside it are kit text; the chevron is the only thing scripted, and it
 * is scripted in CSS. The questions come from the storefront store (`GET /api/public/faqs`); with
 * none the section renders nothing. Only the first `HOME_FAQ_LIMIT` questions are listed, with a
 * link to the full `/faq` page when more exist.
 */
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { HOME_FAQ_LIMIT } from '~/constants/faq'
import { useStorefrontStore } from '~/stores/storefront'
import { SITE_FAQ_PATH } from '~/constants/siteRoutes'

/** Translator for the section copy, and the active language. */
const { t, locale } = useI18n()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:faqs', () => storefront.loadFaqs(), { watch: [locale] })

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The first `HOME_FAQ_LIMIT` questions, in display order. */
const faqs = computed(() => storefront.faqs.slice(0, HOME_FAQ_LIMIT))

/** Whether more questions exist than the home page lists, so the link to the full page is worth showing. */
const hasMore = computed(() => storefront.faqs.length > HOME_FAQ_LIMIT)
</script>
