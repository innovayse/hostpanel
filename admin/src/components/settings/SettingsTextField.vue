<script setup lang="ts">
/**
 * One free-text settings field of a card: the input that fills its grid column, the refusal under it and the outcome
 * line of its save.
 *
 * The card builds the draft (`useSettingDraft`) and hands it over with the key it edits, so the page's save bar counts
 * and saves the edit. A row the API never seeded shows the warning in place of the input. A value that breaks its rule
 * is flagged under the field (`invalidMessage`); the card decides what breaks it. A `hint` is a quiet help line under the
 * field while it has no refusal.
 */
import { computed } from 'vue'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import type { SettingDraft } from '@/composables/useSettingDraft'
import { UiInputType } from '@/utils/ui/input'
import { UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for SettingsTextField. */
const props = withDefaults(defineProps<{
  /** The field's draft state. */
  draft: SettingDraft
  /** Settings key the field edits, named in the warning when the row is missing. */
  settingKey: string
  /** Environment variable the portal falls back to until the row exists. */
  envName: string
  /** Caption of the field (already translated). */
  label: string
  /** Placeholder of the field (already translated). */
  placeholder: string
  /** Longest text the field takes. */
  maxlength: number
  /** `data-testid` of the input; the refusal line is `<testid>-invalid`. */
  testid: string
  /** Native input type. */
  type?: UiInputType
  /** Sentence shown under the field while the value breaks its rule; undefined when the value is fine. */
  invalidMessage?: string
  /** Help line under the field (already translated); omitted when there is none. */
  hint?: string
}>(), {
  type: UiInputType.Text,
  invalidMessage: undefined,
  hint: undefined,
})

/** The draft's text, which the field edits in place; read through a computed so a replaced draft is followed. */
const text = computed(() => props.draft.value)

/** The draft's text as a model the input binds to. */
const model = computed<string>({
  /**
   * The text the input shows.
   *
   * @returns The draft's edit, else the stored value.
   */
  get: () => text.value.value,
  /**
   * Records an edit in the draft.
   *
   * @param next - The text the operator typed.
   */
  set: (next: string) => {
    text.value.value = next
  },
})
</script>

<template>
  <SettingNotSeeded v-if="!draft.setting.value" :setting-key="settingKey" :env-name="envName" />
  <UiStack v-else :gap="UiGap.Xs">
    <UiInput
      v-model="model"
      :type="type"
      :label="label"
      :placeholder="placeholder"
      :maxlength="maxlength"
      :invalid="invalidMessage !== undefined"
      :data-testid="testid"
    />
    <UiText v-if="invalidMessage !== undefined" :tone="UiTextTone.Danger" :size="UiTextSize.Sm" :data-testid="`${testid}-invalid`">
      {{ invalidMessage }}
    </UiText>
    <UiText v-else-if="hint !== undefined" :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
      {{ hint }}
    </UiText>
    <SettingOutcome :outcome="draft.outcome.value" />
  </UiStack>
</template>
