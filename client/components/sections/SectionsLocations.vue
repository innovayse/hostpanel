<template>
  <UiSection v-if="locations.length" :contained="false" padding="lg" class="bg-classic-surface border-y border-classic-border" data-testid="sections-locations">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-12"
        :eyebrow="$t('hostingSections.locations.badge')"
        :title="$t('hostingSections.locations.title')"
        :subtitle="$t('hostingSections.locations.subtitle')"
      />

      <UiBalancedGrid :count="locations.length" data-testid="sections-locations-grid">
        <ClassicCard
          v-for="location in locations"
          :key="location.id"
          hover
          :data-testid="`location-${location.id}`"
        >
          <UiStack direction="row" align="center" gap="md">
            <UiBadge variant="primary" size="lg" class="font-mono tracking-wider flex-shrink-0 !rounded-md !bg-classic-brand-soft !text-classic-brand" :data-testid="`location-code-${location.id}`">{{ location.countryCode }}</UiBadge>
            <UiStack gap="none" class="min-w-0">
              <UiHeading :level="3" size="bare" class="truncate text-lg font-semibold text-classic-ink">{{ location.name }}</UiHeading>
              <UiText v-if="location.city" tone="inherit" size="sm" class="truncate text-classic-muted" :data-testid="`location-city-${location.id}`">{{ location.city }}</UiText>
              <UiText v-if="location.latencyNote" tone="inherit" size="sm" class="text-classic-muted" :data-testid="`location-latency-${location.id}`">{{ location.latencyNote }}</UiText>
            </UiStack>
          </UiStack>
        </ClassicCard>
      </UiBalancedGrid>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Server locations section: one card per data-centre location with its country as a code badge
 * (no flag emoji, no map), the name, the city and a latency note. The locations come from the
 * storefront store (`GET /api/portal/public/locations`, localised by the API); with none the
 * section renders nothing.
 */
import { useStorefrontStore } from '~/stores/storefront'

/** The active language; the locations are re-read when it changes. */
const { locale } = useI18n()

/** The storefront content, including the server locations. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:locations', () => storefront.loadLocations(), { watch: [locale] })

/** The locations to show, in display order. */
const locations = computed(() => storefront.locations)
</script>
