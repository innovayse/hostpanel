<script setup lang="ts">
/**
 * One form row: caption, the control, and the message under it.
 *
 * The default slot receives `{ id, invalid, describedby }` to bind on the control, so the caption
 * is wired to it (`for`/`id`) and the message is announced (`aria-describedby`). `error` is the
 * sentence the server returned for this field and always wins over `hint`; it is a `role="alert"`
 * line, so a screen reader announces it the moment it appears.
 *
 * While `loading` (or while a parent form provides `UI_FIELD_LOADING`) the control is replaced by a
 * skeleton of the control's height; the caption and the hint stay, so the form keeps its shape.
 */
import { computed, inject, ref, useId } from 'vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiText from '@/components/ui/UiText.vue'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'

/** Props for UiField. */
const props = withDefaults(defineProps<{
  /** Caption above the control. */
  label?: string
  /** Help line shown while there is no error. */
  hint?: string
  /** Server sentence for this field; turns the row red. */
  error?: string
  /** Marks the caption with an asterisk. */
  required?: boolean
  /** `data-testid` of the error message, for e2e specs. */
  errorTestid?: string
  /** Draws a skeleton instead of the control while the value is still loading. */
  loading?: boolean
}>(), {
  label: undefined,
  hint: undefined,
  error: undefined,
  required: false,
  errorTestid: undefined,
  loading: false,
})

/** Loading flag a parent form provides for all of its fields. */
const formLoading = inject(UI_FIELD_LOADING, ref(false))

/** True when this field shows a skeleton instead of its control. */
const isLoading = computed<boolean>(() => props.loading || formLoading.value)

/** Id of the control, generated per instance. */
const id = useId()

/** Id of the message element, referenced by the control's `aria-describedby`. */
const messageId = computed<string>(() => `${id}-message`)

/** Whether the field currently shows an error. */
const invalid = computed<boolean>(() => props.error !== undefined && props.error !== '')

/** Value for the control's `aria-describedby`; absent when there is no message. */
const describedby = computed<string | undefined>(() => {
  if (invalid.value || props.hint !== undefined) {
    return messageId.value
  }
  return undefined
})
</script>

<template>
  <div class="flex flex-col gap-1.5">
    <UiLabel v-if="label !== undefined" :for="id" variant="field">
      {{ label }}
      <span v-if="required" class="text-status-red" aria-hidden="true">*</span>
    </UiLabel>
    <UiSkeletonBar v-if="isLoading" class="h-10.5 w-full rounded-control" aria-hidden="true" data-testid="ui-field-skeleton" />
    <slot v-else :id="id" :invalid="invalid" :describedby="describedby" />
    <UiText
      v-if="invalid"
      :id="messageId"
      role="alert"
      :data-testid="errorTestid"
      tone="danger"
      size="sm"
    >
      {{ error }}
    </UiText>
    <UiText
      v-else-if="hint !== undefined"
      :id="messageId"
      tone="secondary"
      size="xs"
    >
      {{ hint }}
    </UiText>
  </div>
</template>
