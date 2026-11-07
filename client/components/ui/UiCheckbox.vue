<template>
  <div class="flex items-start gap-3" :class="disabled ? 'opacity-60' : ''">
    <input
      :id="inputId"
      type="checkbox"
      :checked="modelValue"
      :disabled="disabled"
      :required="required"
      :aria-describedby="descriptionId"
      :data-testid="testid ? `${testid}-input` : undefined"
      class="sr-only"
      @change="handleChange"
    >
    <button
      type="button"
      role="checkbox"
      :aria-checked="modelValue"
      :aria-labelledby="label || $slots.default ? labelId : undefined"
      :aria-describedby="descriptionId"
      :disabled="disabled"
      :data-testid="testid"
      class="ui-check flex-shrink-0 mt-0.5 w-5 h-5 rounded border-2 transition-all duration-150 flex items-center justify-center"
      :class="[
        FOCUS_RING,
        modelValue
          ? 'ui-check-on bg-primary-500 border-primary-500 dark:bg-primary-500 dark:border-primary-400'
          : 'bg-white dark:bg-white/5 border-gray-400 dark:border-gray-500 hover:border-primary-500 dark:hover:border-primary-400',
        disabled ? 'cursor-not-allowed' : 'cursor-pointer',
      ]"
      @click="toggle"
    >
      <svg v-if="modelValue" viewBox="0 0 12 10" fill="none" class="w-3 h-3 text-on-primary">
        <path d="M1 5l3.5 3.5L11 1" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
      </svg>
    </button>
    <div v-if="label || $slots.default" class="flex-1 text-sm leading-relaxed">
      <label
        :id="labelId"
        :for="inputId"
        class="font-medium text-gray-700 dark:text-gray-300 transition-colors"
        :class="disabled ? 'cursor-not-allowed' : 'cursor-pointer hover:text-gray-900 dark:hover:text-white'"
        @click.prevent="toggle"
      >
        {{ label }}
        <slot />
      </label>
      <p v-if="description" :id="descriptionId" class="text-gray-500 dark:text-gray-400 text-xs mt-1">{{ description }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiCheckbox - a themed checkbox with an optional label and description.
 *
 * A visually hidden native checkbox keeps form semantics; the visible box is a `role="checkbox"`
 * button so the look is fully ours. Both the box and the label toggle the value.
 */
import { computed, useId } from 'vue'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Props of UiCheckbox. */
interface Props {
  /** DOM id of the native checkbox; generated when omitted. */
  id?: string
  /** Label text next to the box. */
  label?: string
  /** Muted helper text under the label. */
  description?: string
  /** Checked state (v-model). */
  modelValue?: boolean
  /** Disables the checkbox. */
  disabled?: boolean
  /** Marks the native checkbox required. */
  required?: boolean
  /** `data-testid` of the visible box. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  label: undefined,
  description: undefined,
  modelValue: false,
  disabled: false,
  required: false,
  testid: undefined,
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** The checked state changed. */
  'update:modelValue': [value: boolean]
}>()

/** Generated id used when the caller passes none. */
const generatedId = useId()

/** The id bound to the native checkbox and the label. */
const inputId = computed(() => props.id ?? generatedId)

/** Id of the label, so the visible box (a button) takes its accessible name from it. */
const labelId = computed(() => `${inputId.value}-label`)

/** Id of the description when there is one; the box and the native input are described by it, so the agreement text is read with the control. */
const descriptionId = computed((): string | undefined => (props.description ? `${inputId.value}-description` : undefined))

/** Flips the value unless disabled. */
const toggle = (): void => {
  if (!props.disabled) {
    emit('update:modelValue', !props.modelValue)
  }
}

/**
 * Forwards a change of the native checkbox (form autofill, assistive tech).
 *
 * @param event The native change event.
 */
const handleChange = (event: Event): void => {
  emit('update:modelValue', (event.target as HTMLInputElement).checked)
}
</script>
