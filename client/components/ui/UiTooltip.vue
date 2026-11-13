<template>
  <div ref="wrapperRef" class="relative inline-block">
    <!-- Trigger -->
    <div data-testid="ui-tooltip-trigger" @click="toggle">
      <slot />
    </div>

    <!-- Popover -->
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="opacity-0 scale-95 translate-y-1"
      enter-to-class="opacity-100 scale-100 translate-y-0"
      leave-active-class="transition duration-100 ease-in"
      leave-from-class="opacity-100 scale-100 translate-y-0"
      leave-to-class="opacity-0 scale-95 translate-y-1"
    >
      <div
        v-if="open"
        ref="tooltipRef"
        role="tooltip"
        data-testid="ui-tooltip-content"
        class="absolute z-50 top-full mt-1.5 left-[var(--tip-left,0px)] rounded-xl border border-gray-200 dark:border-white/15 bg-ui-popover shadow-xl text-xs text-gray-700 dark:text-gray-300 leading-relaxed p-4 w-72 max-w-[calc(100vw-2rem)]"
      >
        <slot name="content" />
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
/**
 * UiTooltip: a click-to-open popover anchored under its trigger.
 *
 * The popover is nudged horizontally so it never leaves the viewport; the nudge is written as the
 * `--tip-left` custom property on the popover (read by a Tailwind class), not as an inline style.
 * It closes on an outside click.
 */
import { nextTick, ref } from 'vue'
import { onClickOutside } from '@vueuse/core'

/** Smallest gap kept between the popover and the screen edge, in px. */
const EDGE_PADDING = 8

/** Whether the popover is showing. */
const open = ref(false)

/** The root element, used to anchor and to detect outside clicks. */
const wrapperRef = ref<HTMLElement | null>(null)

/** The popover element, measured for its width. */
const tooltipRef = ref<HTMLElement | null>(null)

/** Aligns the popover to the right edge of the trigger, clamped inside the viewport. */
const computePosition = (): void => {
  if (!wrapperRef.value || !tooltipRef.value) {
    return
  }
  const rect = wrapperRef.value.getBoundingClientRect()
  const tipWidth = tooltipRef.value.offsetWidth
  const viewportWidth = window.innerWidth

  // Try aligning to the right edge of the trigger first, then clamp both overflows.
  let right = viewportWidth - rect.right
  if (right + tipWidth > viewportWidth - EDGE_PADDING) {
    right = EDGE_PADDING
  }
  if (right < EDGE_PADDING) {
    right = EDGE_PADDING
  }

  // Convert the right offset into a left offset relative to the wrapper.
  const leftOffset = rect.right - rect.left - tipWidth + (viewportWidth - rect.right - right)
  tooltipRef.value.style.setProperty('--tip-left', `${Math.max(EDGE_PADDING - rect.left, leftOffset)}px`)
}

/** Opens or closes the popover; on open it is positioned after it has rendered. */
const toggle = (): void => {
  open.value = !open.value
  if (open.value) {
    void nextTick(computePosition)
  }
}

onClickOutside(wrapperRef, () => {
  open.value = false
})
</script>
