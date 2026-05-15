<script setup lang="ts">
/**
 * Small modal for admin-initiated password change.
 * Single password field + Save/Cancel.
 */
import { ref } from 'vue'

defineProps<{
  /** Whether a save is in flight. */
  saving?: boolean
}>()

const emit = defineEmits<{
  /** Emitted with the new password when saved. */
  save: [password: string]
  /** Emitted when the modal is closed. */
  close: []
}>()

/** New password value. */
const password = ref('')

/** Error if password is empty. */
const fieldError = ref('')

/**
 * Validates and emits the save event.
 */
function handleSave(): void {
  if (!password.value.trim()) {
    fieldError.value = 'Password is required'
    return
  }
  fieldError.value = ''
  emit('save', password.value)
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center">
    <!-- Backdrop -->
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')" />

    <!-- Modal -->
    <div class="relative bg-surface-card border border-border rounded-2xl shadow-2xl w-full max-w-sm p-6">
      <h2 class="font-display font-bold text-[1.1rem] text-text-primary mb-4">Change Password</h2>

      <div>
        <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-1.5">New Password</label>
        <input
          v-model="password"
          type="password"
          placeholder="Enter new password"
          class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
          @keydown.enter="handleSave"
        />
        <p v-if="fieldError" class="text-[0.72rem] text-status-red mt-1">{{ fieldError }}</p>
      </div>

      <!-- Footer -->
      <div class="flex items-center justify-end gap-2 mt-5">
        <button
          class="px-4 py-2 text-[0.82rem] font-medium text-text-secondary bg-white/[0.05] border border-border rounded-[9px] hover:text-text-primary hover:border-border/80 transition-colors"
          @click="emit('close')"
        >
          Cancel
        </button>
        <button
          :disabled="saving"
          class="gradient-brand px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90 disabled:opacity-50"
          @click="handleSave"
        >
          {{ saving ? 'Saving…' : 'Save' }}
        </button>
      </div>
    </div>
  </div>
</template>
