<template>
  <span class="inline-flex items-center gap-1 py-2" role="status" :aria-label="label" data-testid="ui-typing-dots">
    <span
      v-for="delay in DELAYS"
      :key="delay"
      :class="['size-1.5 animate-bounce rounded-full bg-gray-400 motion-reduce:animate-none dark:bg-gray-500', delay]"
    />
  </span>
</template>

<script setup lang="ts">
/**
 * UiTypingDots - three bouncing dots that say the other party is writing, before its first words arrive.
 *
 * A status region named by `label`, so a screen reader announces it once; the dots stand still for readers who ask for
 * reduced motion.
 */

/** Props of UiTypingDots. */
interface Props {
  /** The announced name, for example "Writing". */
  label: string
}

/** The props, read in the template. */
defineProps<Props>()

/** The animation delay of each dot, as complete classes so Tailwind emits them. */
const DELAYS: readonly string[] = ['[animation-delay:-0.3s]', '[animation-delay:-0.15s]', '[animation-delay:0s]']
</script>
