<script setup lang="ts">
/**
 * Header of a screen: an optional breadcrumb trail, the title with its status badge, an optional subtitle, and an actions area on the right.
 *
 * A screen that sits below a parent (a record's detail, a form) passes `crumbs`, the whole trail from its section down to
 * itself (`usePageCrumbs` builds it), and the header draws it above the title as the kit's `UiBreadcrumb` in its `Page`
 * layout: every step a link but the last, which is the page itself, the steps kept at every width and a long name cut with an
 * ellipsis. It is the one way up from a page: no screen draws a back link of its own. The header that draws a trail owns it:
 * it tells the page title store, and the top bar leaves its own trail out, so the same steps are never shown twice. The
 * skeleton of such a screen passes `crumbsPending`, which draws a bar where the trail will be.
 *
 * Under the trail the title row wraps: the text column (title row, subtitle and the default slot) takes the flexible space
 * with a 20rem basis and may shrink to nothing, so the title wraps at word boundaries and never letter by letter, and the
 * actions sit to its right while they fit. When they do not (a long actions row, a phone) they wrap onto their own row under the
 * text, and inside that row they wrap again, so nothing overflows at 375px. The title row is the title and, beside it, the `badge`
 * slot (a status badge); the title row and the actions share one minimum height and centre their content, so the buttons sit on the
 * title's line. The subtitle is at most 70 characters wide.
 *
 * The `actions` prop takes the screen's buttons as data (`UiHeaderAction`: label, kind, look and what it does; the icon is the one of the kind), so every header
 * action carries an icon with its label. They are all buttons, except on a phone (below `sm`): there the primary action stays a
 * normal kit button with its icon and label and the others fold into the kit's standard "…" menu trigger beside it
 * (`useFoldedActions`, shared with the bulk bar); the menu rows carry the full label. The `actions` slot takes what is not a plain action (a help
 * link, a filter, a period switch, an error pill) and is drawn before them and stays on a phone. The `badge` slot renders inline after the title (a status badge, a priority badge). The default slot renders under the title
 * (a switcher, a line of facts); a screen puts a wrapping row of its own there, which wraps inside the text
 * column. `loading` draws a title bar (and a subtitle bar when a subtitle is passed or `subtitleLoading` is set) of the heading
 * sizes instead of the text, for a title that comes from data still loading; the actions stay where they will be.
 * `subtitleLoading` is the smaller state of the two: the title is known and drawn, and only a bar stands in for a subtitle that
 * is made of data still loading.
 * An action may carry `pluginIcon` (the plugin it runs through): its picture is drawn on the button in place of the generic icon, named after
 * the plugin because the label does not say it, while the phone menu row keeps the generic icon.
 *
 * The header sits 22px (the concept's section gap) above the content on every screen: its bottom margin is 22px less the
 * gap of the stack it is in (`--stack-gap`, published by `gapClasses`), so a screen's own stack gap never adds to it.
 *
 * The header also announces its title to the page title store, which the top bar of a phone shows in place of the
 * breadcrumb; a header whose title is still `loading` announces nothing, and withdraws what it announced when it goes.
 */
