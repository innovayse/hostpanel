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
 * UiFooterLink: a link in a site footer, shared by the aurora, nova and classic footers.
 *
 * At rest it is muted text; on hover and on keyboard focus it turns to the template's brand
 * colour and draws an underline that slides in from the left, with a smooth transition. Colours
 * come from each template's theme tokens (or a light and a dark value of the generic scales), so
 * the same link reads correctly in both colour modes. `to` renders a NuxtLink, `href` an anchor;
 * an absolute `href` opens in a new tab safely.
 */
import { computed, resolveComponent } from 'vue'
import { UiFooterLinkVariant } from '~/utils/ui/enums'

/** Props of UiFooterLink. */
interface Props {
  /** In-app route; renders a NuxtLink. */
  to?: string
  /** URL; renders an anchor (`mailto:`, `tel:` and absolute URLs included). */
  href?: string
  /** Template palette the colours come from. */
  variant?: `${UiFooterLinkVariant}`
  /** Draws the sliding underline on hover and focus; off where the host draws its own indicator. */
  underline?: boolean
  /** `data-testid` of the link. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  to: undefined,
  href: undefined,
  variant: UiFooterLinkVariant.Aurora,
  underline: true,
  testid: undefined,
})

/** Resting and hover colour utilities plus the focus ring colour, per template. */
const VARIANT_CLASSES: Record<UiFooterLinkVariant, string> = {
  [UiFooterLinkVariant.Aurora]: 'text-mut hover:text-ac1 focus-visible:text-ac1 focus-visible:ring-ac1',
  [UiFooterLinkVariant.Nova]: 'text-nova-muted hover:text-nova-brand focus-visible:text-nova-brand focus-visible:ring-nova-brand',
  [UiFooterLinkVariant.Classic]: 'text-classic-muted hover:text-classic-brand focus-visible:text-classic-brand focus-visible:ring-classic-brand',
}

/** Shared layout, transition and keyboard focus ring. */
const BASE_CLASSES = 'relative inline-flex w-fit items-center rounded-sm transition-colors duration-300 focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-offset-transparent'

/** The underline: a hairline under the text that scales in from the left. */
const UNDERLINE_CLASSES = 'after:absolute after:-bottom-0.5 after:left-0 after:h-px after:w-full after:origin-left after:scale-x-0 after:bg-current after:transition-transform after:duration-300 hover:after:scale-x-100 focus-visible:after:scale-x-100'

/** Whether the link leaves the site. */
const isExternal = computed(() => /^https?:\/\//i.test(props.href ?? ''))

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
  BASE_CLASSES,
  VARIANT_CLASSES[props.variant as UiFooterLinkVariant],
  props.underline ? UNDERLINE_CLASSES : '',
].filter(Boolean).join(' '))
</script>
