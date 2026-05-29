<script setup lang="ts">
/**
 * Reusable confirmation modal with customizable title, message, and action button.
 * Uses Teleport to render at the body level with a backdrop overlay.
 */

withDefaults(defineProps<{
  /** Modal title displayed in the header. */
  title: string
  /** Descriptive message shown to the user. */
  message: string
  /** Label for the confirm action button. */
  confirmLabel?: string
  /** Label shown while the action is processing. */
  loadingLabel?: string
  /** Whether the action is currently processing. */
  loading?: boolean
  /** Visual variant for the confirm button. */
  variant?: 'danger' | 'primary' | 'warning'
}>(), {
  confirmLabel: 'Confirm',
  loadingLabel: 'Processing...',
  loading: false,
  variant: 'primary',
})

const emit = defineEmits<{
  /** Emitted when the user confirms the action. */
  confirm: []
  /** Emitted when the user cancels or closes the modal. */
  close: []
}>()

/** Maps variant to Tailwind button classes. */
const variantClasses: Record<string, string> = {
  danger: 'bg-red-600 hover:bg-red-500',
  primary: 'bg-blue-600 hover:bg-blue-500',
  warning: 'bg-yellow-600 hover:bg-yellow-500',
}
</script>

<template>
  <Teleport to="body">
    <div
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      @click.self="emit('close')"
    >
      <div class="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-sm p-6 space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="text-white font-semibold text-lg">{{ title }}</h2>
          <button class="text-zinc-400 hover:text-white transition" @click="emit('close')">&#10005;</button>
        </div>
        <p class="text-zinc-400 text-sm">{{ message }}</p>
        <div class="flex justify-end gap-2">
          <button
            class="px-4 py-2 bg-zinc-700 hover:bg-zinc-600 text-white text-sm rounded-lg transition"
            @click="emit('close')"
          >Cancel</button>
          <button
            :disabled="loading"
            class="px-4 py-2 text-white text-sm rounded-lg transition disabled:opacity-50"
            :class="variantClasses[variant]"
            @click="emit('confirm')"
          >{{ loading ? loadingLabel : confirmLabel }}</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
