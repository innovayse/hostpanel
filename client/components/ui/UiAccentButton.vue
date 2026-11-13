<template>
  <component
    :is="tag"
    v-bind="tagAttrs"
    :class="classes"
  >
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiAccentButton: a button, link or route link painted in the colour of the nearest `UiAccent`.
 *
 * Use it where the colour is data (a product's brand colour); everywhere else `UiButton` is the
 * button. `to` renders a NuxtLink, `href` an anchor (off-site ones open in a new tab with
 * `rel="noopener noreferrer"`), otherwise a `<button>`. `@click` and every other listener fall
 * through to the root element.
 */
import { computed, resolveComponent } from 'vue'
import { UiButtonType, UiSize } from '~/utils/ui/enums'
import { ACCENT_BUTTON_CLASSES, UiAccentAppearance } from '~/utils/ui/accent'
import { BUTTON_SIZE_CLASSES, FOCUS_RING_OFFSET } from '~/utils/ui/tones'

/** Props of UiAccentButton. */
interface Props {
  /** Fill, tint or outline. */
  appearance?: `${UiAccentAppearance}`
  /** Size step. */
  size?: `${UiSize}`
  /** Native button type; ignored when rendered as a link. */
  type?: `${UiButtonType}`
  /** Disables the button. */
  disabled?: boolean
  /** Stretches the button to the width of its container. */
  fullWidth?: boolean
  /** Fully rounded instead of a large radius. */
  pill?: boolean
  /** Internal route; renders a NuxtLink. */
  to?: string
  /** External URL; renders an anchor. */
  href?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  appearance: UiAccentAppearance.Solid,
  size: UiSize.Md,
  type: UiButtonType.Button,
  disabled: false,
  fullWidth: false,
  pill: false,
  to: undefined,
  href: undefined,
})

/** NuxtLink for a route, an anchor for a URL, a button otherwise. */
const tag = computed(() => {
  if (props.to) {
    return resolveComponent('NuxtLink')
  }
  return props.href ? 'a' : 'button'
})

/** Attributes that belong to the chosen element. */
const tagAttrs = computed(() => {
  if (props.to) {
    return { to: props.to }
  }
  if (props.href) {
    return { href: props.href, target: '_blank', rel: 'noopener noreferrer' }
  }
  return { type: props.type, disabled: props.disabled }
})

/** Class list of the root element. */
const classes = computed(() => [
  'ui-accent-btn inline-flex items-center justify-center gap-2 font-semibold whitespace-nowrap transition-all duration-300 hover:scale-105 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100',
  props.pill ? 'rounded-full' : 'rounded-xl',
  FOCUS_RING_OFFSET,
  // The offset gap is painted in the surface colour so the ring reads on light and dark.
  'ring-offset-white dark:ring-offset-ui-surface',
  ACCENT_BUTTON_CLASSES[props.appearance as UiAccentAppearance],
  BUTTON_SIZE_CLASSES[props.size as UiSize],
  props.fullWidth ? 'w-full' : '',
].filter(Boolean).join(' '))
</script>
