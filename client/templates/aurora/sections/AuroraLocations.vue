<template>
  <UiSection v-if="items.length" padding="lg" :contained="false" class="relative border-t border-line" data-testid="aurora-locations">
    <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac1']">{{ t('aurora.locations.eyebrow') }}</UiText>
    <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
      {{ t('aurora.locations.title') }}
    </UiHeading>

    <UiBalancedGrid :count="items.length" class="mt-8" data-testid="aurora-locations-grid">
      <UiCard
        v-for="location in items"
        :key="location.id"
        tone="bare"
        :data-testid="`location-${location.id}`"
        class="border-line bg-surf shadow-card transition-[border-color,transform] duration-200 hover:-translate-y-0.5 hover:border-ac1"
      >
        <UiStack direction="row" align="center" gap="md">
          <UiTile size="xl" class="bg-tile-sky text-white" :data-testid="`location-code-${location.id}`">
            <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="bold" class="text-[13px] tracking-wider">{{ location.countryCode }}</UiText>
          </UiTile>
          <UiStack gap="none">
            <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="bold" class="text-[17px] text-tx">{{ location.name }}</UiText>
            <UiText v-if="location.city" :as="UiTextTag.Span" size="inherit" tone="inherit" class="text-[14px] text-mut" :data-testid="`location-city-${location.id}`">{{ location.city }}</UiText>
            <UiText v-if="location.latencyNote" :as="UiTextTag.Span" size="inherit" tone="inherit" class="text-[13px] text-ac2" :data-testid="`location-latency-${location.id}`">{{ location.latencyNote }}</UiText>
          </UiStack>
        </UiStack>
      </UiCard>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora server locations: one card per data centre the operator lists, with a country-code chip
 * (text, no flags or maps), the name, the city and the latency note. The list comes from the
 * storefront store (`GET /api/public/locations`); with none the section renders nothing.
 */
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import { useStorefrontStore } from '~/stores/storefront'
import UiStack from '~/components/ui/UiStack.vue'
import { UiTextTag } from '~/utils/ui/enums'

/** Translator and the active language. */
const { t, locale } = useI18n()

/** The storefront content, including the server locations. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:locations', () => storefront.loadLocations(), { watch: [locale] })

/** The locations, in display order. */
const items = computed(() => storefront.locations)
</script>
