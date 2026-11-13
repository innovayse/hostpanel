<template>
  <span :class="badgeClasses">
    <Icon v-if="icon" :name="icon" class="mr-1" />
    <slot />
  </span>
</template>

<script setup lang="ts">
/**
 * UiBadge: a small label for status, counts and tags.
 *
 * Colours come from the shared tone map (`utils/ui/tones.ts`), so a "success" badge is the same
 * green as every other success surface.
 */
import { computed } from 'vue'
import { UiSize, UiTone } from '~/utils/ui/enums'
import { BADGE_TONE_CLASSES } from '~/utils/ui/tones'

/** Props of UiBadge. */
interface Props {
  /** Colour family. */
  variant?: `${UiTone}`
  /** Size step. */
  size?: `${UiSize}`
  /** Name of a leading icon (Nuxt Icon). */
  icon?: string
  /** Fully rounded pill instead of a small radius. */
  rounded?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  variant: UiTone.Default,
  size: UiSize.Md,
  icon: undefined,
  rounded: false,
})

/** Padding and type size per size step. */
const SIZE_CLASSES: Record<UiSize, string> = {
  [UiSize.Sm]: 'px-2 py-0.5 text-xs',
  [UiSize.Md]: 'px-2.5 py-1 text-sm',
  [UiSize.Lg]: 'px-3 py-1.5 text-base',
}

/** Class list of the root element. */
const badgeClasses = computed(() => [
  'ui-badge inline-flex items-center font-medium',
  `ui-badge-${props.variant}`,
  BADGE_TONE_CLASSES[props.variant as UiTone],
  SIZE_CLASSES[props.size as UiSize],
  props.rounded ? 'rounded-full' : 'rounded',
].join(' '))
</script>
