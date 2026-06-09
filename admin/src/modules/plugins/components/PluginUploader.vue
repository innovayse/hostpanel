<script setup lang="ts">
/**
 * File upload dropzone for installing a plugin ZIP.
 * Validates that the selected file has a .zip extension before emitting.
 */
import { ref } from 'vue'

const emit = defineEmits<{
  upload: [file: File]
}>()

const selectedFile = ref<File | null>(null)
const validationError = ref<string | null>(null)
const isDragging = ref(false)

function handleFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  processFile(file)
}

function handleDrop(event: DragEvent): void {
  isDragging.value = false
  const file = event.dataTransfer?.files?.[0] ?? null
  processFile(file)
}

function processFile(file: File | null): void {
  validationError.value = null
  selectedFile.value = null
  if (!file) return
  if (!file.name.endsWith('.zip')) {
    validationError.value = 'Only .zip files are accepted.'
    return
  }
  selectedFile.value = file
}

function handleUpload(): void {
  if (!selectedFile.value) return
  emit('upload', selectedFile.value)
  selectedFile.value = null
}

function clearFile(): void {
  selectedFile.value = null
  validationError.value = null
}
</script>

<template>
  <div
    class="relative rounded-2xl border-2 border-dashed transition-all duration-200 p-8"
    :class="isDragging
      ? 'border-primary-500 bg-primary-500/8'
      : 'border-border hover:border-primary-500/40 bg-surface-card'"
    @dragover.prevent="isDragging = true"
    @dragleave.prevent="isDragging = false"
    @drop.prevent="handleDrop"
  >
    <input
      id="plugin-zip-input"
      type="file"
      accept=".zip"
      class="hidden"
      @change="handleFileChange"
    />

    <!-- No file selected -->
    <div v-if="!selectedFile" class="flex flex-col items-center gap-3 text-center">
      <!-- Icon -->
      <div class="w-12 h-12 rounded-2xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center">
        <svg class="w-6 h-6 text-primary-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
          <polyline points="17 8 12 3 7 8"/>
          <line x1="12" y1="3" x2="12" y2="15"/>
        </svg>
      </div>

      <div>
        <p class="text-[0.9rem] font-medium text-text-primary">
          Drop your plugin <span class="font-mono text-primary-400">.zip</span> here
        </p>
        <p class="text-[0.78rem] text-text-muted mt-0.5">or click to browse files</p>
      </div>

      <label
        for="plugin-zip-input"
        class="cursor-pointer inline-flex items-center gap-2 px-4 py-2 rounded-xl text-[0.82rem] font-medium bg-primary-500/10 hover:bg-primary-500/20 text-primary-400 border border-primary-500/20 transition-all duration-150"
      >
        Choose file
      </label>

      <p v-if="validationError" class="text-[0.78rem] text-status-red">{{ validationError }}</p>
    </div>

    <!-- File selected -->
    <div v-else class="flex items-center gap-4">
      <!-- File icon -->
      <div class="w-10 h-10 rounded-xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center shrink-0">
        <svg class="w-5 h-5 text-primary-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"/>
          <polyline points="13 2 13 9 20 9"/>
        </svg>
      </div>

      <div class="flex-1 min-w-0">
        <p class="text-[0.88rem] font-medium text-text-primary truncate font-mono">{{ selectedFile.name }}</p>
        <p class="text-[0.75rem] text-text-muted mt-0.5">{{ (selectedFile.size / 1024).toFixed(1) }} KB · Ready to install</p>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <button
          type="button"
          class="px-3 py-1.5 rounded-xl text-[0.78rem] font-medium text-text-muted hover:text-text-secondary hover:bg-white/[0.04] transition-all duration-150"
          @click="clearFile"
        >
          Cancel
        </button>
        <button
          type="button"
          class="px-4 py-1.5 rounded-xl text-[0.82rem] font-semibold gradient-brand text-white transition-all duration-150 hover:opacity-90"
          @click="handleUpload"
        >
          Install Plugin
        </button>
      </div>
    </div>
  </div>
</template>
