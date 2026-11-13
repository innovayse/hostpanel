<template>
  <button
    :type="type"
    :aria-label="label"
    :title="title ?? label"
    :disabled="disabled"
    :data-testid="testid"
    :class="classes"
  >
    <component :is="icon" v-if="icon" :size="iconSize" :stroke-width="2" />
    <slot />
  </button>
</template>

<script setup lang="ts">
/**
 * UiIconButton: a square button that holds only an icon.
 *
 * `label` is required because an icon alone tells a screen reader nothing; it becomes the
 * `aria-label` (and the tooltip unless `title` says otherwise). The icon is a lucide component
 * passed as `icon`, or any content in the default slot.
 */
import { computed } from 'vue'
import type { Component } from 'vue'
import { UiButtonType, UiIconButtonVariant, UiSize } from '~/utils/ui/enums'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Props of UiIconButton. */
interface Props {
  /** Accessible name; required. */
  label: string
  /** A lucide icon component. */
  icon?: Component
  /** Tooltip text; defaults to `label`. */
  title?: string
  /** Look of the button. */
  variant?: `${UiIconButtonVariant}`
  /** Size step. */
  size?: `${UiSize}`
  /** Native button type. */
  type?: `${UiButtonType}`
  /** Disables the button. */
  disabled?: boolean
  /** `data-testid` of the button. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  icon: undefined,
  title: undefined,
  variant: UiIconButtonVariant.Subtle,
  size: UiSize.Md,
  type: UiButtonType.Button,
  disabled: false,
  testid: undefined,
})

/** Frame and colours per variant. */
const VARIANT_CLASSES: Record<UiIconButtonVariant, string> = {
  [UiIconButtonVariant.Subtle]: 'border-gray-200 dark:border-white/10 bg-gray-100 dark:bg-white/5 text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:border-gray-300 dark:hover:border-white/20',
  [UiIconButtonVariant.Ghost]: 'border-transparent text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-white/10 hover:text-gray-900 dark:hover:text-white',
  [UiIconButtonVariant.Primary]: 'border-transparent bg-primary-600 text-on-primary hover:bg-primary-700 active:bg-primary-800',
  [UiIconButtonVariant.Danger]: 'border-transparent text-red-600 dark:text-red-400 hover:bg-red-500/10',
  [UiIconButtonVariant.Bare]: '',
}

/** Square dimensions per size. */
const SIZE_CLASSES: Record<UiSize, string> = {
  [UiSize.Sm]: 'w-8 h-8',
  [UiSize.Md]: 'w-9 h-9',
  [UiSize.Lg]: 'w-10 h-10',
}

/** Icon pixel size per size step. */
const ICON_SIZES: Record<UiSize, number> = {
  [UiSize.Sm]: 14,
  [UiSize.Md]: 16,
  [UiSize.Lg]: 20,
}

/** Pixel size of the icon. */
const iconSize = computed(() => ICON_SIZES[props.size as UiSize])

/** Class list of the button. */
const classes = computed(() => [
  'inline-flex items-center justify-center rounded-lg border transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed',
  FOCUS_RING,
  VARIANT_CLASSES[props.variant as UiIconButtonVariant],
  SIZE_CLASSES[props.size as UiSize],
].join(' '))
</script>
