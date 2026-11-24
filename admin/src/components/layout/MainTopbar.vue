<script setup lang="ts">
/**
 * Admin top navigation bar, a slim blurred strip under the ground.
 *
 * Shows the breadcrumb of the current page (the section down to the page; on a phone the page's own title), the search trigger (a wide field that announces Ctrl K and opens the command palette; as the bar narrows it drops the shortcut chip and then collapses to an icon, by the bar's own width, so a rail layout or a folded sidebar is followed), the
 * theme and language switches, the notification feed (in a side drawer, a bottom sheet on a phone) and the primary
 * action. The signed-in account lives in the sidebar. Below lg it carries the menu button, which emits `toggle-sidebar` to open the navigation sheet; from lg up the
 * sidebar's own header button folds the sidebar, so the bar has no second control for it.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { activityEventLink } from '@/lib/activityEventLink'
import { formatRelativeFromNow } from '@/lib/format'
import { placeholderRows } from '@/lib/placeholderRows'
import { useBreadcrumb } from '@/composables/useBreadcrumb'
import { useDrawerPresentation } from '@/composables/useDrawerPresentation'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { useActivityStore } from '@/stores/activity'
import { useCommandPaletteStore } from '@/stores/commandPalette'
import { usePageTitleStore } from '@/stores/pageTitle'
import type { ActivityEvent } from '@/types/activityevent'
import { UI_BELOW_MD_QUERY } from '@/utils/ui/breakpoints'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiBreadcrumb from '@/components/ui/UiBreadcrumb.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
import LocaleMenu from '@/components/layout/LocaleMenu.vue'
import UiSearchTrigger from '@/components/ui/UiSearchTrigger.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'
import { iconTriggerIconSize, UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiBreakpoint, UiDirection, UiGap, UiStackTag } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'

/** Emitted by the hamburger button on a phone. */
const emit = defineEmits<{
  /** Emitted to toggle the navigation sheet below lg. */
  'toggle-sidebar': []
}>()

/** Translation function and the active locale. */
const { t, locale } = useI18n()

/** Router, for the notification links. */
const router = useRouter()

/** Command palette store: the search trigger opens it. */
const palette = useCommandPaletteStore()

/** Activity store: the notification feed. */
const activity = useActivityStore()

/** Placeholder rows drawn in the notification drawer while the feed loads. */
const FEED_PLACEHOLDER_ROWS = 3

/** The rows of the drawer: the events, or null placeholders while the feed loads. */
const feedRows = computed(() => placeholderRows(activity.events, activity.initialLoading, FEED_PLACEHOLDER_ROWS))

/** The keys the search trigger announces for the command palette. */
const SEARCH_SHORTCUT = 'Ctrl K'

/** Hamburger glyph. */
const MENU_MARKUP = '<line x1="3" y1="6" x2="21" y2="6"/><line x1="3" y1="12" x2="21" y2="12"/><line x1="3" y1="18" x2="21" y2="18"/>'

/** Bell glyph. */
const BELL_MARKUP = '<path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0"/>'

/** The shape of the notification drawer: a bottom sheet on a phone, a side panel from `sm` up. */
const presentation = useDrawerPresentation()

/** Controls the notification drawer. */
const showNotifications = ref(false)

/** The breadcrumb of the current page, which replaces the plain section title. */
const crumbs = useBreadcrumb()

/** The heading of the screen on display, which a phone shows instead of the trail. */
const pageTitle = usePageTitleStore()

/** Whether the screen is below `md`, where the trail is cut to its last step, a generic word such as "Details". */
const phone = useMediaQuery(UI_BELOW_MD_QUERY)

/** What the bar names the page by: the trail, or on a phone the page's own title (the trail's last step when no header shows one yet). */
const trail = computed<UiBreadcrumbItem[]>(() => (phone.value && pageTitle.title !== undefined ? [{ label: pageTitle.title }] : crumbs.value))

/** Number of events in the feed, shown as the badge and header count. */
const notificationCount = computed<number>(() => activity.events.length)

/** Text of the count badge: the exact number up to 9, "9+" beyond. */
const notificationBadge = computed<string>(() => (notificationCount.value > 9 ? '9+' : String(notificationCount.value)))

/** Accessible name of the bell: carries the unread count when there is one. */
const notificationLabel = computed<string>(() =>
  notificationCount.value > 0 ? t('common.notificationsWithCount', { count: notificationCount.value }) : t('common.notifications'),
)

/** Opens the notification drawer, loading the feed the first time it opens. */
const openNotifications = (): void => {
  showNotifications.value = true
  if (activity.events.length === 0 && !activity.loading) {
    void activity.fetchActivity()
  }
}

/**
 * Describes how long ago an event happened, in the active locale.
 *
 * @param iso - ISO 8601 timestamp.
 * @returns Relative wording such as "5 minutes ago".
 */
const relativeTime = (iso: string): string => formatRelativeFromNow(iso, locale.value)

/** Closes the drawer and sends the operator to the full notification feed. */
const viewAllNotifications = (): void => {
  showNotifications.value = false
  void router.push(AppRoute.Notifications)
}

/**
 * Closes the drawer and navigates to the record a notification is about.
 *
 * @param event - The activity event that was clicked.
 */
const goToActivityEvent = (event: ActivityEvent): void => {
  showNotifications.value = false
  void router.push(activityEventLink(event))
}

