<script setup lang="ts">
/**
 * What an update changes: the release notes of every version between the installed one and the one being
 * installed, newest first, each under its number and release date. A version without notes says so, so a
 * gap in the list is never mistaken for a version that was skipped. With nothing installed it shows the notes
 * of the chosen version alone. The component owns no data: the page hands it the registry's versions.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { versionsBetween } from '@/components/plugins/details/versionOrder'
import UiMarkdown from '@/components/ui/UiMarkdown.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatDate } from '@/lib/format'
import type { RegistryPluginVersionDto } from '@/types/plugin'
import { UiTextTag } from '@/utils/ui/text'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginUpdateChanges. */
const props = defineProps<{
  /** Every version the registry lists for the plugin. */
  versions: RegistryPluginVersionDto[]
  /** The installed version, null when the plugin is not installed. */
  installedVersion: string | null
  /** The version about to be installed. */
  targetVersion: string
}>()

/** The versions the update passes through, newest first. */
const changes = computed<RegistryPluginVersionDto[]>(() =>
  versionsBetween(props.versions, props.installedVersion, props.targetVersion),
)

/** The heading: a comparison when something is installed, the plain title otherwise. */
const heading = computed<string>(() =>
  props.installedVersion === null
    ? t('pluginDetails.changes.headingFresh', { version: props.targetVersion })
    : t('pluginDetails.changes.heading', { from: props.installedVersion, to: props.targetVersion }),
)
</script>

<template>
  <UiStack v-if="changes.length > 0" gap="sm" data-testid="plugin-update-changes">
    <UiText tone="secondary" size="sm" class="font-semibold" data-testid="plugin-update-changes-heading">{{ heading }}</UiText>
    <UiStack v-for="entry in changes" :key="entry.version" gap="xs" :data-testid="`plugin-update-change-${entry.version}`">
      <UiText tone="primary" size="sm" class="font-semibold">
        <UiText :as="UiTextTag.Span" class="font-mono">{{ entry.version }}</UiText>
        <UiText :as="UiTextTag.Span" tone="muted" size="sm" class="font-normal"> · {{ formatDate(entry.publishedAt) }}</UiText>
      </UiText>
      <UiMarkdown :source="entry.changelog" :empty="t('pluginDetails.versions.noChangelog')" />
    </UiStack>
  </UiStack>
</template>
