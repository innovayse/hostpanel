<script setup lang="ts">
/**
 * The versions of a plugin, newest first as the registry sends them, one {@link PluginVersionRow}
 * each. Shows an empty note when the registry lists none. The component owns no data and installs
 * nothing: it passes the row's install request up.
 */
import { useI18n } from 'vue-i18n'
import PluginVersionRow from '@/components/plugins/details/PluginVersionRow.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import type { RegistryPluginVersionDto } from '@/types/plugin'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginVersionList. */
defineProps<{
  /** Every version the registry lists. */
  versions: RegistryPluginVersionDto[]
  /** The installed version, null when the plugin is not installed. */
  installedVersion: string | null
  /** The version staged for the next restart, null when none. */
  stagedVersion: string | null
  /** The newest version the registry offers. */
  latestVersion: string
  /** True while an install of this plugin is in flight. */
  busy: boolean
}>()

/** Events emitted by PluginVersionList. */
const emit = defineEmits<{
  /** Emitted with the version number when the operator asks to install a version. */
  install: [version: string]
}>()
</script>

<template>
  <UiStack gap="sm" data-testid="plugin-details-versions">
    <UiHeading :level="2" variant="section">{{ t('pluginDetails.versions.heading') }}</UiHeading>
    <UiEmptyState
      v-if="versions.length === 0"
      :title="t('pluginDetails.versions.emptyTitle')"
      :description="t('pluginDetails.versions.emptyDescription')"
      data-testid="plugin-details-versions-empty"
    />
    <template v-else>
      <PluginVersionRow
        v-for="version in versions"
        :key="version.version"
        :version="version"
        :installed-version="installedVersion"
        :staged-version="stagedVersion"
        :latest-version="latestVersion"
        :busy="busy"
        @install="emit('install', $event)"
      />
    </template>
  </UiStack>
</template>
