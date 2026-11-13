<template>
  <component
    :is="tag"
    v-bind="tagAttrs"
    :data-testid="testid"
    :class="classes"
  >
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiLink: a text link in the theme.
 *
 * `to` renders a NuxtLink for in-app navigation; `href` renders an anchor. An off-site `href`
 * (or `external`) opens in a new tab with `rel="noopener noreferrer"` so the target cannot reach
 * back into this window.
 */
import { computed, resolveComponent } from 'vue'
import { UiLinkTone } from '~/utils/ui/enums'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Props of UiLink. */
interface Props {
  /** In-app route; renders a NuxtLink. */
  to?: string
  /** URL; renders an anchor. */
  href?: string
  /** Forces new-tab behaviour for an `href`. */
  external?: boolean
  /** Colour behaviour. */
  tone?: `${UiLinkTone}`
  /** Underlines the text. */
  underline?: boolean
  /** `data-testid` of the link. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  to: undefined,
  href: undefined,
  external: false,
  tone: UiLinkTone.Primary,
  underline: true,
  testid: undefined,
})

/** Colour utilities per tone. */
const TONE_CLASSES: Record<UiLinkTone, string> = {
  [UiLinkTone.Primary]: 'ui-link-primary text-primary-600 dark:text-primary-400 hover:text-primary-700 dark:hover:text-primary-300',
  [UiLinkTone.Muted]: 'text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white',
  [UiLinkTone.Inherit]: 'hover:opacity-80',
}

/** Whether the link leaves the site. */
const isExternal = computed(() => props.external || /^https?:\/\//i.test(props.href ?? ''))

/** NuxtLink for a route, an anchor otherwise. */
const tag = computed(() => (props.to ? resolveComponent('NuxtLink') : 'a'))

/** Attributes that belong to the chosen element. */
const tagAttrs = computed(() => {
  if (props.to) {
    return { to: props.to }
  }
  return isExternal.value
    ? { href: props.href, target: '_blank', rel: 'noopener noreferrer' }
    : { href: props.href }
})

/** Class list of the link. */
const classes = computed(() => [
  'transition-colors rounded-sm',
  FOCUS_RING,
  TONE_CLASSES[props.tone as UiLinkTone],
  props.underline ? 'underline' : '',
].filter(Boolean).join(' '))
</script>
