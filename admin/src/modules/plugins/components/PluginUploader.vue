<script setup lang="ts">
/**
 * File upload dropzone for installing a plugin ZIP.
 * Validates that the selected file has a .zip extension before emitting.
 */
import { ref } from 'vue'

/** Emits for PluginUploader. */
const emit = defineEmits<{
  /** Emitted with the selected ZIP file when the admin confirms the upload. */
  upload: [file: File]
}>()

/** The file the admin has selected (before confirming). */
const selectedFile = ref<File | null>(null)

/** Validation error shown when a non-ZIP file is selected. */
const validationError = ref<string | null>(null)

/**
 * Handles file input change — validates extension and stages the file.
 *
 * @param event - The native change event from the file input.
 */
function handleFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  validationError.value = null
  selectedFile.value = null

  if (!file) return

  if (!file.name.endsWith('.zip')) {
    validationError.value = 'Only .zip files are accepted.'
    return
  }

  selectedFile.value = file
}

/**
 * Emits the staged file and resets local state.
 */
function handleUpload(): void {
  if (!selectedFile.value) return
  emit('upload', selectedFile.value)
  selectedFile.value = null
}
</script>

<template>
  <div class="bg-white rounded-xl border border-dashed border-gray-300 p-6 text-center">
    <p class="text-sm text-gray-500 mb-3">Upload a plugin <span class="font-mono">.zip</span> file</p>

    <input
      id="plugin-zip-input"
      type="file"
      accept=".zip"
      class="hidden"
      @change="handleFileChange"
    />

    <label
      for="plugin-zip-input"
      class="cursor-pointer inline-block bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-medium px-4 py-2 rounded-lg transition"
    >
      Choose file
    </label>

    <div v-if="selectedFile" class="mt-3 text-sm text-gray-700">
      Selected: <span class="font-mono">{{ selectedFile.name }}</span>
      <button
        type="button"
        class="ml-3 bg-blue-700 hover:bg-blue-800 text-white text-xs font-semibold px-4 py-1.5 rounded-lg transition"
        @click="handleUpload"
      >
        Install Plugin
      </button>
    </div>

    <p v-if="validationError" class="mt-2 text-xs text-red-600">{{ validationError }}</p>
  </div>
</template>
