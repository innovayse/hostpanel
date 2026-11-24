<script setup lang="ts">
/**
 * One entry of a navigation menu (the sidebar): an icon and a label, the current page marked by a filled pill.
 *
 * `to` draws a router link, `href` a plain anchor (`external` opens it in a new tab), and neither a button, which is
 * how an entry that opens a sub-menu is drawn: pass `expanded` and it carries `aria-expanded` and a chevron that turns
 * as the sub-menu opens. `level` picks a top-level entry (icon, 42px) or a link inside a sub-menu (13px, its own piece
 * of the tree guide from `stop` and `activeStop`). In the `rail` a top-level entry is its icon alone, and its label
 * becomes the tooltip and the accessible name (`hint` replaces it with a fuller phrase). The `badge` slot holds a count
 * or a dot beside the label; in the rail the caller places it over the icon's corner. Looks come from `utils/ui/nav`
 * and the theme tokens only. `data-testid` and listeners fall through to the element.
 */
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { UiButtonType } from '@/utils/ui/button'
import { navGuideClasses, navIconClasses, navItemClasses, UiNavItemLevel } from '@/utils/ui/nav'
import { UiSize } from '@/utils/ui/size'
import { UiTextTag, UiTextTone } from '@/utils/ui/text'

/** Props for UiNavItem. */
const props = withDefaults(defineProps<{
  /** Text of the entry; in the rail it is the tooltip and accessible name. */
  label: string
  /** Where the entry is: top level, or a link inside a sub-menu. */
  level?: UiNavItemLevel
  /** Whether the entry is the current page. */
  active?: boolean
  /** Whether the panel is the icon rail: a top-level entry is then its icon alone. */
  rail?: boolean
  /** In-app destination; takes precedence over `href`. */
  to?: RouteLocationRaw
  /** Plain address for a regular anchor. */
  href?: string
  /** Opens the address in a new tab. */
  external?: boolean
  /** Markup of the icon of a top-level entry (an `Icon` entry). */
  icon?: string
  /** Whether the sub-menu this entry opens is open; set only on the button of an entry with a sub-menu. */
  expanded?: boolean
  /** Full phrase for the tooltip and accessible name, such as the label with a waiting count; the label itself in the rail when absent. */
  hint?: string
  /** Place of a sub-menu link in its sub-menu, which lights its piece of the guide up to the active link. */
  stop?: number
  /** Place of the active link in the sub-menu, or -1 when none is active. */
  activeStop?: number
}>(), {
  level: UiNavItemLevel.Top,
  active: false,
  rail: false,
  to: undefined,
  href: undefined,
  external: false,
  icon: undefined,
  expanded: undefined,
  hint: undefined,
  stop: 0,
  activeStop: -1,
})

/** The element the entry is: a router link, a plain anchor or, with neither address, a button. */
const element = computed(() => {
  if (props.to !== undefined) {
    return RouterLink
  }
  return props.href === undefined ? 'button' : 'a'
})

/** Whether the entry is a button, which is not a page and so is never marked as the current one. */
const isButton = computed<boolean>(() => props.to === undefined && props.href === undefined)

/** Attributes of the element that depend on what it is. */
const elementAttrs = computed<Record<string, unknown>>(() => {
  if (props.to !== undefined) {
    return { to: props.to }
  }
  if (props.href === undefined) {
    return { type: UiButtonType.Button }
  }
  return props.external ? { href: props.href, target: '_blank', rel: 'noopener noreferrer' } : { href: props.href }
})

/** Whether the entry is the icon alone: only a top-level entry folds to the rail. */
const iconOnly = computed<boolean>(() => props.rail && props.level === UiNavItemLevel.Top)

/** Tooltip and accessible name, needed only while no label is drawn. */
const name = computed<string | undefined>(() => props.hint ?? (iconOnly.value ? props.label : undefined))

/** Classes of the element for its level, state and the rail, with the guide piece of a sub-menu link. */
const classes = computed<string[]>(() => [
  ...navItemClasses(props.level, props.active, props.rail),
  props.level === UiNavItemLevel.Sub ? navGuideClasses(props.stop, props.activeStop) : '',
])

/** Label classes: a sub-menu label may wrap inside the row, a top-level one fills it. */
const labelClasses = computed<string>(() => (props.level === UiNavItemLevel.Sub ? 'min-w-0 flex-1' : 'flex-1'))
</script>

<template>
  <component
    :is="element"
    v-bind="elementAttrs"
    :class="classes"
    :aria-current="active && !isButton ? 'page' : undefined"
    :aria-expanded="expanded"
    :aria-label="name"
    :title="name"
  >
    <UiIcon v-if="icon !== undefined" :markup="icon" :size="UiSize.Sm" :class="navIconClasses(active)" />
    <UiText v-if="!iconOnly" :as="UiTextTag.Span" :tone="UiTextTone.Plain" :class="labelClasses">{{ label }}</UiText>
    <slot name="badge" />
    <UiIcon
      v-if="expanded !== undefined && !iconOnly"
      :markup="Icon.Chevron"
      :size="UiSize.Sm"
      class="text-text-muted transition-transform duration-300"
      :class="expanded ? 'rotate-90' : ''"
    />
  </component>
</template>
