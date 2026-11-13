<template>
  <div class="w-full">
    <label v-if="label" :for="controlId" class="block text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-600 dark:text-primary-400">*</span>
    </label>

    <slot v-bind="slotProps" />

    <p
      v-if="message"
      :id="messageId"
      role="alert"
      data-testid="ui-field-error"
      class="mt-2 text-sm text-red-600 dark:text-red-400"
    >
      {{ message }}
    </p>
    <p
      v-else-if="hint"
      :id="messageId"
      data-testid="ui-field-hint"
      class="mt-2 text-sm text-gray-500 dark:text-gray-400"
    >
      {{ hint }}
    </p>
  </div>
</template>

<script setup lang="ts">
/**
 * UiField: a label, a control and the message under it.
 *
 * It frames any control (a custom picker, a group of checkboxes, a bare file input) so every
 * form lays out the same. `error` takes what a store's `fieldErrors[name]` holds (one sentence or
 * the list the API returned; the first is shown) and wins over `hint`. The default slot receives
 * `{ id, invalid, describedBy }` so the control can bind them for the label and for screen readers.
 */
import { computed, useId } from 'vue'

/** Props of UiField. */
interface Props {
  /** DOM id of the control the label points at; generated when omitted. */
  id?: string
  /** Label text above the control. */
  label?: string
  /** Adds the required asterisk to the label. */
  required?: boolean
  /** Error sentence, or the list of sentences for this field; the first is shown. */
  error?: string | readonly string[]
  /** Neutral help line, hidden while an error shows. */
  hint?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  id: undefined,
  label: undefined,
  required: false,
  error: undefined,
  hint: undefined,
})

/** Generated id used when the caller passes none. */
const generatedId = useId()

/** The id of the control, bound to the label's `for`. */
const controlId = computed(() => props.id ?? generatedId)

/** The id of the message element, for `aria-describedby`. */
const messageId = computed(() => `${controlId.value}-message`)

/** The error sentence to show: the first of a list, or the string itself. */
const message = computed(() => {
  if (Array.isArray(props.error)) {
    return props.error[0]
  }
  return props.error as string | undefined
})

/** What the slot receives. */
const slotProps = computed(() => ({
  id: controlId.value,
  invalid: Boolean(message.value),
  describedBy: message.value || props.hint ? messageId.value : undefined,
}))
</script>