import { computed, onBeforeUnmount, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBreadcrumb from '@/components/ui/UiBreadcrumb.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiMenu from '@/components/ui/UiMenu.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiText from '@/components/ui/UiText.vue'
import { useFoldedActions } from '@/composables/useFoldedActions'
import { usePageTitleStore } from '@/stores/pageTitle'
import { barActionToMenu, drawHeaderAction } from '@/utils/ui/actionBar'
import { UiSize } from '@/utils/ui/size'
import type { UiHeaderAction } from '@/utils/ui/actionBar'
import { UiBreadcrumbVariant } from '@/utils/ui/breadcrumb'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'

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
  /** The whole trail from the screen's section down to the screen itself, outermost first; its last step is the page and is not a link. None draws no trail. */
  crumbs?: UiBreadcrumbItem[]
  /** Draws a bar where the trail will be, for the skeleton of a screen that has one. */
  crumbsPending?: boolean
  /** The screen's actions, each with its icon; the primary one stays a button on a phone and the rest fold into a "…" menu. */
  actions?: UiHeaderAction[]
}>(), {
  title: undefined,
  subtitle: undefined,
  loading: false,
  subtitleLoading: false,
  crumbs: undefined,
  crumbsPending: false,
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

/** Whether this header draws the page's trail (or the bar that stands for it), which the top bar then leaves out. */
const ownsTrail = computed<boolean>(() => props.crumbsPending || (props.crumbs?.length ?? 0) > 0)

watch(() => [props.loading ? undefined : props.title, ownsTrail.value] as const, ([title, trail]) => pageTitle.announce(owner, title, trail), { immediate: true })

onBeforeUnmount(() => pageTitle.release(owner))

/** The actions drawn as buttons (only the primary one on a phone), and the menu rows of the others. */
const { buttons, folded } = useFoldedActions(
  () => props.actions.map(drawHeaderAction),
  (action) => ({ ...barActionToMenu(action), to: action.to, onSelect: action.onSelect }),
)
</script>

<template>
  <header class="flex flex-col gap-3 mb-[calc(1.375rem-var(--stack-gap,0rem))]" :aria-busy="loading || subtitleLoading ? 'true' : undefined">
    <UiBreadcrumb v-if="crumbs !== undefined && crumbs.length > 0" :items="crumbs" :variant="UiBreadcrumbVariant.Page" current-testid="ui-page-header-crumb-current" />
    <UiSkeletonBar v-else-if="crumbsPending" class="h-4 w-56 max-w-full rounded-md" aria-hidden="true" data-testid="ui-page-header-crumbs-skeleton" />
    <div class="flex flex-wrap items-start justify-between gap-x-4 gap-y-3">
      <div class="min-w-0 max-w-full flex-[1_1_20rem]">
        <div v-if="loading" aria-hidden="true" data-testid="ui-page-header-skeleton">
          <div class="flex min-h-10.5 items-center max-sm:min-h-11">
            <UiSkeletonBar class="h-9 w-64 max-w-full rounded-md" />
          </div>
          <UiSkeletonBar v-if="subtitle || subtitleLoading" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" />
        </div>
        <template v-else>
          <div class="flex min-h-10.5 flex-wrap items-center gap-x-3 gap-y-1 max-sm:min-h-11" data-testid="ui-page-header-title-row">
            <UiHeading :level="1" variant="page" class="break-words">
              {{ title }}
            </UiHeading>
            <slot name="badge" />
          </div>
          <UiSkeletonBar v-if="subtitleLoading" class="mt-1.5 h-3.5 w-72 max-w-full rounded-md" aria-hidden="true" data-testid="ui-page-header-subtitle-skeleton" />
          <UiText v-else-if="subtitle" tone="muted" size="md" class="mt-1.5 max-w-[70ch]">
            {{ subtitle }}
          </UiText>
        </template>
        <slot />
      </div>
      <div class="flex min-h-10.5 min-w-0 max-w-full flex-wrap items-center gap-2 max-sm:min-h-11">
        <slot name="actions" />
        <template v-for="action in buttons" :key="action.testid">
          <UiLink v-if="action.to !== undefined" :to="action.to" :variant="action.variant" :icon="action.icon" :title="action.title" :data-testid="action.testid">
            {{ action.label }}
          </UiLink>
          <UiButton v-else type="button" :variant="action.variant" :icon="action.pluginIcon ? undefined : action.icon" :title="action.title" :loading="action.loading" :disabled="action.disabled" :data-testid="action.testid" @click="action.onSelect?.()">
            <UiPluginAvatar v-if="action.pluginIcon && !action.loading" :name="action.pluginIcon.name" :icon-url="action.pluginIcon.iconUrl" :size="UiSize.Xs" data-testid="ui-page-header-plugin-icon" />
            {{ action.label }}
          </UiButton>
        </template>
        <UiMenu v-if="folded.length > 0" :actions="folded" :aria-label="t('ui.actionBar.more')" testid="ui-page-header-more" />
      </div>
    </div>
  </header>
</template>
