<script setup lang="ts">
/**
 * Hover/focus hint around any trigger.
 *
 * The default slot is the trigger; `text` is the hint, shown on pointer hover and on keyboard
 * focus inside the trigger, and linked to it with `aria-describedby`.
 */
import { useId } from 'vue'
import type { UiTooltipPlacement } from '@/utils/ui/tooltip'

/** Props for UiTooltip. */
withDefaults(defineProps<{
  /** The hint (already translated). */
  text: string
  /** Where the hint appears. */
  placement?: UiTooltipPlacement
}>(), {
  placement: 'top',
})

/** Id of the hint element. */
const id = useId()

/** Position classes per placement. */
const placementClasses: Record<UiTooltipPlacement, string> = {
  top: 'bottom-full mb-1.5',
  bottom: 'top-full mt-1.5',
}
</script>

<template>
  <span class="relative inline-flex group" :aria-describedby="id">
    <slot />
    <span
      :id="id"
      role="tooltip"
      class="pointer-events-none absolute left-1/2 -translate-x-1/2 z-40 whitespace-nowrap rounded-md bg-surface-elevated border border-border-strong px-2 py-1 text-[0.72rem] text-text-primary shadow-lg hidden group-hover:block group-focus-within:block"
      :class="placementClasses[placement]"
    >
      {{ text }}
    </span>
  </span>
</template>
