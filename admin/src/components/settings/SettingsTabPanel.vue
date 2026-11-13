<script setup lang="ts">
/**
 * One tab's worth of settings: its cards in the default slot and one sticky bar for that tab only.
 *
 * The panel provides its own draft registry, so every control below registers with it and the bar counts,
 * saves and discards the unsaved edits of this tab, not of the whole screen. While `loading` is set the
 * bar is left out, since there is nothing to edit yet.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsFormFooter from '@/components/settings/SettingsFormFooter.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { provideSettingsDraftRegistry } from '@/composables/useSettingsDraftRegistry'
import type { SettingSave } from '@/types/setting'

/** Props for SettingsTabPanel. */
const props = withDefaults(defineProps<{
  /** Persists one setting; the bar's Save all runs every unsaved draft of the tab through it. */
  save: SettingSave
  /** True while the settings are first read; the bar is hidden. */
  loading?: boolean
}>(), {
  loading: false,
})

const { t } = useI18n()

/** Collects the drafts of every control below, for this tab's bar. */
const registry = provideSettingsDraftRegistry()

/** The bar's status line: how many edits are unsaved. */
const unsavedLabel = computed<string>(() =>
  registry.dirtyCount.value === 0
    ? t('settings.saveBar.clean')
    : t('settings.saveBar.unsaved', { count: registry.dirtyCount.value }, registry.dirtyCount.value),
)
</script>

<template>
  <UiStack gap="none" class="flex-1" data-testid="settings-tab-panel">
    <slot />
    <!-- Pinned to the bottom of the content column: unsaved count, save all, discard all. -->
    <SettingsFormFooter
      v-if="!props.loading"
      class="mt-auto"
      :save-label="t('settings.saveBar.saveAll')"
      :cancel-label="t('settings.saveBar.discard')"
      :save-label-compact="t('settings.saveBar.saveShort')"
      :cancel-label-compact="t('settings.saveBar.discardShort')"
      :saving="registry.saving.value"
      :dirty="registry.dirtyCount.value > 0"
      :status="unsavedLabel"
      @save="registry.saveAll(props.save)"
      @cancel="registry.discardAll()"
    />
  </UiStack>
</template>
