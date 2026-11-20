<script setup lang="ts">
/**
 * "You are here" trail: the steps from the section down to the current page.
 *
 * Every step but the last is a link (or, for a trail the screen keeps in its own state, a button that runs the step's `onSelect`); the last is the current page, plain text marked `aria-current="page"`. A trail
 * longer than `maxVisible` keeps its first step and the steps nearest the current page and folds the middle ones into
 * a "..." button; the button opens the kit's menu (keyboard accessible: Enter opens it, the arrow keys move, Escape
 * closes) listing the hidden steps as links. Below `md` there is no room for a trail, so only the current page is drawn
 * (a screen that needs a way up keeps its back link there, see `UiBackLink`).
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiMenu from '@/components/ui/UiMenu.vue'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { BREADCRUMB_MAX_VISIBLE, foldBreadcrumb } from '@/utils/ui/breadcrumb'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'
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
}>(), {
  maxVisible: BREADCRUMB_MAX_VISIBLE,
  currentTestid: 'ui-breadcrumb-current',
})

/** One entry of the drawn list: a step of the trail, or (without `item`) the "..." button that stands for the folded ones. */
interface BreadcrumbPart {
  /** Keys the entry in the list. */
  key: string
  /** The step; absent for the fold. */
  item?: UiBreadcrumbItem
}

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
    <ol class="flex min-w-0 items-center gap-1 text-sm">
      <li v-for="part in parts" :key="part.key" class="flex items-center gap-1" :class="part.item !== undefined && isCurrent(part.item) ? 'min-w-0' : 'shrink-0 max-md:hidden'">
        <template v-if="part.item === undefined">
          <UiMenu :actions="hiddenActions" :aria-label="t('layout.breadcrumb.hidden')" testid="ui-breadcrumb-more" />
          <UiIcon :markup="Icon.Chevron" :size="UiSize.Sm" class="text-text-muted" />
        </template>
        <span v-else-if="isCurrent(part.item)" class="truncate text-text-primary" aria-current="page" :data-testid="currentTestid">{{ part.item.label }}</span>
        <template v-else>
          <UiButton v-if="part.item.onSelect !== undefined" :variant="UiButtonVariant.Link" :size="UiSize.Sm" :data-testid="part.item.testid" @click="part.item.onSelect">{{ part.item.label }}</UiButton>
          <UiLink v-else :to="part.item.to" class="text-text-muted transition-colors hover:text-text-primary">{{ part.item.label }}</UiLink>
          <UiIcon :markup="Icon.Chevron" :size="UiSize.Sm" class="text-text-muted" />
        </template>
      </li>
    </ol>
  </nav>
</template>
