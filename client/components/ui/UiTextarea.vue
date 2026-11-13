<template>
  <div class="w-full">
    <label v-if="label" :for="textareaId" class="block text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-600 dark:text-primary-400">*</span>
    </label>

    <textarea
      :id="textareaId"
      :value="modelValue"
      :placeholder="placeholder"
      :required="required"
      :disabled="disabled"
      :rows="rows"
      :maxlength="maxLength"
      :aria-invalid="error ? 'true' : undefined"
      :data-testid="testid"
      :class="textareaClasses"
      @input="handleInput"
      @blur="$emit('blur')"
      @focus="$emit('focus')"
    />

    <div class="mt-2 flex items-center justify-between">
      <div>
        <p v-if="error" class="text-sm text-red-600 dark:text-red-400">
          {{ error }}
        </p>
        <p v-else-if="hint" class="text-sm text-gray-500 dark:text-gray-400">
          {{ hint }}
        </p>
      </div>

      <p v-if="maxLength" class="text-sm text-gray-500 dark:text-gray-400">
        {{ characterCount }}/{{ maxLength }}
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiTextarea: a labelled multi-line field with error, hint and optional character counter.
 *
 * Shares its frame with UiInput through `fieldStateClasses`, so the two never drift apart.
 */
import { computed, useId } from 'vue'
import { fieldStateClasses } from '~/utils/ui/tones'

/** Props of UiTextarea. */
interface Props {
  /** DOM id; generated when omitted. */
  id?: string
  /** Label text above the field. */
  label?: string
  /** Current value (v-model). */
  modelValue?: string
  /** Hint shown inside the empty field; every text field must set one. */
  placeholder?: string
  /** Marks the field required and adds the asterisk. */
  required?: boolean
  /** Disables the field. */
  disabled?: boolean
  /** Error line; also switches the field to its error look. */
  error?: string
  /** Neutral help line, hidden while an error shows. */
  hint?: string
  /** Number of visible rows. */
  rows?: number
  /** Maximum number of characters; also shows the counter. */
  maxLength?: number
  /** Lets the reader resize the field vertically. */
  resize?: boolean
  /** `data-testid` of the inner textarea element. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  label: undefined,
  modelValue: undefined,
  placeholder: undefined,
  required: false,
  disabled: false,
  error: undefined,
  hint: undefined,
  rows: 4,
  maxLength: undefined,
  resize: true,
  testid: undefined,
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** The user changed the value. */
  'update:modelValue': [value: string]
  /** The field lost focus. */
  blur: []
  /** The field gained focus. */
  focus: []
}>()

/** Generated id used when the caller passes none. */
const generatedId = useId()

/** The id bound to both the label and the textarea. */
const textareaId = computed(() => props.id ?? generatedId)

/**
 * Forwards the typed value.
 *
 * @param event The native input event.
 */
const handleInput = (event: Event): void => {
  emit('update:modelValue', (event.target as HTMLTextAreaElement).value)
}

/** Current character count. */
const characterCount = computed(() => props.modelValue?.length ?? 0)

/** Class list of the `<textarea>`. */
const textareaClasses = computed(() => [
  fieldStateClasses({ error: Boolean(props.error), disabled: props.disabled }),
  'px-4 py-2.5 placeholder:text-gray-500 dark:placeholder:text-gray-400',
  props.resize ? 'resize-y' : 'resize-none',
].join(' '))
</script>
