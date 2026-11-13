<template>
  <component :is="as" :class="classes">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiText: body text with a size, a colour role and a weight.
 *
 * Renders a paragraph by default; pass as="span" for inline text. Colour is a role (muted,
 * subtle, danger and so on), never a literal, so light and dark stay consistent.
 */
import { computed } from 'vue'
import { UiTextSize, UiTextTone, UiTextWeight } from '~/utils/ui/enums'

/** Props of UiText. */
interface Props {
  /** Tag to render. */
  as?: string
  /** Type size. */
  size?: `${UiTextSize}`
  /** Colour role. */
  tone?: `${UiTextTone}`
  /** Font weight. */
  weight?: `${UiTextWeight}`
  /** Truncates to one line with an ellipsis. */
  truncate?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  as: 'p',
  size: UiTextSize.Md,
  tone: UiTextTone.Default,
  weight: UiTextWeight.Normal,
  truncate: false,
})

/** Size utility per step. */
const SIZE_CLASSES: Record<UiTextSize, string> = {
  [UiTextSize.Xs]: 'text-xs',
  [UiTextSize.Sm]: 'text-sm',
  [UiTextSize.Md]: 'text-base',
  [UiTextSize.Lg]: 'text-lg',
  [UiTextSize.Inherit]: '',
}

/** Colour utilities per role. */
const TONE_CLASSES: Record<UiTextTone, string> = {
  [UiTextTone.Default]: 'text-gray-900 dark:text-white',
  [UiTextTone.Muted]: 'text-gray-600 dark:text-gray-300',
  [UiTextTone.Subtle]: 'text-gray-500 dark:text-gray-400',
  [UiTextTone.Primary]: 'text-primary-600 dark:text-primary-400',
  [UiTextTone.Success]: 'text-green-700 dark:text-green-400',
  [UiTextTone.Danger]: 'text-red-600 dark:text-red-400',
  [UiTextTone.Inherit]: '',
}

/** Weight utility per step. */
const WEIGHT_CLASSES: Record<UiTextWeight, string> = {
  [UiTextWeight.Normal]: 'font-normal',
  [UiTextWeight.Medium]: 'font-medium',
  [UiTextWeight.Semibold]: 'font-semibold',
  [UiTextWeight.Bold]: 'font-bold',
}

/** Class list of the root element. */
const classes = computed(() => [
  SIZE_CLASSES[props.size as UiTextSize],
  TONE_CLASSES[props.tone as UiTextTone],
  WEIGHT_CLASSES[props.weight as UiTextWeight],
  props.truncate ? 'truncate' : '',
].filter(Boolean).join(' '))
</script>
