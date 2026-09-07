<script setup lang="ts">
/**
 * Reusable alert banner component with variant support.
 *
 * Renders an icon + message in a rounded card styled to the site's dark theme.
 */

const props = withDefaults(defineProps<{
  /** Visual variant controlling colors. */
  variant?: 'warning' | 'error' | 'info' | 'success'
}>(), {
  variant: 'warning',
})

/** Maps variant to Tailwind classes for the container. */
const variantClasses: Record<string, string> = {
  warning: 'bg-status-yellow/10 border-status-yellow/20 text-status-yellow',
  error: 'bg-status-red/10 border-status-red/20 text-status-red',
  info: 'bg-primary-500/10 border-primary-500/20 text-primary-400',
  success: 'bg-status-green/10 border-status-green/20 text-status-green',
}

/** Maps variant to icon SVG path data. */
const iconPaths: Record<string, string> = {
  warning: 'M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z',
  error: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z',
  info: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z',
  success: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z',
}
</script>

<template>
  <div
    class="border rounded-2xl px-5 py-3 flex items-center gap-3"
    :class="variantClasses[props.variant]"
  >
    <svg class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
      <path :d="iconPaths[props.variant]" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
    <span class="text-[0.82rem]">
      <slot />
    </span>
  </div>
</template>
