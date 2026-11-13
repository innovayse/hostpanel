<template>
  <button
    type="button"
    role="switch"
    :aria-checked="modelValue"
    :aria-label="label"
    :disabled="disabled || loading"
    :data-testid="testid"
    :class="trackClasses"
    @click="emit('update:modelValue', !modelValue)"
  >
    <Loader2 v-if="loading" :size="14" :class="['absolute top-0.5 left-0.5 w-5 h-5 animate-spin', modelValue ? 'text-on-primary' : 'text-gray-700 dark:text-white']" />
    <span
      v-else
      :class="['absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white transition-transform duration-300 shadow-sm', modelValue ? 'translate-x-6 dark:bg-on-primary' : 'translate-x-0 dark:bg-gray-200']"
    />
  </button>
</template>

<script setup lang="ts">
/**
 * UiToggle: an on/off switch.
 *
 * It never flips itself: a click emits the value the reader asked for and the parent decides
 * whether to accept it, so a refused request leaves the switch where it was. While `loading` it
 * shows a spinner in place of the knob and ignores clicks.
 */
import { computed } from 'vue'
import { Loader2 } from 'lucide-vue-next'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Props of UiToggle. */
interface Props {
  /** Whether the switch is on (v-model). */
  modelValue: boolean
  /** Accessible name of the switch (what it turns on or off). */
  label: string
  /** Shows a spinner and ignores clicks. */
  loading?: boolean
  /** Disables the switch. */
  disabled?: boolean
  /** `data-testid` of the switch. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  loading: false,
  disabled: false,
  testid: undefined,
})

/** Events the toggle emits. */
const emit = defineEmits<{
  /** The reader asked for the opposite state. */
  'update:modelValue': [value: boolean]
}>()

/** Class list of the track: primary when on; a clearly bordered grey when off so it reads on a white and on a dark page. */
const trackClasses = computed(() => [
  'ui-toggle relative w-12 h-6 rounded-full border transition-all duration-300 flex-shrink-0 disabled:cursor-not-allowed disabled:opacity-70',
  FOCUS_RING,
  props.modelValue
    ? 'ui-toggle-on bg-primary-500 border-primary-600 dark:border-primary-400'
    : 'bg-gray-400 dark:bg-white/20 border-gray-500 hover:bg-gray-500 dark:hover:bg-white/30',
].join(' '))
</script>
