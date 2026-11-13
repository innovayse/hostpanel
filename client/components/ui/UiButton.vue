<template>
  <component
    :is="tag"
    v-bind="tagAttrs"
    :aria-busy="loading || undefined"
    :class="buttonClasses"
  >
    <Loader2 v-if="loading" :size="16" :stroke-width="2" class="animate-spin mr-2" />
    <Icon v-else-if="icon" :name="icon" class="mr-2" />
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiButton: the one button of the portal.
 *
 * Renders a `<button>`, an `<a>` (when `href` is set) or a `NuxtLink` (when `to` is set) with the
 * variant, size and focus look of the theme. `@click` and every other listener fall through to
 * the root element, so there is no emit to declare. While `loading` the button is disabled, marked
 * `aria-busy` and shows a spinner in place of the icon; its label stays.
 */
import { computed, resolveComponent } from 'vue'
import { Loader2 } from 'lucide-vue-next'
import { UiButtonType, UiButtonVariant, UiSize } from '~/utils/ui/enums'
import { BUTTON_SIZE_CLASSES, FOCUS_RING, FOCUS_RING_OFFSET } from '~/utils/ui/tones'

/** Props of UiButton. */
interface Props {
  /** Visual weight of the button. */
  variant?: `${UiButtonVariant}`
  /** Size step. */
  size?: `${UiSize}`
  /** Native button type; ignored when rendered as a link. */
  type?: `${UiButtonType}`
  /** Disables the button. */
  disabled?: boolean
  /** Shows a spinner and disables the button. */
  loading?: boolean
  /** Name of a leading icon (Nuxt Icon). */
  icon?: string
  /** Stretches the button to the width of its container. */
  fullWidth?: boolean
  /** Internal route; renders a NuxtLink. */
  to?: string
  /** External URL; renders an anchor. */
  href?: string
  /** Anchor target, used with `href`. */
  target?: string
  /** Anchor rel, used with `href`. */
  rel?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  variant: UiButtonVariant.Primary,
  size: UiSize.Md,
  type: UiButtonType.Button,
  disabled: false,
  loading: false,
  fullWidth: false,
  icon: undefined,
  to: undefined,
  href: undefined,
  target: undefined,
  rel: undefined,
})

/** Frame and colours per variant, light and dark. */
const VARIANT_CLASSES: Record<UiButtonVariant, string> = {
  [UiButtonVariant.Primary]: 'bg-primary-700 text-on-primary hover:bg-primary-800 active:bg-primary-900',
  [UiButtonVariant.Secondary]: 'bg-gray-200 text-gray-800 hover:bg-gray-300 active:bg-gray-400 dark:bg-white/10 dark:text-gray-100 dark:hover:bg-white/20 dark:active:bg-white/[0.25]',
  [UiButtonVariant.Outline]: 'border-2 border-primary-600 text-primary-600 dark:border-primary-400 dark:text-primary-400 hover:bg-primary-500/10 active:bg-primary-500/20',
  [UiButtonVariant.Ghost]: 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 active:bg-gray-200 dark:active:bg-white/[0.15]',
  [UiButtonVariant.Subtle]: 'bg-primary-500/10 border border-primary-500/30 text-primary-700 hover:bg-primary-500/15 hover:border-primary-500/50 active:bg-primary-500/25 dark:text-primary-300 dark:hover:bg-primary-500/20 dark:active:bg-primary-500/30',
  [UiButtonVariant.Danger]: 'bg-red-600 text-white hover:bg-red-700 active:bg-red-800',
  [UiButtonVariant.Bare]: '',
}

/** Colour of the focus-ring offset gap: the surface the button sits on, so the ring reads on white and on dark. */
const OFFSET_SURFACE = 'ring-offset-white dark:ring-offset-ui-surface'

/** Element or component to render. */
const tag = computed(() => {
  if (props.to) {
    return resolveComponent('NuxtLink')
  }
  if (props.href) {
    return 'a'
  }
  return 'button'
})

/** Attributes that belong to the chosen element. */
const tagAttrs = computed(() => {
  if (props.to) {
    return { to: props.to }
  }
  if (props.href) {
    return { href: props.href, target: props.target, rel: props.rel }
  }
  return { type: props.type, disabled: props.disabled || props.loading }
})

/** Class list of the root element. */
const buttonClasses = computed(() => {
  // `transition`, not `transition-all`: `focus:outline-none` sets a transparent outline, and
  // animating every property fades that outline in from the text colour, so a mouse press
  // flashes a visible outline the focus rules say only the keyboard may draw.
  // `ui-btn` and `ui-btn-<variant>` are template hooks: classic.css restyles them under .tpl-classic.
  const base = 'ui-btn inline-flex items-center justify-center whitespace-nowrap font-medium rounded-lg transition duration-200 disabled:opacity-50 disabled:cursor-not-allowed'
  const width = props.fullWidth ? 'w-full' : ''
  // A bare button carries no palette and no padding step: the screen owns both, and the offset
  // ring (drawn in the page colour) would show as a halo on a surface it does not know.
  if (props.variant === UiButtonVariant.Bare) {
    return [base, FOCUS_RING, width].filter(Boolean).join(' ')
  }
  return [base, `ui-btn-${props.variant}`, FOCUS_RING_OFFSET, OFFSET_SURFACE, VARIANT_CLASSES[props.variant as UiButtonVariant], BUTTON_SIZE_CLASSES[props.size as UiSize], width]
    .filter(Boolean)
    .join(' ')
})
</script>
