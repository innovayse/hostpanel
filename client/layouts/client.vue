<template>
  <div class="fixed inset-0 flex flex-col overflow-hidden bg-gray-50 text-gray-900 dark:bg-gray-950 dark:text-white" data-testid="client-layout">
    <!-- Demo-mode notice; renders nothing outside a demo -->
    <LayoutDemoBanner />

    <!-- Full viewport, pinned to the viewport so the browser itself never scrolls -->
    <ClientLayoutTopBar
      :full-name="store.fullName"
      :widget-user="widgetUser"
      :first-name="store.user?.firstName"
      :last-name="store.user?.lastName"
      :email="store.user?.email"
      :nav-items="visibleNavItems"
      :sidebar-open="sidebarOpen"
      @toggle-sidebar="sidebarOpen = !sidebarOpen"
      @sign-out="handleLogout"
    />

    <!-- Body row: sidebar + main, fills the remaining height. Positioned, so the mobile drawer and
         its backdrop cover this row only and leave the banner and top bar (with the X) visible. -->
    <div class="relative flex flex-1 overflow-hidden">
      <ClientLayoutSidebar
        :items="visibleNavItems"
        :open="sidebarOpen"
        @close="sidebarOpen = false"
        @sign-out="handleLogout"
      />

      <!-- Mobile overlay backdrop -->
      <Transition
        enter-active-class="transition-opacity duration-200 ease-out"
        leave-active-class="transition-opacity duration-200 ease-in"
        enter-from-class="opacity-0"
        leave-to-class="opacity-0"
      >
        <div
          v-if="sidebarOpen"
          class="absolute inset-0 z-10 bg-black/50 lg:hidden"
          data-testid="client-sidebar-backdrop"
          @click="sidebarOpen = false"
        />
      </Transition>

      <!-- Main: only this scrolls -->
      <main class="flex min-w-0 flex-1 flex-col overflow-y-auto">
        <div class="p-6 lg:p-8">
          <ClientVerifyEmailBanner class="mb-6" />
          <Suspense>
            <template #default>
              <slot />
            </template>
            <template #fallback>
              <UiSkeleton shape="block" class="min-h-[40dvh]" />
            </template>
          </Suspense>
        </div>
      </main>
    </div>

    <!-- Consent bar; its spacer shrinks the scrolling area so the bar never covers content -->
    <UiCookieBanner />
  </div>
</template>

<script setup lang="ts">
/**
 * Layout of the signed-in client area: top bar, permission-filtered sidebar and the scrolling
 * page slot.
 *
 * The chrome itself lives in `components/client/layout/`; this file owns what spans it: which
 * links the visitor may see, whether the mobile drawer is open, how the platform widget is told
 * who is signed in, and signing out.
 */
import { CLIENT_ACCOUNT_PATH, CLIENT_DASHBOARD_PATH, CLIENT_DOMAINS_PATH, CLIENT_INVOICES_PATH, CLIENT_SERVICES_PATH, CLIENT_TICKETS_PATH } from '~/constants/clientRoutes'
import {
  LayoutDashboard,
  Server,
  Globe,
  FileText,
  MessageSquare,
  UserCog,
  Megaphone,
  BookOpen
} from 'lucide-vue-next'
import { Permission } from '~/composables/usePermissions'
import { useAuthStore } from '~/stores/auth'
import { useClientStore } from '~/stores/client'
import type { ClientNavItem } from '~/types/clientnavitem'
import { useHeaderWidget } from '~/composables/useHeaderWidget'

/** How often the widget script is polled for, in milliseconds, until it has loaded. */
const WIDGET_POLL_MS = 200

/** How many polls to make before giving up on the widget script. */
const WIDGET_POLL_ATTEMPTS = 50

/** Delay before the first poll, giving the mounts time to render, in milliseconds. */
const WIDGET_FIRST_POLL_MS = 50

/** The platform widget's global, when its script has loaded. */
interface InnoWidgetWindow extends Window {
  /** Re-scans the page for the widget mount points. */
  InnoWidget?: { reinit: () => void }
}

/** A sidebar link and the permission needed to see it. */
interface NavEntry extends ClientNavItem {
  /** Permission flag the visitor needs; {@link Permission.None} for everyone. */
  permission: number
}

