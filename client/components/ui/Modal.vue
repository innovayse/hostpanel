<template>
  <!-- Backdrop overlay -->
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="modelValue"
        class="fixed inset-0 z-50 flex items-center justify-center p-4"
        @click.self="handleClose"
      >
        <!-- Backdrop -->
        <div class="absolute inset-0 bg-black/50 backdrop-blur-sm" />

        <!-- Modal container -->
        <div
          :class="modalClasses"
          class="relative bg-white rounded-xl shadow-2xl max-h-[90vh] overflow-y-auto"
          @click.stop
        >
          <!-- Header -->
          <div v-if="title || $slots.header" class="flex items-center justify-between p-6 border-b border-gray-200">
            <slot name="header">
              <h3 class="text-xl font-semibold text-gray-900">
                {{ title }}
              </h3>
            </slot>

            <button
              v-if="closable"
              type="button"
              class="text-gray-400 hover:text-gray-600 transition-colors"
              @click="handleClose"
            >
              <X :size="20" :stroke-width="2" />
            </button>
          </div>

          <!-- Body -->
          <div class="p-6">
            <slot />
          </div>

          <!-- Footer -->
          <div v-if="$slots.footer" class="flex items-center justify-end gap-3 p-6 border-t border-gray-200 bg-gray-50">
            <slot name="footer" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
/**
 * Modal component with backdrop, header, body, and footer slots
 * Teleports to body for proper z-index stacking
 */

import { X } from 'lucide-vue-next'

interface Props {
  /** Whether modal is open */
  modelValue: boolean
  /** Modal title */
  title?: string
  /** Size variant */
  size?: 'sm' | 'md' | 'lg' | 'xl' | 'full'
  /** Show close button */
  closable?: boolean
  /** Close on backdrop click */
  closeOnBackdrop?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  size: 'md',
  closable: true,
  closeOnBackdrop: true
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  close: []
}>()

/** Handle close action */
const handleClose = () => {
  if (props.closeOnBackdrop || props.closable) {
    emit('update:modelValue', false)
    emit('close')
  }
}

/** Computed classes for modal size */
const modalClasses = computed(() => {
  const sizes = {
    sm: 'max-w-sm w-full',
    md: 'max-w-md w-full',
    lg: 'max-w-lg w-full',
    xl: 'max-w-2xl w-full',
    full: 'max-w-6xl w-full'
  }

  return sizes[props.size]
})

// Lock body scroll when modal is open
watch(() => props.modelValue, (isOpen) => {
  if (isOpen) {
    document.body.style.overflow = 'hidden'
  } else {
    document.body.style.overflow = ''
  }
})

// Cleanup on unmount
onUnmounted(() => {
  document.body.style.overflow = ''
})
</script>

<style scoped>
/* Modal transition animations */
.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.2s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-active > div:last-child,
.modal-leave-active > div:last-child {
  transition: transform 0.2s ease;
}

.modal-enter-from > div:last-child,
.modal-leave-to > div:last-child {
  transform: scale(0.95);
}
</style>
