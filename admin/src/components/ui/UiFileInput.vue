<script setup lang="ts">
/**
 * File picker.
 *
 * Wraps the native `<input type="file">`. The chosen files are emitted as a `File[]` through
 * `update:modelValue` (so `v-model` works and an empty array clears the picker), and the native
 * `change` listener still falls through for screens that read the event. `accept`, `multiple`,
 * `id`, classes and `data-testid` land on the input.
 */
import { ref, watch } from 'vue'
import { focusRingClasses } from '@/utils/ui/input'

/** Props for UiFileInput. */
const props = withDefaults(defineProps<{
  /** Chosen files (two-way bound with `v-model`); set to `[]` to clear the picker. */
  modelValue?: File[]
}>(), {
  modelValue: undefined,
})

const emit = defineEmits<{
  /** Emitted with the chosen files, empty when the selection was cleared. */
  'update:modelValue': [files: File[]]
}>()

/** The native input, needed to clear its value. */
const input = ref<HTMLInputElement | null>(null)

/** Classes of the picker: the kit's focus ring, a muted label and button while it is disabled. */
const classes = `text-sm text-text-secondary rounded-control file:mr-3 file:rounded-[9px] file:border file:border-border file:bg-surface-field file:px-3.5 file:py-1.5 file:text-[0.8125rem] file:font-medium file:text-text-secondary file:cursor-pointer hover:file:bg-surface-hover hover:file:text-text-primary disabled:text-text-muted disabled:cursor-not-allowed file:disabled:text-text-muted file:disabled:cursor-not-allowed ${focusRingClasses}`

/**
 * Re-emits the native selection as a file array.
 *
 * @param event - Native change event.
 */
const onChange = (event: Event): void => {
  const files = (event.target as HTMLInputElement).files
  emit('update:modelValue', files === null ? [] : Array.from(files))
}

// A parent resetting the model to [] must also clear the browser's own selection, otherwise the
// same file cannot be picked twice in a row (no change event fires).
watch(() => props.modelValue, (files) => {
  if (files !== undefined && files.length === 0 && input.value !== null) {
    input.value.value = ''
  }
})
</script>

<template>
  <input
    ref="input"
    type="file"
    :class="classes"
    @change="onChange"
  >
</template>
