<template>
  <UiText :as="UiTextTag.Section" size="inherit" tone="inherit" :aria-label="t('nova.trust.performance')" class="border-b border-nova-border bg-nova-surface">
    <UiContainer class="py-2">
      <UiTrustBar
        :items="entries"
        surface-class=""
        text-class="text-nova-ink"
        chip-class="bg-nova-brand/10 text-nova-brand"
      />
    </UiContainer>
  </UiText>
</template>

<script setup lang="ts">
/**
 * nova trust bar.
 *
 * Qualitative by design. Every line states something the platform does rather
 * than how well it does it, which is the only kind of claim this codebase can
 * stand behind: with the single exception of the uptime figure, which is the
 * operator's to publish and stays hidden until they do.
 */
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { TRUST_ITEMS } from '~/templates/nova/content'
import { useVisibleFeatures } from '~/templates/nova/features'
import { UiTextTag } from '~/utils/ui/enums'

/** Translator for the copy. */
const { t } = useI18n()
/** Reads the operator settings. */
const { get } = usePortalSettings()

/** The trust lines this install may state. */
const items = useVisibleFeatures(TRUST_ITEMS)
/** The operator's published uptime figure; empty hides the line. */
const uptime = computed(() => get(PortalSettingKey.StatsUptime, 'portalStatsUptime'))

/**
 * Bar entries: the uptime line first, only when the operator entered a figure (this project
 * measures nothing, so a default would be a number somebody made up), then the visible lines.
 */
const entries = computed(() => [
  ...(uptime.value ? [{ key: 'uptime', icon: 'lucide:activity', label: t('nova.trust.uptimeLabel'), value: uptime.value }] : []),
  ...items.value.map(item => ({ key: item.key, icon: item.icon, label: t(item.titleKey) })),
])
</script>
