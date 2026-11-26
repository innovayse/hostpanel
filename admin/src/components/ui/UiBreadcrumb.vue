<script setup lang="ts">
/**
 * "You are here" trail: the steps from the section down to the current page.
 *
 * Every step but the last is a link (or, for a trail the screen keeps in its own state, a button that runs the step's `onSelect`); the last is the current page, plain text marked `aria-current="page"`. A trail
 * longer than `maxVisible` keeps its first step and the steps nearest the current page and folds the middle ones into
 * a "..." button; the button opens the kit's menu (keyboard accessible: Enter opens it, the arrow keys move, Escape
 * closes) listing the hidden steps as links.
 *
 * It has two layouts (`variant`). The `Bar` layout is the top bar's: one line, and below `md`, where there is no room for
 * a trail, only the current page is drawn. The `Page` layout is the one `UiPageHeader` draws above its title: every step
 * stays at every width, a trail that does not fit wraps onto a second line, and a long name (a client, a domain) is cut
 * with an ellipsis, its full text in the step's `title`. A step marked `pending` draws a bar in place of its label.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiMenu from '@/components/ui/UiMenu.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { BREADCRUMB_MAX_VISIBLE, foldBreadcrumb, UiBreadcrumbVariant } from '@/utils/ui/breadcrumb'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'
import { focusRingClasses } from '@/utils/ui/input'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiSize } from '@/utils/ui/size'

/** Resolves the locale keys of the trail's accessible captions. */
const { t } = useI18n()

/** Props for UiBreadcrumb. */
const props = withDefaults(defineProps<{
  /** The trail, outermost first; the last step is the current page and is not a link. */
  items: UiBreadcrumbItem[]
  /** How many steps are drawn before the middle ones fold into the "..." button. */
  maxVisible?: number
  /** Test id of the current page's step. */
  currentTestid?: string
  /** How the trail is laid out: the top bar's single line, or the wrapping trail above a page title. */
  variant?: UiBreadcrumbVariant
}>(), {
  maxVisible: BREADCRUMB_MAX_VISIBLE,
  currentTestid: 'ui-breadcrumb-current',
  variant: UiBreadcrumbVariant.Bar,
})

/** The classes of one layout of the trail. */
interface BreadcrumbClasses {
  /** The list of steps. */
  list: string
  /** A step that is a parent of the current page. */
  parent: string
  /** The step that is the current page. */
  current: string
  /** The text of the current page. */
  currentText: string
  /** A parent drawn as a link. */
  link: string
}

/** The classes of each layout, written out whole because a class built from pieces would emit no CSS. */
const CLASSES: Record<UiBreadcrumbVariant, BreadcrumbClasses> = {
  [UiBreadcrumbVariant.Bar]: {
    list: 'flex min-w-0 items-center gap-1 text-sm',
    parent: 'shrink-0 max-md:hidden',
    current: 'min-w-0',
    currentText: 'truncate text-text-primary',
    link: `rounded-sm text-text-muted transition-colors hover:text-text-primary ${focusRingClasses}`,
  },
  [UiBreadcrumbVariant.Page]: {
    list: 'flex min-w-0 flex-wrap items-center gap-x-1 gap-y-0.5 text-sm',
    parent: 'min-w-0 max-w-[10rem] sm:max-w-[16rem]',
    current: 'min-w-0 max-w-full',
    currentText: 'truncate text-text-secondary',
    link: `min-w-0 truncate rounded-sm text-text-muted transition-colors hover:text-text-primary ${focusRingClasses}`,
  },
}

/** One entry of the drawn list: a step of the trail, or (without `item`) the "..." button that stands for the folded ones. */
interface BreadcrumbPart {
  /** Keys the entry in the list. */
  key: string
  /** The step; absent for the fold. */
  item?: UiBreadcrumbItem
}

/** The classes of the chosen layout. */
const classes = computed<BreadcrumbClasses>(() => CLASSES[props.variant])

/** The trail split into what is drawn before the fold, what hides in it and what is drawn after it. */
const fold = computed(() => foldBreadcrumb(props.items, props.maxVisible))

/** The list as drawn: the head, the fold when something is folded, the tail. */
const parts = computed<BreadcrumbPart[]>(() => [
  ...fold.value.head.map((item, index) => ({ key: `head-${index}`, item })),
  ...(fold.value.hidden.length > 0 ? [{ key: 'fold' }] : []),
  ...fold.value.tail.map((item, index) => ({ key: `tail-${index}`, item })),
])

/** The folded steps as menu entries: links to the steps they stand for. */
const hiddenActions = computed<UiMenuAction[]>(() =>
  fold.value.hidden.map((item, index) => ({ label: item.label, icon: Icon.Chevron, to: item.to, onSelect: item.onSelect, testid: `ui-breadcrumb-hidden-${index}` })),
)

/**
 * Whether a step is the current page: the last one of the trail.
 *
 * @param item - A step of the trail.
 * @returns True for the last step.
 */
const isCurrent = (item: UiBreadcrumbItem): boolean => item === props.items[props.items.length - 1]
</script>

<template>
  <nav v-if="items.length > 0" :aria-label="t('layout.breadcrumb.label')" class="min-w-0" data-testid="ui-breadcrumb">
    <ol :class="classes.list">
      <li v-for="part in parts" :key="part.key" class="flex items-center gap-1" :class="part.item !== undefined && isCurrent(part.item) ? classes.current : classes.parent">
        <template v-if="part.item === undefined">
          <UiMenu :actions="hiddenActions" :aria-label="t('layout.breadcrumb.hidden')" testid="ui-breadcrumb-more" />
          <UiIcon :markup="Icon.Chevron" :size="UiSize.Sm" class="shrink-0 text-text-muted" />
        </template>
        <template v-else-if="isCurrent(part.item)">
          <UiSkeletonBar v-if="part.item.pending === true" class="h-4 w-24 rounded-md" aria-hidden="true" :data-testid="`${currentTestid}-pending`" />
          <span v-else :class="classes.currentText" aria-current="page" :title="part.item.label" :data-testid="currentTestid">{{ part.item.label }}</span>
        </template>
        <template v-else>
          <UiButton v-if="part.item.onSelect !== undefined" :variant="UiButtonVariant.Link" :size="UiSize.Sm" :data-testid="part.item.testid" @click="part.item.onSelect">{{ part.item.label }}</UiButton>
          <RouterLink v-else-if="part.item.to !== undefined" :to="part.item.to" :title="part.item.label" :class="classes.link">{{ part.item.label }}</RouterLink>
          <span v-else :class="classes.link">{{ part.item.label }}</span>
          <UiIcon :markup="Icon.Chevron" :size="UiSize.Sm" class="shrink-0 text-text-muted" />
        </template>
      </li>
    </ol>
  </nav>
</template>
