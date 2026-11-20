<script setup lang="ts">
/**
 * The number input of one numeric fraud setting: the range of the field, its unit, its placeholder and its test id come
 * from the table of numeric fields, so the screen binds a value and an event and nothing else.
 *
 * It is the control the fraud settings form puts in the slot of each numeric field of its schema; the caption, the help
 * line and the refusal sentence belong to the form's field, which hands over `id`, `invalid` and `describedby`.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import { FRAUD_NUMBER_FIELDS } from '@/lib/fraud'
import type { FraudNumberField } from '@/lib/fraud'
import type { FraudSettingsField } from '@/types/fraud'

const { t } = useI18n()

/** Props for FraudNumberInput. */
const props = defineProps<{
  /** The setting the input edits. */
  field: FraudSettingsField
  /** The number the setting holds in the draft. */
  modelValue: number
  /** Id of the native input, so the caption points at it. */
  id?: string
  /** Marks the value as refused (red border, `aria-invalid`). */
  invalid?: boolean
  /** Id of the element that describes the value (the help line or the refusal). */
  describedby?: string
  /** Turns the input off (fraud protection is off, so the value is kept but not applied). */
  disabled?: boolean
}>()

const emit = defineEmits<{
  /** Emitted with the number the input reports; null for a cleared input. */
  'update:modelValue': [value: number | null]
}>()

/** The range and the wording of the setting. */
const spec = computed<FraudNumberField>(() => FRAUD_NUMBER_FIELDS[props.field])
</script>

<template>
  <UiNumberInput
    :id="id"
    :model-value="modelValue"
    :min="spec.min"
    :max="spec.max"
    :invalid="invalid"
    :describedby="describedby"
    :placeholder="String(spec.min)"
    :suffix="t(spec.unitKey)"
    :disabled="disabled"
    :data-testid="`fraud-settings-${field}`"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>
