<script setup lang="ts">
/**
 * One row of the Advanced settings table: the key, an input for its value and its description.
 *
 * The value is a draft of the page's save bar, so the row has no Save button of its own: the bar
 * counts the edit and saves it, and the row shows the outcome next to its input.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useSettingDraft } from '@/composables/useSettingDraft'
import { EMPTY_VALUE } from '@/lib/format'
import type { Setting } from '@/types/setting'

const { t } = useI18n()

/** Props for RawSettingRow. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** The key this row edits. */
  settingKey: string
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Draft of this row's value; it registers in the page's draft registry. */
const draft = useSettingDraft(settings, props.settingKey)
</script>

<template>
  <UiTableRow v-if="draft.setting.value" hoverable :data-testid="`setting-row-${draft.setting.value.id}`">
    <UiTableCell class="font-mono text-text-primary">{{ draft.setting.value.key }}</UiTableCell>
    <UiTableCell>
      <UiStack gap="xs">
        <UiInput
          v-model="draft.value.value"
          type="text"
          size="sm"
          :placeholder="t('shared.value')"
          :data-testid="`setting-value-${draft.setting.value.id}`"
          class="min-w-48"
        />
        <SettingOutcome :outcome="draft.outcome.value" :data-testid="`setting-outcome-${draft.setting.value.id}`" />
      </UiStack>
    </UiTableCell>
    <UiTableCell muted>{{ draft.setting.value.description ?? EMPTY_VALUE }}</UiTableCell>
  </UiTableRow>
</template>
