<template>
  <form novalidate @submit.prevent="$emit('submit', $event)">
    <!-- Error banner -->
    <Transition name="ui-form-fade">
      <div
        v-if="error"
        class="mb-5 p-4 rounded-xl border border-red-500/30 bg-red-500/10 flex items-start gap-3"
      >
        <AlertCircle :size="16" :stroke-width="2" class="text-red-400 flex-shrink-0 mt-0.5" />
        <p class="text-red-400 text-sm">{{ error }}</p>
      </div>
    </Transition>

    <!-- Success banner -->
    <Transition name="ui-form-fade">
      <div
        v-if="success"
        class="mb-5 p-4 rounded-xl border border-green-500/30 bg-green-500/10 flex items-start gap-3"
      >
        <CheckCircle :size="16" :stroke-width="2" class="text-green-400 flex-shrink-0 mt-0.5" />
        <p class="text-green-400 text-sm">{{ success }}</p>
      </div>
    </Transition>

    <!-- Fields -->
    <div :class="spacingClass">
      <slot />
    </div>

    <!-- Actions row -->
    <div v-if="$slots.actions" class="flex gap-3 justify-end mt-6">
      <slot name="actions" />
    </div>
  </form>
</template>

<style scoped>
.ui-form-fade-enter-active,
.ui-form-fade-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.ui-form-fade-enter-from,
.ui-form-fade-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>

<script setup lang="ts">
/**
 * UiForm — standardised form wrapper.
 * Handles @submit.prevent, error/success banners, field spacing, and an actions slot.
 */
import { AlertCircle, CheckCircle } from 'lucide-vue-next'

interface Props {
  /** Red error banner shown above the fields */
  error?: string
  /** Green success banner shown above the fields */
  success?: string
  /**
   * Vertical spacing between top-level field elements.
   * 'none' — no spacing wrapper (use when fields are in a custom grid).
   */
  spacing?: 'none' | 'sm' | 'md' | 'lg'
}

const props = withDefaults(defineProps<Props>(), {
  spacing: 'md'
})

defineEmits<{
  /** Fires on form submit (preventDefault already called) */
  submit: [event: SubmitEvent]
}>()

const spacingClass = computed(() => ({
  none: '',
  sm:   'space-y-3',
  md:   'space-y-4',
  lg:   'space-y-5'
}[props.spacing]))
</script>
