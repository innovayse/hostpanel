<script setup lang="ts">
/**
 * Hover/focus hint around any trigger.
 *
 * The default slot is the trigger; `text` is the hint, shown on pointer hover and on keyboard
 * focus inside the trigger, and linked to it with `aria-describedby`. The hint is an opaque elevated surface.
 * A trigger that cannot hold hover or focus itself (a region of a drawing) drives the hint through `open`:
 * the hint is then drawn while `open` is true and left out while it is false.
 */
import { useId } from 'vue'
import { FLOATING_PANEL_CLASSES } from '@/utils/ui/floating'
import type { UiTooltipPlacement } from '@/utils/ui/tooltip'

/** Props for UiTooltip. */
withDefaults(defineProps<{
  /** The hint (already translated). */
  text: string
  /** Where the hint appears. */
  placement?: UiTooltipPlacement
  /** Shows or hides the hint from outside; left out, the hint follows hover and focus of the trigger. */
  open?: boolean
}>(), {
  placement: 'top',
  open: undefined,
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
      v-if="open !== false"
      :id="id"
      role="tooltip"
      class="pointer-events-none absolute left-1/2 -translate-x-1/2 z-40 whitespace-nowrap rounded-lg px-2.5 py-1.5 text-xs text-text-primary"
      :class="[FLOATING_PANEL_CLASSES, placementClasses[placement], open === undefined ? 'hidden group-hover:block group-focus-within:block' : 'block']"
    >
      {{ text }}
    </span>
  </span>
</template>
