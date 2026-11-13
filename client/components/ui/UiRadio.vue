<template>
  <div class="flex items-center gap-3" :class="disabled ? 'opacity-60' : ''">
    <!-- Hidden native input for form compatibility -->
    <input
      :id="inputId"
      type="radio"
      :name="name"
      :value="value"
      :checked="selected"
      :disabled="disabled"
      :data-testid="testid ? `${testid}-input` : undefined"
      class="sr-only"
      @change="emit('update:modelValue', value)"
    >

    <!-- Custom radio circle -->
    <button
      type="button"
      role="radio"
      :aria-checked="selected"
      :disabled="disabled"
      :data-testid="testid"
      class="ui-radio flex-shrink-0 w-5 h-5 rounded-full border-2 transition-all duration-150 flex items-center justify-center"
      :class="[
        FOCUS_RING,
        selected
          ? 'ui-radio-on border-primary-500 dark:border-primary-400 bg-transparent'
          : 'bg-transparent border-gray-400 dark:border-gray-500 hover:border-primary-500 dark:hover:border-primary-400',
        disabled ? 'cursor-not-allowed' : 'cursor-pointer',
      ]"
      @click="choose"
    >
      <span
        v-if="selected"
        class="ui-radio-dot w-2.5 h-2.5 rounded-full bg-primary-500 dark:bg-primary-400"
      />
    </button>

    <label
      v-if="label || $slots.default"
      :for="inputId"
      class="text-sm text-gray-700 dark:text-gray-300 leading-snug select-none"
      :class="disabled ? 'cursor-not-allowed' : 'cursor-pointer hover:text-gray-900 dark:hover:text-white'"
      @click.prevent="choose"
    >
      {{ label }}
      <slot />
      <span v-if="description" class="block text-gray-500 dark:text-gray-400 text-xs mt-0.5 font-normal">{{ description }}</span>
    </label>
  </div>
</template>

<script setup lang="ts">
/**
 * UiRadio: a themed radio option; group several with the same `name` and one v-model.
 *
 * A hidden native radio keeps form semantics; the visible circle is a `role="radio"` button.
 */
import { computed, useId } from 'vue'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Props of UiRadio. */
interface Props {
  /** DOM id of the native radio; generated when omitted. */
  id?: string
  /** Group name shared by the options. */
  name: string
  /** Value this option stands for. */
  value: string | number | boolean
  /** Label text next to the circle. */
  label?: string
  /** Muted helper text inside the label. */
  description?: string
  /** Currently selected value of the group (v-model). */
  modelValue?: string | number | boolean
  /** Disables the option. */
  disabled?: boolean
  /** `data-testid` of the visible circle. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  label: undefined,
  description: undefined,
  modelValue: undefined,
  disabled: false,
  testid: undefined,
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** This option was chosen. */
  'update:modelValue': [value: string | number | boolean]
}>()

/** Generated id used when the caller passes none. */
const generatedId = useId()

/** The id bound to the native radio and the label. */
const inputId = computed(() => props.id ?? generatedId)

/** Whether this option is the selected one. */
const selected = computed(() => props.modelValue === props.value)

/** Selects this option unless disabled. */
const choose = (): void => {
  if (!props.disabled) {
    emit('update:modelValue', props.value)
  }
}
</script>
