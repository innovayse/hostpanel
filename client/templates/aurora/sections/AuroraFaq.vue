<template>
  <UiSection v-if="entries.length" id="faq" padding="lg" :contained="false" class="relative border-t border-line">
    <UiSplit ratio="equal" gap="xl">
      <template #start>
        <UiStack gap="none" :class="STICKY_COLUMN_CLASS" data-testid="faq-aside">
          <UiHeading :level="2" size="bare" :class="BLOCK_TITLE_CLASS">
            {{ t('aurora.faq.title') }}
          </UiHeading>
          <UiText tone="inherit" class="mt-4 text-[17px] leading-relaxed text-mut">{{ t('aurora.faq.lead') }}</UiText>
          <UiStack direction="row" wrap class="mt-8">
            <UiButton
              variant="bare"
              :to="localePath(SITE_CONTACT_PATH)"
              data-testid="faq-contact"
              :class="[BRAND_BUTTON, 'px-6 py-3 text-[15px]']"
            >
              {{ t('aurora.faq.contact') }}
            </UiButton>
          </UiStack>
          <UiStack v-if="email || phone" gap="xs" class="mt-5 text-[14px] text-mut" data-testid="faq-support">
            <UiLink v-if="email" :href="`mailto:${email}`" tone="inherit" :underline="false" data-testid="faq-email" class="hover:text-ac1">
              {{ email }}
            </UiLink>
            <UiLink v-if="phone" :href="`tel:${phoneLink}`" tone="inherit" :underline="false" data-testid="faq-phone" class="hover:text-ac1">
              {{ phone }}
            </UiLink>
          </UiStack>
        </UiStack>
      </template>
      <template #end>
        <!-- Answers stay in the DOM: this is indexable content and a crawler should read every one. -->
        <UiAccordion :items="entries" keep-content :default-open="[0]" data-testid="aurora-faq" />
        <UiStack v-if="hasMore" direction="row" class="mt-8">
          <UiButton
            variant="bare"
            :to="localePath(SITE_FAQ_PATH)"
            data-testid="faq-view-all"
            :class="[LINE_BUTTON, 'px-6 py-3 text-[15px]']"
          >
            {{ t('aurora.faq.viewAll') }}
          </UiButton>
        </UiStack>
      </template>
    </UiSplit>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora FAQ accordion beside a sticky intro with a contact call to action. The questions come
 * from the storefront store (`GET /api/public/faqs`); with none the section renders nothing.
 * Answers stay in the DOM so they remain indexable. The support email and phone shown under the
 * button are the operator's portal settings and are hidden when unset. Only the first
 * `HOME_FAQ_LIMIT` questions are listed, with a link to the full `/faq` page when more exist.
 */
import { BRAND_BUTTON, LINE_BUTTON } from '~/templates/aurora/buttons'
import { STICKY_COLUMN_CLASS } from '~/templates/aurora/layoutClasses'
import { BLOCK_TITLE_CLASS } from '~/templates/aurora/typography'
import { HOME_FAQ_LIMIT } from '~/constants/faq'
import { useStorefrontStore } from '~/stores/storefront'
import { SITE_CONTACT_PATH, SITE_FAQ_PATH } from '~/constants/siteRoutes'

/** Translator and the active language. */
const { t, locale } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The support email and phone the operator configured; each is empty when unset. */
const { email, phone, phoneLink } = useContactDetails()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:faqs', () => storefront.loadFaqs(), { watch: [locale] })

/** Whether more questions exist than the home page lists, so the link to the full page is worth showing. */
const hasMore = computed(() => storefront.faqs.length > HOME_FAQ_LIMIT)

/** The first `HOME_FAQ_LIMIT` questions in the shape the accordion takes. */
const entries = computed(() => storefront.faqs.slice(0, HOME_FAQ_LIMIT).map(faq => ({ title: faq.question, content: faq.answer })))
</script>
