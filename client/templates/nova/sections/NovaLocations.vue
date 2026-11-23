<template>
  <UiSection v-if="items.length" padding="lg" class="border-b border-nova-border bg-nova-surface" data-testid="nova-locations">
    <NovaSectionHeader
      :eyebrow="t('nova.locations.eyebrow')"
      :title="t('nova.locations.title')"
      :lead="t('nova.locations.lead')"
    />

    <UiBalancedGrid :as="UiGridTag.Ul" :count="items.length" class="mt-10" data-testid="nova-locations-grid">
      <UiStack v-for="location in items" :key="location.id" :as="UiStackTag.Li" gap="none" class="h-full" :data-testid="`nova-location-${location.id}`">
        <UiCard padding="lg" class="h-full !border-nova-border !bg-nova-bg">
          <UiStack direction="row" align="center" gap="md">
            <UiIconTile size="lg" tone="nova" class="text-[13px] font-extrabold tracking-wider">{{ location.countryCode }}</UiIconTile>
            <UiStack gap="none" class="min-w-0">
              <UiHeading :level="3" size="bare" class="text-[17px] font-bold text-nova-ink">{{ location.name }}</UiHeading>
              <UiText v-if="location.city" :as="UiTextTag.P" size="inherit" tone="inherit" class="text-[14px] text-nova-muted">
                {{ location.city }}
              </UiText>
            </UiStack>
          </UiStack>
          <UiText
            v-if="location.latencyNote"
            :as="UiTextTag.P"
            size="inherit"
            tone="inherit"
            class="mt-4 text-[14px] leading-relaxed text-nova-muted"
          >
            {{ location.latencyNote }}
          </UiText>
        </UiCard>
      </UiStack>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova server locations: one card per active location with its country code, name, city and
 * latency note. No flag emoji and no map; the code is plain text. The locations come from the
 * storefront store (`GET /api/public/locations`); with none the section renders nothing.
 */
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { useStorefrontStore } from '~/stores/storefront'
import { UiGridTag, UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Translator and the active language. */
const { t, locale } = useI18n()

/** The storefront content: partners, locations and the rest. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:locations', () => storefront.loadLocations(), { watch: [locale] })

/** The locations, in display order. */
const items = computed(() => storefront.locations)
</script>
