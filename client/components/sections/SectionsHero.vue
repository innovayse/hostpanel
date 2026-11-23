<template>
  <UiSection :contained="false" padding="none" class="bg-classic-page border-b border-classic-border" data-testid="sections-hero">
    <UiContainer class="py-16 md:py-24">
      <UiStack gap="none" class="text-center">
        <!-- Eyebrow badge: the operator's line when set -->
        <UiText :as="UiTextTag.Span" tone="inherit" size="sm" weight="medium" class="inline-flex items-center self-center px-3 py-1 mb-6 rounded-md bg-classic-brand-soft text-classic-brand" data-testid="hero-badge">
          {{ badge }}
        </UiText>

        <UiHeading :level="1" size="display" class="text-4xl sm:text-5xl lg:text-6xl mb-5 leading-tight tracking-tight text-classic-ink">
          {{ $t('hero.title') }}
        </UiHeading>

        <UiText size="lg" class="md:text-xl mb-10 max-w-[75ch] mx-auto text-classic-muted">
          {{ $t('hero.subtitle') }}
        </UiText>

        <!-- Main action: the domain search -->
        <SectionsDomainSearch class="mb-5" />

        <!-- Secondary action -->
        <UiButton :to="localePath(SITE_HOSTING_PATH)" variant="outline" size="lg" class="self-center mb-14" data-testid="hero-secondary">
          {{ $t('cta.button') }}
          <ArrowRight :size="18" :stroke-width="2" class="ml-2" />
        </UiButton>

        <!-- Figures: only the ones the operator configured -->
        <UiStatGroup v-if="stats.length > 0" :stats="stats" data-testid="hero-stats" />
      </UiStack>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Hero of the home page when no product slide is configured: a hosting headline, the domain search
 * as the main action, "See hosting plans" as the secondary one and the operator's headline figures
 * (`portal.stats.*`, each left out while empty). The badge line is the operator's `portal.hero.badge`
 * setting when set, the translated default otherwise.
 */

import { ArrowRight } from 'lucide-vue-next'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SITE_HOSTING_PATH } from '~/constants/siteRoutes'
import { UiTextTag } from '~/utils/ui/enums'

/** Where a headline figure's value and caption come from. */
interface StatDefinition {
  /** Setting that holds the figure. */
  settingKey: PortalSettingKey
  /** Runtime-config fallback of the setting. */
  envKey: string
  /** Locale key of the caption. */
  labelKey: string
}

/** The headline figures, in display order. */
const STAT_DEFINITIONS: StatDefinition[] = [
  { settingKey: PortalSettingKey.StatsClients, envKey: 'portalStatsClients', labelKey: 'hero.stats.clients' },
  { settingKey: PortalSettingKey.StatsUptime, envKey: 'portalStatsUptime', labelKey: 'hero.stats.uptime' },
  { settingKey: PortalSettingKey.StatsRegions, envKey: 'portalStatsRegions', labelKey: 'hero.stats.regions' },
]

/** Translation helpers and the active language. */
const { t } = useI18n()
/** Operator settings, with their runtime-config fallbacks. */
const { get } = usePortalSettings()
/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** The badge line: the operator's setting, or the translated default. */
const badge = computed(() => get(PortalSettingKey.HeroBadge, 'portalHeroBadge') || t('hero.badge'))

/** The figures the operator configured, ready for the stat group; one that is empty is left out. */
const stats = computed(() => STAT_DEFINITIONS.flatMap((definition) => {
  const value = get(definition.settingKey, definition.envKey)
  return value === '' ? [] : [{ value, label: t(definition.labelKey) }]
}))
</script>
