<script setup lang="ts">
/**
 * Warning shown in place of a settings control whose row the API never seeded.
 *
 * The backend has no create endpoint for settings, so a missing key cannot be added from the
 * panel. Saying so beats a control that silently does nothing. Seeding runs on API start, so this
 * only ever appears against an API older than this panel. While the settings are still being read (the
 * surrounding panel provides `UI_FIELD_LOADING`) no row is there yet either, so it draws the placeholder of the
 * control instead of the warning.
 */
import { inject, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiField from '@/components/ui/UiField.vue'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'

/** Translation function. */
const { t } = useI18n()

/** True while the settings are being read, when a missing row only means the answer has not arrived. */
const loading = inject(UI_FIELD_LOADING, ref(false))

/** Props for SettingNotSeeded. */
defineProps<{
  /** The settings key that has no row. */
  settingKey: string
  /** The environment variable the portal falls back to until the row exists. */
  envName: string
}>()
</script>

<template>
  <UiField v-if="loading" loading data-testid="setting-loading" />
  <UiAlert v-else variant="warning" data-testid="setting-not-seeded">
    {{ t('settings.portalAppearance.notSeeded', { key: settingKey, env: envName }) }}
  </UiAlert>
</template>
