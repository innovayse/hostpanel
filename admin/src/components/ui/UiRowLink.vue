<script setup lang="ts">
/**
 * A link whose whole surface is the target: a row or a card that goes to a route or an address.
 *
 * `to` renders a router link, `href` a plain anchor (`external` opens it in a new tab with the safe `rel`); with neither it
 * is an anchor with no address, which a loading placeholder uses so it keeps the shape of the live row without being a link.
 * `look` (`UiRowLinkLook`) gives the shape; a live link also lights up on hover and shows the focus ring.
 * `data-testid` and other attributes fall through to the root.
 */
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import { focusRingClasses } from '@/utils/ui/input'
import { rowLinkLookClasses, rowLinkLooksWithHover, UiRowLinkLook } from '@/utils/ui/rowLink'

/** Props for UiRowLink. */
const props = withDefaults(defineProps<{
  /** In-app destination; takes precedence over `href`. */
  to?: RouteLocationRaw
  /** Plain address for a regular anchor. */
  href?: string
  /** Opens the address in a new tab. */
  external?: boolean
  /** Shape of the link. */
  look?: UiRowLinkLook
}>(), {
  to: undefined,
  href: undefined,
  external: false,
  look: UiRowLinkLook.Tile,
})

/** Whether the link goes anywhere; a placeholder does not. */
const live = computed<boolean>(() => props.to !== undefined || props.href !== undefined)

/** Class list: the look, plus hover and focus for a live link. */
const classes = computed<string[]>(() => {
  const list = [rowLinkLookClasses[props.look]]
  if (live.value) {
    list.push(focusRingClasses)
    if (rowLinkLooksWithHover.has(props.look)) {
      list.push('hover:bg-surface-hover')
    }
  }
  return list
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
