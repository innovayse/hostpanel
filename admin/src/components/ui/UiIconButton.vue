<script setup lang="ts">
/**
 * Square button that holds only an icon.
 *
 * The required `label` becomes the accessible name (`aria-label`) and the native tooltip, since
 * the icon alone says nothing to a screen reader. The icon is the default slot.
 */
import { computed } from 'vue'
import { buttonBaseClasses, buttonRadiusClasses, buttonVariantClasses } from '@/utils/ui/button'
import type { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { iconButtonSizeClasses } from '@/utils/ui/size'
import type { UiSize } from '@/utils/ui/size'

/** Props for UiIconButton. */
const props = withDefaults(defineProps<{
  /** Accessible name of the action, e.g. "Delete row". */
  label: string
  /** Visual look; ghost by default. */
  variant?: UiButtonVariant
  /** Square footprint. */
  size?: UiSize
  /** Native button type. */
  type?: UiButtonType
  /** Blocks clicks. */
  disabled?: boolean
}>(), {
  variant: 'ghost',
  size: 'md',
  type: 'button',
  disabled: false,
})

/** Class list for the chosen look and size. */
const classes = computed<string[]>(() => [
  buttonBaseClasses,
  buttonVariantClasses[props.variant],
  iconButtonSizeClasses[props.size],
  buttonRadiusClasses(props.variant, props.size),
  'shrink-0',
])
</script>

<template>
  <button
    :type="type"
    :aria-label="label"
    :title="label"
    :disabled="disabled"
    :class="classes"
  >
    <slot />
  </button>
</template>
