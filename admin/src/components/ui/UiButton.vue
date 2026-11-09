<script setup lang="ts">
/**
 * Button with a named look.
 *
 * `variant` picks the look (primary gradient, secondary, ghost, danger, link) and `size` the
 * padding; both read theme tokens so the button follows light/dark. The default `plain` look adds
 * no colours, which keeps screens that still pass their own classes unchanged. `loading` swaps the
 * label for a progress indicator and blocks clicks; `block` stretches it to the container.
 * `data-testid` and listeners fall through to the native `<button>`.
 */
import { computed } from 'vue'
import { buttonBaseClasses, buttonVariantClasses, UiButtonVariant } from '@/utils/ui/button'
import type { UiButtonType } from '@/utils/ui/button'
import { buttonLinkSizeClasses, buttonSizeClasses } from '@/utils/ui/size'
import type { UiSize } from '@/utils/ui/size'

/** Props for UiButton. */
const props = withDefaults(defineProps<{
  /** Visual look of the button. */
  variant?: UiButtonVariant
  /** Padding and type size; ignored by the `plain` look. */
  size?: UiSize
  /** Native button type; omitted so a button inside a form keeps the browser default. */
  type?: UiButtonType
  /** Replaces the label with a progress indicator and blocks clicks. */
  loading?: boolean
  /** Blocks clicks without showing progress. */
  disabled?: boolean
  /** Stretches the button to the full width of its container. */
  block?: boolean
}>(), {
  variant: UiButtonVariant.Plain,
  size: 'md',
  type: undefined,
  loading: false,
  disabled: false,
  block: false,
})

/** Class list for the chosen look and size; the link look keeps the type scale but has no padding. */
const classes = computed<string[]>(() => {
  const width = props.block ? 'w-full' : ''
  if (props.variant === UiButtonVariant.Plain) {
    return [width]
  }
  const sizing = props.variant === UiButtonVariant.Link ? buttonLinkSizeClasses[props.size] : buttonSizeClasses[props.size]
  return [buttonBaseClasses, buttonVariantClasses[props.variant], sizing, width]
})
</script>

<template>
  <button
    :type="type"
    :disabled="disabled || loading"
    :aria-busy="loading || undefined"
    :class="classes"
  >
    <span v-if="loading" class="flex items-center justify-center gap-1.5" aria-hidden="true">
      <span class="w-1.5 h-1.5 bg-current rounded-full animate-bounce [animation-delay:0s]" />
      <span class="w-1.5 h-1.5 bg-current rounded-full animate-bounce [animation-delay:0.15s]" />
      <span class="w-1.5 h-1.5 bg-current rounded-full animate-bounce [animation-delay:0.3s]" />
    </span>
    <slot v-else />
  </button>
</template>
