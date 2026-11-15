<template>
  <UiCard
    tone="bare"
    padding="none"
    data-testid="aurora-cta"
    class="relative mb-[clamp(56px,8vw,84px)] overflow-hidden border-line2 bg-cta-card px-[clamp(22px,4vw,64px)] py-[clamp(36px,5vw,64px)] text-center shadow-panel"
  >
    <!-- Decoration: a dot matrix faded toward the edges and two brand glows in opposite corners. -->
    <UiDecor class="inset-0 pattern-dots-20 opacity-60 [mask-image:radial-gradient(ellipse_at_center,black,transparent_75%)]" data-testid="cta-pattern" />
    <UiDecor class="-left-24 -top-24 h-72 w-72 rounded-full bg-glow1 opacity-80 blur-3xl" />
    <UiDecor class="-bottom-24 -right-24 h-72 w-72 rounded-full bg-glow2 opacity-80 blur-3xl" />
    <UiHeading :level="2" size="bare" class="relative font-display text-[clamp(28px,4.4vw,42px)] font-bold -tracking-[0.02em] text-tx">
      {{ t('aurora.cta.title') }}
    </UiHeading>
    <UiText size="inherit" tone="inherit" class="relative mx-auto mt-4 max-w-[75ch] text-[18px] leading-relaxed text-tx2">
      {{ t('aurora.cta.lead') }}
    </UiText>
    <UiStack direction="row" wrap justify="center" class="relative mt-8">
      <UiButton
        variant="bare"
        :to="localePath(SITE_CONTACT_PATH)"
        data-testid="cta-primary"
        :class="[BRAND_BUTTON, 'px-7 py-[15px] text-base']"
      >
        {{ t('aurora.cta.primary') }}
      </UiButton>
      <UiButton
        variant="bare"
        :to="localePath(SITE_HOSTING_PATH)"
        data-testid="cta-secondary"
        :class="[LINE_BUTTON, 'bg-panel px-7 py-[15px] text-base']"
      >
        {{ t('aurora.cta.secondary') }}
      </UiButton>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/** aurora closing call to action. */
import { BRAND_BUTTON, LINE_BUTTON } from '~/templates/aurora/buttons'
import { SITE_CONTACT_PATH, SITE_HOSTING_PATH } from '~/constants/siteRoutes'

/** Translator for the active locale. */
const { t } = useI18n()
/** Prefixes a path with the active locale. */
const localePath = useLocalePath()
</script>
