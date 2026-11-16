<script setup lang="ts">
/**
 * Main authenticated layout: sidebar + topbar + content area, over the glowing ground.
 *
 * The shell fills the viewport and never scrolls itself; the content area (`<main>`) is the one scroller. The demo
 * strip, when shown, spans the full width above the sidebar and the main column, so their header lines stay aligned.
 * Responsive sidebar state:
 * - Desktop (lg+): a full-height column against the left edge, open or folded to an icon rail
 * - Below lg: hidden, opened from the topbar menu button as a bottom sheet (grab handle, drag to close, backdrop,
 *   focus trap, Escape closes: the drawer surface provides them)
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import AppTopbar from '@/components/layout/AppTopbar.vue'
import CommandPalette from '@/components/layout/CommandPalette.vue'
import DemoBanner from '@/components/layout/DemoBanner.vue'
import EmailVerificationBanner from '@/components/layout/EmailVerificationBanner.vue'
import RoutedView from '@/components/layout/RoutedView.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiDrawerSheetBelow } from '@/utils/ui/drawer'
import { UiBreakpoint } from '@/utils/ui/layout'
import { useLinkPrefetch } from '@/composables/useLinkPrefetch'
import { useAuthStore } from '@/stores/auth'
import { useDashboardStore } from '@/stores/dashboard'
import { useReleaseStatusStore } from '@/stores/releaseStatus'
import { useVersionStore } from '@/stores/version'

/** Translation function and the active language; the language keys the routed view so labels built once at setup re-render when it changes. */
const { t, locale } = useI18n()

// Hovering, focusing or touching any in-app link starts downloading its screen, so the click finds it ready.
useLinkPrefetch(useRouter())

/** Auth store: who is signed in and with which roles. */
const authStore = useAuthStore()

/** Version store: the installed version every staff role sees, loaded here and nowhere else. */
const versionStore = useVersionStore()

/** Release status store: the upgrade notice data, which only an Admin may read. */
const releaseStatusStore = useReleaseStatusStore()

/** Dashboard store: the headline statistics, whose client and ticket totals the sidebar shows as count badges. */
const dashboardStore = useDashboardStore()

// The one place that loads both. It runs on mount and again when the account or its roles change
// (the version store has already dropped its old value by then), so a failed or refused load never
// sticks. The upgrade notice data is asked for only when the account is an Admin.
watch(
  () => authStore.accountKey,
  (account) => {
    if (account === null) {
      return
    }
    void versionStore.ensureLoaded()
    void dashboardStore.fetchStats()
    if (authStore.isAdmin) {
      void releaseStatusStore.fetch()
    } else {
      releaseStatusStore.reset()
    }
  },
  { immediate: true },
)

/** Controls the mobile drawer open state. */
const drawerOpen = ref(false)

/** Toggles the mobile sidebar drawer. */
const toggleSidebar = (): void => {
  drawerOpen.value = !drawerOpen.value
}

/** Closes the mobile drawer (called on nav item click or backdrop). */
const closeDrawer = (): void => {
  drawerOpen.value = false
}
</script>

<template>
  <!-- The shell: the demo strip across the full width on top, then the sidebar column beside the main column, so the sidebar header and the topbar stay on one line -->
  <UiStack gap="none" class="ground h-dvh overflow-hidden" data-testid="app-layout">
    <DemoBanner />

    <UiStack direction="row" gap="none" align="stretch" class="min-h-0 flex-1">
      <!-- Sidebar: only from lg up (UiStack swaps its own flex for the breakpoint pair, so it takes no room below lg), flush with the left edge and the full height -->
      <UiStack gap="none" :visible-from="UiBreakpoint.Lg" data-testid="app-sidebar-desktop">
        <AppSidebar />
      </UiStack>

      <!-- Sidebar: a bottom sheet below lg (the persistent sidebar above takes over from there) -->
      <UiDrawer :open="drawerOpen" side="left" :title="t('layout.menu')" flush narrow sheet :sheet-below="UiDrawerSheetBelow.Lg" @close="closeDrawer">
        <AppSidebar embedded @navigate="closeDrawer" />
      </UiDrawer>

      <!-- The command palette: one instance for the whole shell, opened by the top bar and by Ctrl K -->
      <CommandPalette />

      <!-- Main column: topbar + content -->
      <UiStack gap="none" class="min-h-0 min-w-0 flex-1">
        <AppTopbar @toggle-sidebar="toggleSidebar" />

        <main class="relative min-h-0 min-w-0 flex-1 overflow-x-hidden overflow-y-auto">
          <!--
            The content gutter lives here, once, rather than being repeated on
            every view root. A flex column with min-h-full so a nested module
            layout can still claim the full height with flex-1, while a long
            page keeps its bottom padding when scrolled to the end.
          -->
          <UiStack gap="none" class="min-h-full w-full min-w-0 p-4 sm:p-6 lg:px-8 lg:py-7">
            <RoutedView :key="locale" />
          </UiStack>
          <EmailVerificationBanner />
        </main>
      </UiStack>
    </UiStack>
  </UiStack>
</template>
