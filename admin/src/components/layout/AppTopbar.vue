<script setup lang="ts">
/**
 * Admin top navigation bar, a slim blurred strip under the ground.
 *
 * Shows the current page title, the wide search trigger (it announces Ctrl K and opens the command palette), the
 * theme and language switches, the notification feed (in a side drawer, a bottom sheet on a phone) and the primary
 * action. The signed-in account lives in the sidebar. Below lg it carries the menu button, which emits `toggle-sidebar` to open the navigation sheet; from lg up the
 * sidebar's own header button folds the sidebar, so the bar has no second control for it.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { activityEventLink } from '@/lib/activityEventLink'
import { formatRelativeFromNow } from '@/lib/format'
import { useActivityStore } from '@/stores/activity'
import { useCommandPaletteStore } from '@/stores/commandPalette'
import type { ActivityEvent } from '@/types/activityevent'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import ThemeMenu from '@/components/layout/ThemeMenu.vue'
import LocaleMenu from '@/components/layout/LocaleMenu.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { AppRoute } from '@/lib/routes'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiBreakpoint } from '@/utils/ui/layout'

/** Emitted by the hamburger button on a phone. */
const emit = defineEmits<{
  /** Emitted to toggle the navigation sheet below lg. */
  'toggle-sidebar': []
}>()

/** Translation function and the active locale. */
const { t, locale } = useI18n()

/** Current route, to derive the page title. */
const route = useRoute()

/** Router, for the notification links. */
const router = useRouter()

/** Command palette store: the search trigger opens it. */
const palette = useCommandPaletteStore()

/** Activity store: the notification feed. */
const activity = useActivityStore()

/** The keys the search trigger announces for the command palette. */
const SEARCH_SHORTCUT = 'Ctrl K'

/** Hamburger glyph. */
const MENU_MARKUP = '<line x1="3" y1="6" x2="21" y2="6"/><line x1="3" y1="12" x2="21" y2="12"/><line x1="3" y1="18" x2="21" y2="18"/>'

/** Bell glyph. */
const BELL_MARKUP = '<path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0"/>'

/** Maps route paths to i18n keys for the page title. */
const titleKeys: Record<string, string> = {
  [AppRoute.Dashboard]: 'nav.dashboard',
  [AppRoute.Clients]: 'nav.clients',
  [AppRoute.Billing]: 'nav.billing',
  [AppRoute.Services]: 'nav.servicesTitle',
  [AppRoute.Domains]: 'nav.domains',
  [AppRoute.Support]: 'nav.support',
  [AppRoute.Plugins]: 'nav.pluginManager',
  [AppRoute.Servers]: 'nav.servers',
  [AppRoute.Integrations]: 'nav.integrations',
  [AppRoute.Settings]: 'nav.settings',
  [AppRoute.Reports]: 'nav.reports',
  [AppRoute.Orders]: 'nav.orders',
  [AppRoute.Notifications]: 'common.notifications',
}

/** Controls the notification drawer. */
const showNotifications = ref(false)

