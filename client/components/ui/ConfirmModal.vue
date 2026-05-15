<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-opacity duration-150 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        @click.self="emit('cancel')"
      >
        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          enter-from-class="opacity-0 scale-95 translate-y-2"
          enter-to-class="opacity-100 scale-100 translate-y-0"
          leave-active-class="transition-all duration-150 ease-in"
          leave-from-class="opacity-100 scale-100 translate-y-0"
          leave-to-class="opacity-0 scale-95 translate-y-2"
        >
          <div
            v-if="open"
            class="w-full max-w-sm rounded-2xl bg-white dark:bg-[#13131a] border border-gray-200 dark:border-white/10 shadow-2xl"
          >
            <!-- Icon + content -->
            <div class="p-6 text-center">
              <div class="w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-4"
                :class="variant === 'danger'
                  ? 'bg-red-500/10 border border-red-500/20'
                  : 'bg-yellow-500/10 border border-yellow-500/20'"
              >
                <component
                  :is="variant === 'danger' ? Trash2 : AlertTriangle"
                  :size="22"
                  :stroke-width="2"
                  :class="variant === 'danger' ? 'text-red-400' : 'text-yellow-400'"
                />
              </div>
              <h3 class="text-base font-bold text-gray-900 dark:text-white mb-2">{{ title }}</h3>
              <p v-if="description" class="text-sm text-gray-500 dark:text-gray-400 leading-relaxed">{{ description }}</p>
            </div>

            <!-- Actions -->
            <div class="flex gap-3 px-6 pb-6">
              <UiButton
                class="flex-1"
                variant="outline"
                @click="emit('cancel')"
              >
                {{ cancelLabel || $t('common.cancel') }}
              </UiButton>
              <UiButton
                class="flex-1"
                :variant="variant === 'danger' ? 'danger' : 'primary'"
                :loading="loading"
                @click="emit('confirm')"
              >
                {{ confirmLabel || $t('common.confirm') }}
              </UiButton>
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { Trash2, AlertTriangle } from 'lucide-vue-next'

defineProps<{
  open: boolean
  title: string
  description?: string
  confirmLabel?: string
  cancelLabel?: string
  variant?: 'danger' | 'warning'
  loading?: boolean
}>()

const emit = defineEmits<{
  confirm: []
  cancel: []
}>()
</script>