/** Loads the notification feed once on mount so the badge reflects real data immediately. */
onMounted(() => {
  void activity.fetchActivity()
})
</script>

<template>
  <UiStack :as="UiStackTag.Header" :direction="UiDirection.Row" :align="UiAlignItems.Center" class="@container h-[calc(var(--shell-header-h)+env(safe-area-inset-top))] shrink-0 border-b border-border-shell bg-surface-topbar px-4 pt-[env(safe-area-inset-top)] backdrop-blur-[14px] lg:px-8" data-testid="topbar">
    <!-- Left: hamburger + page title -->
    <!-- The title may shrink (min-w-0) so it is cut with an ellipsis before the controls on the right are pushed off a phone screen; a long Armenian or Russian title would otherwise do that. -->
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" class="min-w-0">
      <!-- Hamburger: opens the navigation drawer, so only below lg; from lg up the sidebar's own header button folds the sidebar -->
      <UiIconButton :label="t('layout.openMenu')" :variant="UiButtonVariant.Ghost" class="lg:hidden" data-testid="topbar-toggle-sidebar" @click="emit('toggle-sidebar')">
        <UiIcon :markup="MENU_MARKUP" :size="iconTriggerIconSize" />
      </UiIconButton>

      <UiBreadcrumb :items="trail" current-testid="topbar-title" />
    </UiStack>

    <!-- Opens the command palette; the shortcut is only announced here. The bar's own width decides the shape (a container query, since the sidebar takes a different share of the screen in the rail layout and when folded): 4xl or more draws the wide field, 5xl or more adds the shortcut chip, and a narrower bar the icon alone, drawn like the other icon triggers of the bar (ghost: no fill, no border). -->
    <UiSearchTrigger
      :label="t('commandPalette.open')"
      :text="t('commandPalette.placeholder')"
      :shortcut="SEARCH_SHORTCUT"
      shortcut-testid="topbar-search-shortcut"
      data-testid="topbar-search"
      @click="palette.open()"
    />

    <!-- Right: theme + language + notifications + the primary action -->
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" class="ml-auto shrink-0">
      <!-- The theme and language pickers do not fit a phone's top bar; on a phone they are in the menu drawer. -->
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" :visible-from="UiBreakpoint.Sm">
        <ThemeToggle testid="topbar-theme" />
        <LocaleMenu testid="topbar-locale" />
      </UiStack>

      <!-- The count sits on the bell's top-right corner (the button draws it); on a phone it is a marker dot, the label still says how many. -->
      <UiIconButton :label="notificationLabel" :variant="UiButtonVariant.Ghost" :badge="notificationCount > 0 ? notificationBadge : undefined" badge-testid="topbar-notification" data-testid="topbar-notifications" @click="openNotifications">
        <UiIcon :markup="BELL_MARKUP" :size="iconTriggerIconSize" />
      </UiIconButton>

      <!-- The primary action: the gradient button, from a tablet up. -->
      <UiStack :visible-from="UiBreakpoint.Md">
        <UiLink :action="UiActionKind.Add" :to="AppRoute.ClientsAdd" :variant="UiButtonVariant.Primary" data-testid="topbar-primary-action">
          {{ t('commandPalette.actions.newClient') }}
        </UiLink>
      </UiStack>
    </UiStack>

    <!-- Notification feed -->
    <UiDrawer :open="showNotifications" :title="t('common.notifications')" :presentation="presentation" @close="showNotifications = false">
      <UiStack :gap="UiGap.Sm" data-testid="notifications-drawer">
        <UiBadge :tone="UiTone.Primary" small>{{ t('common.newCount', { count: notificationCount }, notificationCount) }}</UiBadge>

        <UiEmptyState v-if="!activity.initialLoading && activity.events.length === 0" :title="t('common.noNotifications')" />
        <!-- The feed; while it loads the same rows are drawn with bars where the event goes -->
        <UiStack v-else :gap="UiGap.Xs" :aria-busy="activity.initialLoading ? 'true' : undefined">
          <UiButton
            v-for="(event, index) in feedRows"
            :key="event ? `${event.type}-${event.entityId}-${event.occurredAt}` : `placeholder-${index}`"
            type="button"
            :variant="UiButtonVariant.Ghost"
            block
            class="!justify-start text-left"
            :disabled="event === null"
            :data-testid="`notification-${index}`"
            @click="event && goToActivityEvent(event)"
          >
            <UiStack v-if="event" :gap="UiGap.Xs" class="min-w-0">
              <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ event.message }}</UiText>
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ relativeTime(event.occurredAt) }}</UiText>
            </UiStack>
            <UiStack v-else :gap="UiGap.Xs" class="w-full" aria-hidden="true">
              <UiSkeletonBar class="h-4 w-full rounded" />
              <UiSkeletonBar class="h-3 w-16 rounded" />
            </UiStack>
          </UiButton>
        </UiStack>

        <UiButton type="button" :variant="UiButtonVariant.Link" :title="t('common.viewAllNotifications')" :aria-label="t('common.viewAllNotifications')" data-testid="notifications-view-all" @click="viewAllNotifications">
          {{ t('shared.viewAll') }}
        </UiButton>
      </UiStack>
    </UiDrawer>
  </UiStack>
</template>