/** Current page title derived from the active route. */
const pageTitle = computed<string>(() => {
  const match = Object.entries(titleKeys).find(([k]) => route.path === k || route.path.startsWith(`${k}/`))
  return match ? t(match[1]) : t('common.admin')
})

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
  <header class="flex h-[calc(var(--shell-header-h)+env(safe-area-inset-top))] shrink-0 items-center gap-4 border-b border-border-shell bg-surface-topbar px-4 pt-[env(safe-area-inset-top)] backdrop-blur-[14px] lg:px-8" data-testid="app-topbar">
    <!-- Left: hamburger + page title -->
    <!-- The title may shrink (min-w-0) so it is cut with an ellipsis before the controls on the right are pushed off a phone screen; a long Armenian or Russian title would otherwise do that. -->
    <UiStack direction="row" align="center" gap="sm" class="min-w-0">
      <!-- Hamburger: opens the navigation drawer, so only below lg; from lg up the sidebar's own header button folds the sidebar -->
      <UiIconButton :label="t('layout.openMenu')" class="lg:hidden" data-testid="topbar-toggle-sidebar" @click="emit('toggle-sidebar')">
        <UiIcon :markup="MENU_MARKUP" />
      </UiIconButton>

      <UiHeading :level="2" class="min-w-0 truncate font-sans text-sm font-normal text-text-muted" data-testid="topbar-title">{{ pageTitle }}</UiHeading>
    </UiStack>

    <!-- Opens the command palette; the shortcut is only announced here. From a tablet up it is a wide field, on a phone the icon alone, drawn like the other icon triggers of the bar (ghost: no fill, no border). -->
    <UiButton
      type="button"
      :aria-label="t('commandPalette.open')"
      class="flex h-[2.625rem] min-w-0 items-center gap-2.5 rounded-control border border-border bg-surface-field px-3.5 text-sm text-text-muted transition-colors hover:bg-surface-hover max-md:w-10.5 max-md:justify-center max-md:border-transparent max-md:bg-transparent max-md:px-0 max-md:text-text-secondary max-md:hover:bg-surface-hover max-md:hover:text-text-primary md:ml-6 md:w-full md:max-w-[460px] md:flex-1"
      data-testid="topbar-search"
      @click="palette.open()"
    >
      <UiIcon :markup="Icon.Search" size="sm" class="shrink-0" />
      <UiText :tone="UiTextTone.Muted" class="hidden flex-1 truncate text-left md:block">{{ t('commandPalette.placeholder') }}</UiText>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="hidden shrink-0 rounded-md border border-border-strong px-[7px] py-0.5 md:block" data-testid="topbar-search-shortcut">{{ SEARCH_SHORTCUT }}</UiText>
    </UiButton>

    <!-- Right: theme + language + notifications + the primary action -->
    <UiStack direction="row" align="center" gap="sm" class="ml-auto shrink-0">
      <!-- The theme and language pickers do not fit a phone's top bar; on a phone they are in the menu drawer. -->
      <UiStack direction="row" align="center" gap="sm" :visible-from="UiBreakpoint.Sm">
        <ThemeMenu testid="topbar-theme" />
        <LocaleMenu testid="topbar-locale" />
      </UiStack>

      <!--
        The badge is overlaid on the bell's top-right corner; the ring cuts it out of the bar surface. On a phone
        it is a marker dot (the bar has no room for a number; the button's label still says how many).
      -->
      <UiStack direction="row" align="center" class="relative">
        <UiIconButton :label="notificationLabel" data-testid="topbar-notifications" @click="openNotifications">
          <UiIcon :markup="BELL_MARKUP" />
        </UiIconButton>
        <template v-if="notificationCount > 0">
          <UiStack :hidden-from="UiBreakpoint.Sm" class="pointer-events-none absolute right-0.5 top-0.5">
            <UiBadge tone="primary" marker aria-hidden="true" class="ring-2 ring-surface-elevated" data-testid="topbar-notification-dot" />
          </UiStack>
          <UiStack :visible-from="UiBreakpoint.Sm" class="pointer-events-none absolute -right-1 -top-1">
            <UiBadge
              tone="primary"
              small
              aria-hidden="true"
              class="min-w-[1.125rem] justify-center ring-2 ring-surface-elevated"
              data-testid="topbar-notification-count"
            >
              {{ notificationBadge }}
            </UiBadge>
          </UiStack>
        </template>
      </UiStack>

      <!-- The primary action: the gradient button, from a tablet up. -->
      <UiStack :visible-from="UiBreakpoint.Md">
        <UiLink :to="AppRoute.ClientsAdd" :variant="UiButtonVariant.Primary" class="h-[2.625rem] rounded-control px-[18px]" data-testid="topbar-primary-action">
          {{ t('layout.newClient') }}
        </UiLink>
      </UiStack>
    </UiStack>

    <!-- Notification feed -->
    <UiDrawer :open="showNotifications" :title="t('common.notifications')" sheet @close="showNotifications = false">
      <UiStack gap="sm" data-testid="notifications-drawer">
        <UiBadge tone="primary" small>{{ t('common.newCount', { count: notificationCount }, notificationCount) }}</UiBadge>

        <UiSkeleton v-if="activity.loading && activity.events.length === 0" :lines="3" />
        <UiEmptyState v-else-if="activity.events.length === 0" :title="t('common.noNotifications')" />
        <UiStack v-else gap="xs">
          <UiButton
            v-for="(event, index) in activity.events"
            :key="`${event.type}-${event.entityId}-${event.occurredAt}`"
            type="button"
            variant="ghost"
            class="w-full !justify-start text-left"
            :data-testid="`notification-${index}`"
            @click="goToActivityEvent(event)"
          >
            <UiStack gap="xs" class="min-w-0">
              <UiText tone="primary" size="md">{{ event.message }}</UiText>
              <UiText tone="muted" size="xs">{{ relativeTime(event.occurredAt) }}</UiText>
            </UiStack>
          </UiButton>
        </UiStack>

        <UiButton type="button" variant="link" data-testid="notifications-view-all" @click="viewAllNotifications">
          {{ t('common.viewAllNotifications') }}
        </UiButton>
      </UiStack>
    </UiDrawer>
  </header>
</template>
