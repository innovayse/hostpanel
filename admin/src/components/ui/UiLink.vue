<script setup lang="ts">
/**
 * Link to a route or an address.
 *
 * `to` renders a router link (in-app navigation without a reload); `href` renders a plain anchor,
 * and `external` opens it in a new tab with the safe `rel`. `variant` reuses the button looks, so
 * a link can read as plain text, as an accent link or as a button.
 */
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import { buttonBaseClasses, buttonRadiusClasses, buttonVariantClasses, UiButtonVariant } from '@/utils/ui/button'
import { buttonLinkSizeClasses, buttonSizeClasses } from '@/utils/ui/size'
import type { UiSize } from '@/utils/ui/size'

/** Props for UiLink. */
const props = withDefaults(defineProps<{
  /** In-app destination; takes precedence over `href`. */
  to?: RouteLocationRaw
  /** Plain address for a regular anchor. */
  href?: string
  /** Opens the address in a new tab. */
  external?: boolean
  /** Visual look; `plain` adds no colours. */
  variant?: UiButtonVariant
  /** Padding for the button-like looks, type size for the text-link look. */
  size?: UiSize
}>(), {
  to: undefined,
  href: undefined,
  external: false,
  variant: UiButtonVariant.Plain,
  size: 'md',
})

/** Class list for the chosen look. */
const classes = computed<string[]>(() => {
  if (props.variant === UiButtonVariant.Plain) {
    return []
  }
  const isTextLink = props.variant === UiButtonVariant.Link
  const sizing = isTextLink ? buttonLinkSizeClasses[props.size] : buttonSizeClasses[props.size]
  const radius = isTextLink ? '' : buttonRadiusClasses(props.variant, props.size)
  return [buttonBaseClasses, buttonVariantClasses[props.variant], sizing, radius]
})
</script>

<template>
  <RouterLink v-if="to !== undefined" :to="to" :class="classes">
    <slot />
  </RouterLink>
  <a
    v-else
    :href="href"
    :target="external ? '_blank' : undefined"
    :rel="external ? 'noopener noreferrer' : undefined"
    :class="classes"
  >
    <slot />
  </a>
</template>
