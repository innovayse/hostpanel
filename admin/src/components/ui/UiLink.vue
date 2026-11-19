<script setup lang="ts">
/**
 * Link to a route or an address.
 *
 * `to` renders a router link (in-app navigation without a reload); `href` renders a plain anchor,
 * and `external` opens it in a new tab with the safe `rel`. `variant` reuses the button looks, so
 * a link can read as plain text, as an accent link or as a button. A link that looks like a button is an action and
 * takes `action` (`UiActionKind`) or its own `icon`, drawn before the label as `UiButton` does.
 */
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import { resolveActionIcon } from '@/utils/ui/actions'
import type { UiActionKind } from '@/utils/ui/actions'
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
  /** Draws a button-like look as a pill (fully rounded ends), whatever its size. */
  pill?: boolean
  /** Kind of the action; the icon of the kind is drawn before the label. */
  action?: UiActionKind
  /** Own icon (an `Icon` entry) for an action with no kind; wins over the icon of `action`. */
  icon?: string
}>(), {
  to: undefined,
  href: undefined,
  external: false,
  variant: UiButtonVariant.Plain,
  size: 'md',
  pill: false,
  action: undefined,
  icon: undefined,
})

/** Markup of the icon drawn before the label; undefined for a link that names neither an action nor an icon. */
const glyph = computed<string | undefined>(() => resolveActionIcon(props.icon, props.action))

/** Class list for the chosen look. */
const classes = computed<string[]>(() => {
  if (props.variant === UiButtonVariant.Plain) {
    return []
  }
  const isTextLink = props.variant === UiButtonVariant.Link
  const sizing = isTextLink ? buttonLinkSizeClasses[props.size] : buttonSizeClasses[props.size]
  const radius = isTextLink ? '' : buttonRadiusClasses(props.variant, props.size, props.pill)
  return [buttonBaseClasses, buttonVariantClasses[props.variant], sizing, radius]
})
</script>

<template>
  <RouterLink v-if="to !== undefined" :to="to" :class="classes">
    <UiIcon v-if="glyph" :markup="glyph" size="sm" />
    <slot />
  </RouterLink>
  <a
    v-else
    :href="href"
    :target="external ? '_blank' : undefined"
    :rel="external ? 'noopener noreferrer' : undefined"
    :class="classes"
  >
    <UiIcon v-if="glyph" :markup="glyph" size="sm" />
    <slot />
  </a>
</template>
