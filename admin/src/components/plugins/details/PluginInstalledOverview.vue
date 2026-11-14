<script setup lang="ts">
/**
 * The overview of an installed plugin, read from this server's own list (so it exists for every installed plugin,
 * whether or not the registry knows it): version, category, the capabilities the manifest declares, the server
 * versions it runs on, the rule it broke when the host refused it, and the release notes of the installed version.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import type { PluginListItemDto } from '@/types/plugin'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginInstalledOverview. */
const props = defineProps<{
  /** The installed plugin, as this server lists it. */
  plugin: PluginListItemDto
}>()

/** The server versions the plugin runs on, in words. */
const hostRange = computed<string>(() => {
  const min = props.plugin.minHostVersion ?? null
  const max = props.plugin.maxHostVersion ?? null
  if (min !== null && max !== null) {
    return t('pluginDetails.versions.hostRange', { min, max })
  }
  if (min !== null) {
    return t('pluginDetails.versions.hostMin', { min })
  }
  if (max !== null) {
    return t('pluginDetails.versions.hostMax', { max })
  }
  return t('pluginDetails.versions.hostAny')
})
</script>

<template>
  <UiCard padding="md" data-testid="plugin-details-overview">
    <UiStack gap="sm">
      <UiHeading :level="2" variant="section">{{ t('pluginDetails.tabs.overview') }}</UiHeading>
      <UiText tone="secondary" size="sm" data-testid="plugin-details-overview-version">
        {{ t('pluginDetails.installedVersion', { version: plugin.version }) }}
      </UiText>
      <UiText tone="secondary" size="sm" data-testid="plugin-details-overview-category">
        {{ plugin.category }}
      </UiText>
      <UiText tone="secondary" size="sm" data-testid="plugin-details-overview-hosts">{{ hostRange }}</UiText>
      <UiStack v-if="plugin.capabilities.length > 0" direction="row" align="center" gap="sm" wrap data-testid="plugin-details-capabilities">
        <UiBadge
          v-for="capability in plugin.capabilities"
          :key="capability"
          :tone="UiTone.Neutral"
          small
          :data-testid="`plugin-details-capability-${capability}`"
        >
          {{ t(`plugins.pluginCard.capabilities.${capability}`) }}
        </UiBadge>
      </UiStack>
      <UiStack v-if="plugin.refusedRule" gap="xs" data-testid="plugin-details-refusal">
        <UiText tone="danger" size="sm">{{ t('plugins.pluginCard.brokenRule', { id: plugin.id, rule: plugin.refusedRule }) }}</UiText>
        <UiText v-if="plugin.refusedDetail" tone="muted" size="xs" class="font-mono break-all">{{ plugin.refusedDetail }}</UiText>
      </UiStack>
      <UiHeading :level="3" variant="section">{{ t('pluginDetails.versions.changelog') }}</UiHeading>
      <UiText v-if="plugin.changelog" tone="secondary" size="sm" class="whitespace-pre-line" data-testid="plugin-details-changelog">
        {{ plugin.changelog }}
      </UiText>
      <UiText v-else tone="muted" size="sm" data-testid="plugin-details-no-changelog">{{ t('pluginDetails.versions.noChangelog') }}</UiText>
    </UiStack>
  </UiCard>
</template>
