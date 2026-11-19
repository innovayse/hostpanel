<script setup lang="ts">
/**
 * Header of a screen: title, optional subtitle, and an actions area on the right.
 *
 * A screen that returns to a parent (a detail, a form) passes `backTo` and a short `backLabel` naming the destination; the
 * header then draws the kit's back pill (`UiBackLink`) on its row, left of the title. Both are needed: without either, no
 * back link is drawn; the skeleton of such a screen passes `backPending` instead of `backTo`, which draws the pill with its
 * label and no target.
 *
 * The `actions` prop takes the screen's buttons as data (`UiHeaderAction`: label, icon, look and what it does), so every header
 * action carries an icon with its label; on a phone only the primary action stays a button and the rest fold into one "…" menu
 * (`useFoldedActions`, the implementation the bulk bar shares). The `actions` slot takes what is not a plain action (a help
 * link, a filter, a period switch) and is drawn before them. From `md` up they sit top right of the title row, never wrap and
 * never shrink; the title and the subtitle (at most 70 characters wide) take the rest of the row and wrap inside it, so a
 * long subtitle never pushes the actions down. Only below `md` (a phone) do the actions move under the text. The default slot renders under the title (breadcrumbs, filters summary). `loading` draws a
 * title bar (and a subtitle bar when a subtitle is passed or `subtitleLoading` is set) of the heading sizes instead of the text, for a title
 * that comes from data still loading; the actions stay where they will be. `subtitleLoading` is the smaller state of
 * the two: the title is known and drawn, and only a bar stands in for a subtitle that is made of data still loading.
 *
 * The header sits 22px (the concept's section gap) above the content on every screen: its bottom margin is 22px less the
 * gap of the stack it is in (`--stack-gap`, published by `gapClasses`), so a screen's own stack gap never adds to it.
 *
 * The header also announces its title to the page title store, which the top bar of a phone shows in place of the
 * breadcrumb; a header whose title is still `loading` announces nothing, and withdraws what it announced when it goes.
 */
import { onBeforeUnmount, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { RouteLocationRaw } from 'vue-router'
import UiBackLink from '@/components/ui/UiBackLink.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiMenu from '@/components/ui/UiMenu.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiText from '@/components/ui/UiText.vue'
import { useFoldedActions } from '@/composables/useFoldedActions'
import { usePageTitleStore } from '@/stores/pageTitle'
import { barActionToMenu } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** Props for UiPageHeader. */
const props = withDefaults(defineProps<{
  /** Screen title (already translated); may be left out while `loading`, when its bar stands in for it. */
  title?: string
  /** Line under the title. */
  subtitle?: string
  /** Shows placeholder bars in place of the title and the subtitle. */
  loading?: boolean
  /** Shows a placeholder bar in place of the subtitle only, the title being drawn as it is. */
  subtitleLoading?: boolean
  /** The route the back link returns to; the link shows only together with `backLabel`. */
  backTo?: RouteLocationRaw
  /** Short name of the place the back link returns to (already translated), such as "Plugins". */
  backLabel?: string
  /** Draws the back pill with its label and no target, for the skeleton of a screen that has one. */
  backPending?: boolean
  /** Test id of the back link. */
  backTestid?: string
  /** Shows the back link only below `md`, when the top bar's breadcrumb already offers its target from `md` up. */
  backMobileOnly?: boolean
  /** The screen's actions, each with its icon; the primary one stays a button on a phone and the rest fold into a "…" menu. */
  actions?: UiHeaderAction[]
}>(), {
  title: undefined,
  subtitle: undefined,
  loading: false,
  subtitleLoading: false,
  backTo: undefined,
  backLabel: undefined,
  backPending: false,
  backTestid: 'ui-page-header-back',
  backMobileOnly: false,
  /**
   * Default for the header actions.
   *
   * @returns A new empty list, so instances never share one.
   */
  actions: () => [],
})

/** Where the title is announced for the top bar of a phone. */
const pageTitle = usePageTitleStore()

/** Identifies this header to the page title store. */
const owner = Symbol('page-header')

watch(() => (props.loading ? undefined : props.title), (title) => pageTitle.announce(owner, title), { immediate: true })

onBeforeUnmount(() => pageTitle.release(owner))

/** The actions drawn as buttons, and the menu rows of the ones folded on a phone. */
const { buttons, folded } = useFoldedActions(
  () => props.actions,
  (action) => ({ ...barActionToMenu(action), to: action.to, onSelect: action.onSelect }),
)
</script>

<template>
  <header class="flex flex-wrap items-start md:flex-nowrap justify-between gap-4 mb-[calc(1.375rem-var(--stack-gap,0rem))]" :aria-busy="loading || subtitleLoading ? 'true' : undefined">
    <div class="flex min-w-0 flex-1 basis-64 items-start gap-3 md:basis-0">
      <!-- 2px down centres the 32px pill on the 36px line of the title -->
      <UiBackLink v-if="backLabel !== undefined && (backTo !== undefined || backPending)" :to="backTo" :label="backLabel" :mobile-only="backMobileOnly" class="mt-0.5" :data-testid="backTestid" />
      <div class="min-w-0 flex-1">
        <div v-if="loading" aria-hidden="true" data-testid="ui-page-header-skeleton">
          <UiSkeletonBar class="h-9 w-64 max-w-full rounded-md" />
          <UiSkeletonBar v-if="subtitle || subtitleLoading" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" />
        </div>
        <template v-else>
          <UiHeading :level="1" variant="page">
            {{ title }}
          </UiHeading>
          <UiSkeletonBar v-if="subtitleLoading" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" aria-hidden="true" data-testid="ui-page-header-subtitle-skeleton" />
          <UiText v-else-if="subtitle" tone="muted" size="md" class="mt-1.5 max-w-[70ch]">
            {{ subtitle }}
          </UiText>
        </template>
        <slot />
      </div>
    </div>
    <div class="flex max-w-full flex-wrap items-center gap-2 whitespace-nowrap sm:flex-nowrap md:shrink-0">
      <slot name="actions" />
      <template v-for="action in buttons" :key="action.testid">
        <UiLink v-if="action.to !== undefined" :to="action.to" :variant="action.variant" :icon="action.icon" :title="action.title" :data-testid="action.testid">
          {{ action.label }}
        </UiLink>
        <UiButton v-else type="button" :variant="action.variant" :icon="action.icon" :title="action.title" :loading="action.loading" :disabled="action.disabled" :data-testid="action.testid" @click="action.onSelect?.()">
          {{ action.label }}
        </UiButton>
      </template>
      <UiMenu v-if="folded.length > 0" :actions="folded" :aria-label="t('ui.actionBar.more')" testid="ui-page-header-more" />
    </div>
  </header>
</template>
