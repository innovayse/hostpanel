<script setup lang="ts">
/**
 * Square button that holds only an icon.
 *
 * The required `label` becomes the accessible name (`aria-label`) and the native tooltip, since
 * the icon alone says nothing to a screen reader. The icon is the default slot, drawn at the size
 * {@link iconButtonIconSize} pairs with the button's own, so every icon button of the panel reads alike.
 * A `badge` is a count laid over the button's top-right corner (a marker dot on a phone, which has no room for a
 * number); the button's label still says what the count means.
 */
import { computed } from 'vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import { buttonBaseClasses, buttonRadiusClasses, buttonVariantClasses } from '@/utils/ui/button'
import type { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { iconButtonIconClasses, iconButtonSizeClasses } from '@/utils/ui/size'
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
  /** Text of the count laid over the top-right corner; nothing is drawn when absent. */
  badge?: string
  /** Prefix of the badge's `data-testid`s: `-count` for the number, `-dot` for the phone marker. */
  badgeTestid?: string
}>(), {
  variant: 'ghost',
  size: 'md',
  type: 'button',
  disabled: false,
  badge: undefined,
  badgeTestid: 'icon-button-badge',
})

/** Class list for the chosen look and size. */
const classes = computed<string[]>(() => [
  buttonBaseClasses,
  buttonVariantClasses[props.variant],
  iconButtonSizeClasses[props.size],
  iconButtonIconClasses[props.size],
  buttonRadiusClasses(props.size),
  'shrink-0',
  // Only a badge needs the button as its positioning context. A plain button stays free for a caller to place
  // (`absolute`, `fixed`), which a built-in `relative` would silently override.
  props.badge !== undefined ? 'relative' : '',
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
    <!-- Both forms sit on the corner; the ring cuts them out of the surface behind. -->
    <span v-if="badge !== undefined" class="pointer-events-none absolute -right-1 -top-1" aria-hidden="true">
      <UiBadge tone="primary" marker class="ring-2 ring-surface-elevated sm:hidden" :data-testid="`${badgeTestid}-dot`" />
      <UiBadge tone="primary" small class="min-w-[1.125rem] justify-center ring-2 ring-surface-elevated max-sm:hidden" :data-testid="`${badgeTestid}-count`">{{ badge }}</UiBadge>
    </span>
  </button>
</template>