/** Translator for the strings this file builds itself. */
const { t } = useI18n()
/** Auth store; owns the requests, in-flight flags and refusal sentences this file renders. */
const authStore = useAuthStore()
/** Client store: the signed-in person and their services, invoices, domains and tickets. */
const store = useClientStore()
/** Whether the platform widget is served here; without it nothing is polled for and the top bar draws its own account menu. */
const { enabled: widgetEnabled } = useHeaderWidget()

/** Whether the mobile sidebar drawer is open. */
const sidebarOpen = ref(false)

/** The signed-in person in the widget's own shape: `{ firstName, lastName, email }`. */
const widgetUser = computed(() => {
  const user = store.user
  if (!user?.email) {
    return undefined
  }
  return JSON.stringify({ email: user.email, firstName: user.firstName, lastName: user.lastName })
})

/**
 * Asks the platform widget to re-scan the page for its mount points, when it has loaded.
 *
 * @returns True when the widget was present and re-initialised.
 */
const reinitWidget = (): boolean => {
  const widget = (window as InnoWidgetWindow).InnoWidget
  if (!widget?.reinit) {
    return false
  }
  widget.reinit()
  return true
}

/** The pending widget poll, so leaving the layout can cancel it before it fires on a torn-down page. */
let widgetPoll: ReturnType<typeof setTimeout> | undefined

/** Set when the layout unmounts; a mount still awaiting the identity then schedules no poll. */
let unmounted = false

onMounted(async () => {
  // No `userLoaded` check: the store is the one authority on whether this needs a request,
  // and the flag read here answered "not loaded yet" for a request that was already on the
  // wire, which is how the same page load asked for the identity more than once.
  await store.fetchUser()
  // Reinit the widget after Vue renders the mount points. Poll until it is available: its
  // script may still be loading, because it is included with `async`.
  await nextTick()
  let attempts = 0
  /**
   * One poll: re-initialises the widget, or schedules the next poll while attempts remain.
   *
   * @returns Nothing.
   */
  const tryReinit = (): void => {
    if (unmounted) {
      return
    }
    if (!reinitWidget() && attempts++ < WIDGET_POLL_ATTEMPTS) {
      widgetPoll = setTimeout(tryReinit, WIDGET_POLL_MS)
    }
  }
  // The layout may have been left while the identity was loading; onBeforeUnmount has already run then.
  if (unmounted || !widgetEnabled.value) {
    return
  }
  widgetPoll = setTimeout(tryReinit, WIDGET_FIRST_POLL_MS)
})

// A poll still waiting when the layout goes away would look for the widget on a page that no longer exists.
onBeforeUnmount(() => {
  unmounted = true
  clearTimeout(widgetPoll)
})

// Vue may re-render the layout after reactive state changes, replacing the mount-point
// elements. Reinit the widget if the account button was wiped.
onUpdated(() => {
  if (widgetEnabled.value && !document.getElementById('inno-account-btn')) {
    reinitWidget()
  }
})

/** Every sidebar link with the permission that unlocks it. */
const navItems = computed<NavEntry[]>(() => [
  { to: CLIENT_DASHBOARD_PATH, label: t('client.nav.dashboard'), icon: LayoutDashboard, permission: Permission.None },
  { to: CLIENT_SERVICES_PATH, label: t('client.nav.services'), icon: Server, permission: Permission.ViewProductsServices },
  { to: CLIENT_DOMAINS_PATH, label: t('client.nav.domains'), icon: Globe, permission: Permission.ViewDomains },
  { to: CLIENT_INVOICES_PATH, label: t('client.nav.invoices'), icon: FileText, permission: Permission.ViewPayInvoices },
  { to: CLIENT_TICKETS_PATH, label: t('client.nav.support'), icon: MessageSquare, permission: Permission.ViewOpenSupportTickets },
  { to: '/announcements', label: t('announcements.title'), icon: Megaphone, permission: Permission.None },
  { to: '/knowledgebase', label: t('kb.title'), icon: BookOpen, permission: Permission.None },
  { to: CLIENT_ACCOUNT_PATH, label: t('client.nav.account'), icon: UserCog, permission: Permission.None }
])

/** Nav items filtered by what the signed-in person may do, by the same rule as the actions behind them. */
const visibleNavItems = computed(() =>
  navItems.value.filter((item) => store.mayDo(item.permission))
)

/**
 * Signs the person out through the auth store, which ends the session here and, under SSO,
 * hands the browser to the identity provider's end-session page.
 *
 * @returns Nothing.
 */
const handleLogout = async (): Promise<void> => {
  await authStore.signOut()
}
</script>
