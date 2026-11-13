<script setup lang="ts">
/**
 * A limit that may be unlimited: a number field and an "Unlimited" switch.
 *
 * The API spells "unlimited" its own way (`null` for a server's account cap, `0` for a disk
 * quota), so the owner names it in `unlimitedValue` and the field emits exactly that when the
 * switch is on. Switching off puts back the number the reader had before (or the first step when
 * there was none), so a misclick does not lose the figure. With `sizeMb` the limit is megabytes and
 * a human size ("1.5 GB") is written under the field. `data-testid` and classes land on the root.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { formatSizeMb } from '@/lib/format'

const { t, locale } = useI18n()

/** Props for UiLimitField. */
const props = withDefaults(defineProps<{
  /** The limit, or the API's unlimited value. */
  modelValue: number | null
  /** What the API means by "unlimited": `null` or a number such as `0`. The field emits it when the switch is on. */
  unlimitedValue: number | null
  /** Smallest limit. */
  min?: number
  /** Largest limit. */
  max?: number
  /** Step of the number field; also the figure a switched-off field starts from when it has no earlier one. */
  step?: number
  /** Hint shown while the number field is empty. */
  placeholder?: string
  /** The limit is in megabytes: show the human size under the field. */
  sizeMb?: boolean
  /** Disables the number field and the switch. */
  disabled?: boolean
  /** Id of the number field, so a caption can point at it (`UiField` hands one out). */
  id?: string
  /** Marks the value as refused by the server. */
  invalid?: boolean
}>(), {
  min: undefined,
  max: undefined,
  step: 1,
  placeholder: '0',
  sizeMb: false,
  disabled: false,
  id: undefined,
  invalid: false,
})

const emit = defineEmits<{
  /** Emitted with the new limit, or with `unlimitedValue` when the switch goes on. */
  'update:modelValue': [value: number | null]
}>()

/** Whether the model holds the API's unlimited value. */
const unlimited = computed<boolean>(() => props.modelValue === props.unlimitedValue)

/** The last real limit, put back when the switch goes off; null until the field has held one. */
const remembered = ref<number | null>(props.modelValue === props.unlimitedValue ? null : props.modelValue)

/** The human size under the field, empty unless `sizeMb` is set and a limit is on. */
const sizeText = computed<string>(() =>
  props.sizeMb && !unlimited.value && props.modelValue !== null ? formatSizeMb(props.modelValue, locale.value) : '',
)

/**
 * Switches between unlimited and the remembered limit.
 *
 * @param on - True to make the limit unlimited.
 */
const setUnlimited = (on: boolean): void => {
  emit('update:modelValue', on ? props.unlimitedValue : (remembered.value ?? props.step))
}

/**
 * Passes a typed limit up.
 *
 * @param value - The number the field holds.
 */
const setLimit = (value: number | null): void => {
  emit('update:modelValue', value)
}

watch(() => props.modelValue, (value) => {
  if (value !== props.unlimitedValue) {
    remembered.value = value
  }
})
</script>

<template>
  <div class="flex flex-col gap-1.5">
    <div class="flex items-center gap-3">
      <UiNumberInput
        :id="props.id"
        class="min-w-0 flex-1"
        :model-value="unlimited ? '' : props.modelValue"
        :min="props.min"
        :max="props.max"
        :step="props.step"
        :placeholder="unlimited ? t('ui.uiLimitField.unlimited') : props.placeholder"
        :disabled="props.disabled || unlimited"
        :invalid="props.invalid"
        data-testid="ui-limit-number"
        @update:model-value="setLimit"
      />
      <label class="flex shrink-0 items-center gap-2 text-sm text-text-secondary">
        <UiToggle
          :model-value="unlimited"
          :disabled="props.disabled"
          data-testid="ui-limit-toggle"
          @update:model-value="setUnlimited"
        />
        {{ t('ui.uiLimitField.unlimited') }}
      </label>
    </div>
    <UiText v-if="sizeText" tone="muted" size="xs" data-testid="ui-limit-size">{{ sizeText }}</UiText>
  </div>
</template>
