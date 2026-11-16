<script setup lang="ts">
/**
 * One tab's worth of settings: its cards in the default slot and one sticky bar for that tab only.
 *
 * The panel provides its own draft registry, so every control below registers with it and the bar counts,
 * saves and discards the unsaved edits of this tab, not of the whole screen. While `loading` is set the
 * bar is left out, since there is nothing to edit yet, and every card below draws its fields as placeholders
 * (the panel provides `UI_FIELD_LOADING`), so the tab keeps the shape it will have.
 */
import { computed, provide, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsFormFooter from '@/components/settings/SettingsFormFooter.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { provideSettingsDraftRegistry } from '@/composables/useSettingsDraftRegistry'
import type { SettingSave } from '@/types/setting'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'

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

// Every card and field below reads this flag, so the panel needs no per-card loading prop.
provide(UI_FIELD_LOADING, toRef(props, 'loading'))

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
