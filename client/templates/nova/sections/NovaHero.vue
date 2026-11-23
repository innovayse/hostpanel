<template>
  <UiText :as="UiTextTag.Section" size="inherit" tone="inherit" class="relative overflow-hidden border-b border-nova-border">
    <!-- Decorative wash. aria-hidden because it carries no information. -->
    <UiDecor
      class="-top-40 left-1/2 h-[560px] w-[900px] -translate-x-1/2 rounded-full bg-[radial-gradient(closest-side,var(--n-brand),transparent)] opacity-20"
    />

    <UiContainer class="relative grid items-center gap-12 py-12 lg:grid-cols-2 lg:py-20">
      <UiStack gap="none" class="n-reveal min-w-0">
        <UiText
          size="inherit"
          tone="inherit"
          class="inline-flex items-center gap-2 rounded-full border border-nova-border bg-nova-surface px-3.5 py-1.5 text-[13px] text-nova-muted"
        >
          <UiStack :as="UiStackTag.Span" gap="none" class="h-1.5 w-1.5 rounded-full bg-nova-success" aria-hidden="true" />
          {{ t('nova.hero.badge') }}
        </UiText>

        <UiHeading
          :level="1"
          size="bare"
          class="mt-5 text-[clamp(2rem,5.5vw,3.5rem)] font-extrabold leading-[1.08] tracking-tight text-nova-ink"
        >
          {{ t('nova.hero.title') }}
        </UiHeading>

        <UiText size="inherit" tone="inherit" class="mt-5 max-w-[75ch] text-[17px] leading-[1.7] text-nova-muted">
          {{ t('nova.hero.lead', { site: siteName }) }}
        </UiText>

        <UiStack direction="row" wrap gap="sm" class="mt-8">
          <NovaCta :to="localePath(SITE_HOSTING_PATH)" size="lg" lift data-testid="nova-hero-cta-primary">
            {{ t('nova.cta.getStarted') }}
          </NovaCta>
          <NovaCta
            :to="localePath(SITE_HOSTING_PLANS_PATH)"
            tone="outline"
            size="lg"
            data-testid="nova-hero-cta-secondary"
          >
            {{ t('nova.cta.seePlans') }}
          </NovaCta>
        </UiStack>

        <UiGrid :as="UiGridTag.Ul" cols="2" gap="none" class="mt-9 gap-x-6 gap-y-3">
          <UiStack
            v-for="benefit in benefits"
            :key="benefit.key"
            :as="UiStackTag.Li"
            direction="row"
            align="center"
            gap="none"
            class="gap-2.5 text-[15px] text-nova-ink"
          >
            <Icon :name="benefit.icon" class="h-[18px] w-[18px] shrink-0 text-nova-success" aria-hidden="true" />
            {{ t(benefit.titleKey) }}
          </UiStack>
        </UiGrid>
      </UiStack>

      <NovaDashboard compact />
    </UiContainer>
  </UiText>
</template>

<script setup lang="ts">
/**
 * nova hero: the page's first conversion point, and the owner of its only h1.
 *
 * The benefit list is filtered through `useVisibleFeatures`, so a claim this
 * install cannot back never reaches the fold. Nothing here is a number: uptime
 * and customer counts are measurements, and the one place a figure can appear
 * is the trust bar, where it is read from an operator setting.
 */
import NovaCta from '~/templates/nova/sections/NovaCta.vue'
import NovaDashboard from '~/templates/nova/sections/NovaDashboard.vue'
import { HERO_BENEFITS } from '~/templates/nova/content'
import { useVisibleFeatures } from '~/templates/nova/features'
import { SITE_HOSTING_PATH, SITE_HOSTING_PLANS_PATH } from '~/constants/siteRoutes'
import { UiGridTag, UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Translator for the copy. */
const { t } = useI18n()

/** Operator-set site name that the copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()
/** Prefixes a route with the active locale. */
const localePath = useLocalePath()
/** The benefits this install may state. */
const benefits = useVisibleFeatures(HERO_BENEFITS)
</script>
