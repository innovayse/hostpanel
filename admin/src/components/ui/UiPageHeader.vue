<script setup lang="ts">
/**
 * Header of a screen: title, optional subtitle, and an actions area on the right.
 *
 * A screen that returns to a parent (a detail, a form) passes `backTo` and a short `backLabel` naming the destination; the
 * header then draws the kit's back pill (`UiBackLink`) on its own row above the title at every width, so it never takes
 * width from the title. Both are needed: without either, no back link is drawn; the skeleton of such a screen passes
 * `backPending` instead of `backTo`, which draws the pill with its label and no target.
 *
 * Under the back row the title row wraps: the text column (title, subtitle and the default slot) takes the flexible space
 * with a 20rem basis and may shrink to nothing, so the title wraps at word boundaries and never letter by letter, and the
 * actions sit to its right while they fit. When they do not (a long actions row, a phone) they wrap onto their own row under the
 * text, and inside that row they wrap again, so nothing overflows at 375px. The subtitle is at most 70 characters wide.
 *
 * The `actions` prop takes the screen's buttons as data (`UiHeaderAction`: label, icon, look and what it does), so every header
 * action carries an icon with its label; from `md` up they are buttons, and below `md` (a phone) they all collapse into one
 * compact 44px icon trigger (a plus when there is a primary action, the overflow icon otherwise) that opens a menu with every
 * action, the primary one first, under its full label (`useCollapsedActions`). The `actions` slot takes what is not a plain action (a help
 * link, a filter, a period switch, an error pill) and is drawn before them and stays on a phone. The default slot renders under the title
 * (breadcrumbs, a switcher and a status badge); a screen puts a wrapping row of its own there, which wraps inside the text
 * column. `loading` draws a title bar (and a subtitle bar when a subtitle is passed or `subtitleLoading` is set) of the heading
 * sizes instead of the text, for a title that comes from data still loading; the actions stay where they will be.
 * `subtitleLoading` is the smaller state of the two: the title is known and drawn, and only a bar stands in for a subtitle that
 * is made of data still loading.
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
import { useCollapsedActions } from '@/composables/useCollapsedActions'
import { usePageTitleStore } from '@/stores/pageTitle'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
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

/** The actions drawn as buttons from `md` up, and the menu rows they all become below it. */
const { buttons, rows, hasPrimary } = useCollapsedActions(
  () => props.actions,
  (action) => ({ ...barActionToMenu(action), to: action.to, onSelect: action.onSelect }),
)
</script>

<template>
  <header class="flex flex-col gap-3 mb-[calc(1.375rem-var(--stack-gap,0rem))]" :aria-busy="loading || subtitleLoading ? 'true' : undefined">
    <UiBackLink v-if="backLabel !== undefined && (backTo !== undefined || backPending)" :to="backTo" :label="backLabel" :mobile-only="backMobileOnly" class="self-start" :data-testid="backTestid" />
    <div class="flex flex-wrap items-start justify-between gap-x-4 gap-y-3">
      <div class="min-w-0 max-w-full flex-[1_1_20rem]">
        <div v-if="loading" aria-hidden="true" data-testid="ui-page-header-skeleton">
          <UiSkeletonBar class="h-9 w-64 max-w-full rounded-md" />
          <UiSkeletonBar v-if="subtitle || subtitleLoading" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" />
        </div>
        <template v-else>
          <UiHeading :level="1" variant="page" class="break-words">
            {{ title }}
          </UiHeading>
          <UiSkeletonBar v-if="subtitleLoading" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" aria-hidden="true" data-testid="ui-page-header-subtitle-skeleton" />
          <UiText v-else-if="subtitle" tone="muted" size="md" class="mt-1.5 max-w-[70ch]">
            {{ subtitle }}
          </UiText>
        </template>
        <slot />
      </div>
      <div class="flex min-w-0 max-w-full flex-wrap items-center gap-2">
        <slot name="actions" />
        <template v-for="action in buttons" :key="action.testid">
          <UiLink v-if="action.to !== undefined" :to="action.to" :variant="action.variant" :icon="action.icon" :title="action.title" :data-testid="action.testid">
            {{ action.label }}
          </UiLink>
          <UiButton v-else type="button" :variant="action.variant" :icon="action.icon" :title="action.title" :loading="action.loading" :disabled="action.disabled" :data-testid="action.testid" @click="action.onSelect?.()">
            {{ action.label }}
          </UiButton>
        </template>
        <UiMenu v-if="rows.length > 0" :actions="rows" :aria-label="hasPrimary ? t('shared.actions') : t('ui.actionBar.more')" testid="ui-page-header-more">
          <span
            class="inline-flex size-11 items-center justify-center rounded-control transition-colors"
            :class="hasPrimary ? 'gradient-brand shadow-glow-primary text-text-on-primary' : 'text-text-muted group-hover:bg-surface-hover group-hover:text-text-primary'"
            data-testid="ui-page-header-trigger"
          >
            <UiIcon :markup="hasPrimary ? Icon.Add : Icon.MoreHorizontal" size="sm" />
          </span>
        </UiMenu>
      </div>
    </div>
  </header>
</template>
