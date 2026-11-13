<script setup lang="ts">
/**
 * Multi-line text field.
 *
 * Same themed field as `UiInput`, with vertical resizing. `rows`, `maxlength`, `class`,
 * `data-testid` and listeners fall through to the native `<textarea>`.
 */
import { computed } from 'vue'
import { fieldBaseClasses, fieldInvalidClasses, fieldWidthClasses } from '@/utils/ui/input'
import { textareaSizeClasses } from '@/utils/ui/size'
import type { UiSize } from '@/utils/ui/size'

/** Props for UiTextarea. */
const props = withDefaults(defineProps<{
  /** Current value (two-way bound with `v-model`). */
  modelValue?: string | null
  /**
   * Modifiers of `v-model` (`.number`, `.trim`). Declared so they are not forwarded to the native
   * element as an attribute; Vue applies them itself when the component emits the update.
   */
  modelModifiers?: Record<string, boolean>
  /** Padding and type size. */
  size?: UiSize
  /** Hint shown while the field is empty. */
  placeholder?: string
  /** Marks the value as refused by the server (red border, `aria-invalid`). */
  invalid?: boolean
}>(), {
  modelValue: '',
  modelModifiers: undefined,
  size: 'md',
  placeholder: undefined,
  invalid: false,
})

const emit = defineEmits<{
  /** Emitted with the new text on every keystroke. */
  'update:modelValue': [value: string]
}>()

/** Field classes. */
const classes = computed<string[]>(() => [
  fieldWidthClasses,
  fieldBaseClasses,
  textareaSizeClasses[props.size],
  'resize-y',
  props.invalid ? fieldInvalidClasses : '',
])

/**
 * Forwards the native input event as a model update.
 *
 * @param event - Native input event.
 */
const onInput = (event: Event): void => {
  emit('update:modelValue', (event.target as HTMLTextAreaElement).value)
}
</script>

<template>
  <textarea
    :value="modelValue ?? ''"
    :placeholder="placeholder"
    :aria-invalid="invalid || undefined"
    :class="classes"
    @input="onInput"
  />
</template>
