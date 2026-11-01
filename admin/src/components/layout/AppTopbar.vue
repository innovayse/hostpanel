<script setup lang="ts">
/**
 * Admin top navigation bar.
 *
 * Shows the current page title, the theme and language switches, the notification feed (in a side drawer), the
 * signed-in account and the sign-out action. On mobile it emits `toggle-sidebar` to open the
 * navigation drawer.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { SUPPORTED_LOCALES, setLocale } from '@/i18n'
import type { SupportedLocale } from '@/i18n'
import { activityEventLink } from '@/lib/activityEventLink'
import { formatRelativeFromNow } from '@/lib/format'
import { useActivityStore } from '@/stores/activity'
import { useAuthStore } from '@/stores/auth'
import { useThemeStore } from '@/stores/theme'
import { AdminTheme } from '@/types/admintheme'
import type { ActivityEvent } from '@/types/activityevent'
import UiBadge from '@/components/ui/UiBadge.vue'
import AppAccountMenu from '@/components/layout/AppAccountMenu.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'

/** Emitted when the hamburger button is clicked on mobile. */
const emit = defineEmits<{
  /** Emitted to toggle the sidebar drawer on mobile/tablet. */
  'toggle-sidebar': []
}>()

/** Translation function and the active locale. */
const { t, locale } = useI18n()

/** Current route, to derive the page title. */
const route = useRoute()

/** Router, for sign-out and the notification links. */
const router = useRouter()

/** Auth store: the signed-in account and the sign-out action. */
const auth = useAuthStore()

/** Activity store: the notification feed. */
const activity = useActivityStore()

/** Theme store: the operator's light/dark/system choice. */
const theme = useThemeStore()

/** Sun glyph of the light theme. */
const SUN_MARKUP = '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41"/>'

/** Moon glyph of the dark theme. */
const MOON_MARKUP = '<path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>'

/** Monitor glyph of the system theme. */
const MONITOR_MARKUP = '<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/>'

/** Options of the theme select, one per {@link AdminTheme}, each with its glyph. */
const themeOptions = computed(() => [
  { value: AdminTheme.Light, label: t('layout.theme.light'), icon: SUN_MARKUP },
  { value: AdminTheme.Dark, label: t('layout.theme.dark'), icon: MOON_MARKUP },
  { value: AdminTheme.System, label: t('layout.theme.system'), icon: MONITOR_MARKUP },
])

/** Country whose flag stands for each interface language (the flag files are named by country code). */
const FLAG_COUNTRY: Record<SupportedLocale, string> = { en: 'us', hy: 'am', ru: 'ru' }

/**
 * Applies the theme the operator picked, when it is one the panel knows.
 *
 * @param value - The value picked in the select.
 */
const chooseTheme = (value: string): void => {
  const chosen = Object.values(AdminTheme).find(known => known === value)
  if (chosen !== undefined) {
    theme.setTheme(chosen)
  }
}

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

/** Display labels for the language switcher. */
const localeLabels = computed<Record<SupportedLocale, string>>(() => ({
  en: t('layout.localeLabels.en'),
  hy: t('layout.localeLabels.hy'),
  ru: t('layout.localeLabels.ru'),
}))

/** Controls the notification drawer. */
const showNotifications = ref(false)

/** Current page title derived from the active route. */
const pageTitle = computed<string>(() => {
  const match = Object.entries(titleKeys).find(([k]) => route.path === k || route.path.startsWith(`${k}/`))
  return match ? t(match[1]) : t('common.admin')
})

/** Options of the language select. */
const localeOptions = computed(() =>
  SUPPORTED_LOCALES.map(code => ({ value: code, label: localeLabels.value[code], image: `/flags/${FLAG_COUNTRY[code]}.svg` })),
)

/** Number of events in the feed, shown as the badge and header count. */
const notificationCount = computed<number>(() => activity.events.length)

