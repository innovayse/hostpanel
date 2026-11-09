<script setup lang="ts">
/**
 * Quiet "Hostpanel 1.4.0" line showing the installed version.
 *
 * Shown in the sidebar footer and on the Settings page. Both read the version store, which the
 * layout loads once; the line stays absent until the version is known, and a failed load leaves
 * it absent, so a refusal never becomes an error.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiText from '@/components/ui/UiText.vue'
import { useVersionStore } from '@/stores/version'

/** Props of the version label. */
withDefaults(defineProps<{
  /** The `data-testid` of the line, so each place that shows it can be found on its own. */
  testid?: string
}>(), {
  testid: 'version-label',
})

/** Translation function. */
const { t } = useI18n()

/** Version store: the value every place that shows the version reads. */
const versionStore = useVersionStore()

/** The installed version, or an empty string while it is unknown or its load failed. */
const version = computed<string>(() => versionStore.version)
</script>

<template>
  <UiText v-if="version" tone="muted" size="sm" :data-testid="testid">{{ t('layout.version.label', { version }) }}</UiText>
</template>
