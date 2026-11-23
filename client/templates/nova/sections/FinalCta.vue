<template>
  <UiSection padding="lg">
    <UiCard
      padding="none"
      class="rounded-3xl bg-[linear-gradient(135deg,var(--n-surface-2),var(--n-surface))] px-6 py-14 text-center sm:px-12 !border-nova-border"
    >
      <UiHeading
        :level="2"
        size="bare"
        class="mx-auto text-[clamp(1.7rem,4vw,2.7rem)] font-extrabold leading-tight tracking-tight text-nova-ink"
      >
        {{ t('nova.finalCta.title') }}
      </UiHeading>
      <UiText
        size="inherit"
        tone="inherit"
        class="mx-auto mt-4 max-w-[75ch] text-[17px] leading-[1.7] text-nova-muted"
      >
        {{ t('nova.finalCta.lead') }}
      </UiText>

      <UiStack direction="row" wrap justify="center" gap="sm" class="mt-9">
        <NovaCta :to="localePath(SITE_HOSTING_PATH)" size="lg" lift class="px-8" data-testid="nova-final-cta-primary">
          {{ t('nova.cta.getStarted') }}
        </NovaCta>
        <NovaCta
          :to="localePath(SITE_HOSTING_PLANS_PATH)"
          tone="outline"
          size="lg"
          class="px-8 bg-nova-bg"
          data-testid="nova-final-cta-secondary"
        >
          {{ t('nova.cta.seePlans') }}
        </NovaCta>
      </UiStack>
    </UiCard>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova closing call to action. The amber button is the page's highest-contrast
 * element on purpose: it is the one thing the section is asking for.
 */
import NovaCta from '~/templates/nova/sections/NovaCta.vue'
import { SITE_HOSTING_PATH, SITE_HOSTING_PLANS_PATH } from '~/constants/siteRoutes'

/** Translator for the copy. */
const { t } = useI18n()
/** Prefixes a route with the active locale. */
const localePath = useLocalePath()
</script>
