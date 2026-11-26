<script setup lang="ts">
/**
 * Admin top navigation bar, a slim blurred strip under the ground.
 *
 * Shows the breadcrumb of the current page (the section down to the page; on a phone the page's own title; nothing on a screen whose header draws its own trail above the title), the search trigger (a wide field that announces Ctrl K and opens the command palette; as the bar narrows it drops the shortcut chip and then collapses to an icon, by the bar's own width, so a rail layout or a folded sidebar is followed), the
 * theme and language switches, the notification feed (in a side drawer, a bottom sheet on a phone) and the primary
 * action. The signed-in account lives in the sidebar. Below lg it carries the menu button, which emits `toggle-sidebar` to open the navigation sheet; from lg up the
 * sidebar's own header button folds the sidebar, so the bar has no second control for it.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useBreadcrumb } from '@/composables/useBreadcrumb'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { useActivityStore } from '@/stores/activity'
import { useCommandPaletteStore } from '@/stores/commandPalette'
import { usePageTitleStore } from '@/stores/pageTitle'
import { UI_BELOW_MD_QUERY } from '@/utils/ui/breakpoints'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'
import UiBreadcrumb from '@/components/ui/UiBreadcrumb.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import NotificationsDrawer from '@/components/layout/NotificationsDrawer.vue'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
import LocaleMenu from '@/components/layout/LocaleMenu.vue'
import UiSearchTrigger from '@/components/ui/UiSearchTrigger.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiLink from '@/components/ui/UiLink.vue'
import { AppRoute } from '@/lib/routes'
import { iconTriggerIconSize, UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiBreakpoint, UiDirection, UiGap, UiStackTag } from '@/utils/ui/layout'
import { UiActionKind } from '@/utils/ui/actions'

/** Emitted by the hamburger button on a phone. */
const emit = defineEmits<{
  /** Emitted to toggle the navigation sheet below lg. */
  'toggle-sidebar': []
}>()

/** Translation function. */
const { t } = useI18n()

/** Command palette store: the search trigger opens it. */
const palette = useCommandPaletteStore()

/** Activity store: the notification feed, whose unread count is the bell's badge. */
const activity = useActivityStore()

/** The keys the search trigger announces for the command palette. */
const SEARCH_SHORTCUT = 'Ctrl K'

/** Controls the notification drawer. */
const showNotifications = ref(false)

/** The breadcrumb of the current page, which replaces the plain section title. */
const crumbs = useBreadcrumb()

/** The heading of the screen on display, which a phone shows instead of the trail. */
const pageTitle = usePageTitleStore()

/** Whether the screen is below `md`, where the trail is cut to its last step, a generic word such as "Details". */
const phone = useMediaQuery(UI_BELOW_MD_QUERY)

/**
 * What the bar names the page by: on a phone the page's own title (the trail's last step when no header shows one yet),
 * otherwise the trail, unless the screen's header draws the page's trail itself (a record's detail page), which the bar then
 * leaves to it so no step is shown twice.
 */
const trail = computed<UiBreadcrumbItem[]>(() => {
  if (phone.value && pageTitle.title !== undefined) {
    return [{ label: pageTitle.title }]
  }
  return pageTitle.ownsTrail ? [] : crumbs.value
})

/** Number of events not read yet, shown as the badge. */
const notificationCount = computed<number>(() => activity.unreadCount)

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
        <UiIcon :markup="Icon.Menu" :size="iconTriggerIconSize" />
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
        <UiIcon :markup="Icon.Bell" :size="iconTriggerIconSize" />
      </UiIconButton>

      <!-- The primary action: the gradient button, from a tablet up. -->
      <UiStack :visible-from="UiBreakpoint.Md">
        <UiLink :action="UiActionKind.Add" :to="AppRoute.ClientsAdd" :variant="UiButtonVariant.Primary" data-testid="topbar-primary-action">
          {{ t('commandPalette.actions.newClient') }}
        </UiLink>
      </UiStack>
    </UiStack>

    <!-- Notification feed -->
    <NotificationsDrawer :open="showNotifications" @close="showNotifications = false" />
  </UiStack>
</template>
