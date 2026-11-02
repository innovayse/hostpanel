<script setup lang="ts">
/**
 * One on/off settings row: a switch with its label and hint, and its own Save button.
 *
 * The row is stored as the string `true` or `false`. A row that was never seeded is replaced by
 * the usual warning, because the panel cannot create settings. Used by the home sections card and
 * by the hosting details card, so a boolean setting saves the same way everywhere.
 */
import { toRef } from 'vue'
import { useSettingDraft } from '@/composables/useSettingDraft'
import { useSettingSwitch } from '@/composables/useSettingSwitch'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingSaveButton from '@/components/settings/SettingSaveButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { SettingEnv, SettingToggle } from '@/types/setting'
import type { Setting, SettingKey, SettingSave } from '@/types/setting'

/** Props for SettingToggleField. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** The settings key the switch edits. */
  settingKey: SettingKey
  /** Caption of the switch (already translated). */
  label: string
  /** Hint under the caption (already translated); omitted when there is none. */
  hint?: string
  /** Whether the switch shows as on while the stored value is empty. */
  defaultOn: boolean
  /** Test id of the switch; the Save button adds `-save`. */
  testid: string
  /** Persists one row and answers how it went; each control shows its own outcome. */
  save: SettingSave
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Draft of the row; an empty row reads as the section's default. */
const draft = useSettingDraft(settings, props.settingKey, props.defaultOn ? SettingToggle.On : SettingToggle.Off)

/** The switch's view of the row: only the literal `true` is on. */
const enabled = useSettingSwitch(draft)
</script>

<template>
  <SettingNotSeeded v-if="!draft.setting.value" :setting-key="settingKey" :env-name="SettingEnv[settingKey]" />
  <UiStack v-else direction="row" align="center" wrap>
    <UiToggle v-model="enabled" :data-testid="testid" />
    <UiStack gap="xs" class="min-w-56 flex-1">
      <UiText tone="primary" size="md">
        {{ label }}
      </UiText>
      <UiText v-if="hint" tone="muted" size="sm">
        {{ hint }}
      </UiText>
    </UiStack>
    <SettingSaveButton
      :dirty="draft.dirty.value"
      :saving="draft.saving.value"
      :data-testid="`${testid}-save`"
      @click="draft.submit(save)"
    />
    <SettingOutcome :outcome="draft.outcome.value" />
  </UiStack>
</template>
