<script setup lang="ts">
/**
 * Quiet "Hostpanel 1.4.0" line showing the installed version.
 *
 * Shown in the sidebar footer and on the Settings page. Both read the shared release status
 * store, whose single request is started here on mount; the line stays absent until the version
 * is known and stays absent for good when the load fails, so a refusal never becomes an error.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import UiText from '@/components/ui/UiText.vue'
import { useReleaseStatusStore } from '@/stores/releaseStatus'

/** Props of the version label. */
withDefaults(defineProps<{
  /** The `data-testid` of the line, so each place that shows it can be found on its own. */
  testid?: string
}>(), {
  testid: 'version-label',
})

/** Translation function. */
const { t } = useI18n()

/** Release status store: the one request shared by every place that shows the version. */
const releaseStatus = useReleaseStatusStore()

/** The installed version, or an empty string while it is unknown or its load failed. */
const version = computed<string>(() => releaseStatus.status?.installedVersion ?? '')

/** Starts the shared load; later mounts of the label reuse it. */
onMounted(() => {
  void releaseStatus.ensureLoaded()
})
</script>

<template>
  <UiText v-if="version" tone="muted" size="sm" :data-testid="testid">{{ t('layout.version.label', { version }) }}</UiText>
</template>