/**
 * The signed-in operator's address, or a neutral label before the profile has loaded.
 *
 * Naming the account lets an operator tell which account they are acting as, which matters on a
 * panel where several people share one screen.
 */
const accountLabel = computed<string>(() => auth.user?.email ?? t('common.admin'))

/**
 * Switches the active locale when the chosen code is one the panel ships.
 *
 * @param code - The code picked in the select.
 */
const chooseLocale = (code: string): void => {
  const chosen = SUPPORTED_LOCALES.find(supported => supported === code)
  if (chosen !== undefined) {
    setLocale(chosen)
  }
}

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

/**
 * Ends the session and returns to the sign-in screen.
 *
 * @returns Promise that resolves once the operator is on the sign-in screen.
 */
const signOut = async (): Promise<void> => {
  await auth.logout()
  await router.push(AppRoute.Login)
}

/** Loads the notification feed once on mount so the badge reflects real data immediately. */
onMounted(() => {
  void activity.fetchActivity()
})
</script>

<template>
  <header class="flex h-14 shrink-0 items-center justify-between border-b border-border bg-surface-panel px-4 lg:px-6" data-testid="app-topbar">
    <!-- Left: hamburger + page title -->
    <UiStack direction="row" align="center" gap="sm">
      <!-- Hamburger: visible on mobile/tablet -->
      <UiIconButton :label="t('common.toggleSidebar')" class="lg:hidden" data-testid="topbar-toggle-sidebar" @click="emit('toggle-sidebar')">
        <UiIcon :markup="MENU_MARKUP" />
      </UiIconButton>

      <UiHeading :level="2" variant="card" data-testid="topbar-title">{{ pageTitle }}</UiHeading>
    </UiStack>

    <!-- Right: theme + language + notifications + account -->
    <UiStack direction="row" align="center" gap="sm">
      <UiSelect
        :model-value="theme.choice"
        :options="themeOptions"
        :placeholder="t('layout.theme.label')"
        :aria-label="t('layout.theme.label')"
        fit
        icon-only
        class="w-16"
        data-testid="topbar-theme"
        @update:model-value="chooseTheme"
      />
      <UiSelect
        :model-value="locale"
        :options="localeOptions"
        :placeholder="localeLabels.en"
        fit
        class="w-24"
        data-testid="topbar-locale"
        @update:model-value="chooseLocale"
      />

      <!-- The badge is overlaid on the bell's top-right corner; the ring cuts it out of the bar surface. -->
      <UiStack direction="row" align="center" class="relative">
        <UiIconButton :label="notificationLabel" data-testid="topbar-notifications" class="hover:bg-surface-hover" @click="openNotifications">
          <UiIcon :markup="BELL_MARKUP" />
        </UiIconButton>
        <UiBadge
          v-if="notificationCount > 0"
          tone="primary"
          small
          aria-hidden="true"
          class="pointer-events-none absolute -right-1 -top-1 min-w-[1.125rem] justify-center ring-2 ring-surface-panel"
          data-testid="topbar-notification-count"
        >
          {{ notificationBadge }}
        </UiBadge>
      </UiStack>

      <AppAccountMenu :email="accountLabel" @sign-out="signOut" />
    </UiStack>

    <!-- Notification feed -->
    <UiDrawer :open="showNotifications" :title="t('common.notifications')" @close="showNotifications = false">
      <UiStack gap="sm" data-testid="notifications-drawer">
        <UiBadge tone="primary" small>{{ t('common.newCount', { count: notificationCount }, notificationCount) }}</UiBadge>

        <UiSkeleton v-if="activity.loading && activity.events.length === 0" :lines="3" />
        <UiEmptyState v-else-if="activity.events.length === 0" :title="t('common.noNotifications')" />
        <UiStack v-else gap="xs">
          <UiButton
            v-for="(event, index) in activity.events"
            :key="`${event.type}-${event.occurredAt}-${index}`"
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
