<script setup lang="ts">
/**
 * Main authenticated layout: sidebar + topbar + content area.
 *
 * Handles responsive sidebar state:
 * - Desktop (lg+): always visible, full width
 * - Smaller screens: hidden, opened as a drawer from the topbar hamburger
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import AppTopbar from '@/components/layout/AppTopbar.vue'
import DemoBanner from '@/components/layout/DemoBanner.vue'
import EmailVerificationBanner from '@/components/layout/EmailVerificationBanner.vue'
import RoutedView from '@/components/layout/RoutedView.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { useLinkPrefetch } from '@/composables/useLinkPrefetch'

/** Translation function and the active language; the language keys the routed view so labels built once at setup re-render when it changes. */
const { t, locale } = useI18n()

// Hovering, focusing or touching any in-app link starts downloading its screen, so the click finds it ready.
useLinkPrefetch(useRouter())

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
  <UiStack direction="row" gap="none" align="stretch" class="h-dvh overflow-hidden bg-surface-base" data-testid="app-layout">
    <!-- Sidebar: always visible on desktop -->
    <UiStack gap="none" class="hidden lg:flex" data-testid="app-sidebar-desktop">
      <AppSidebar />
    </UiStack>

    <!-- Sidebar: drawer on smaller screens -->
    <UiDrawer :open="drawerOpen" side="left" :title="t('layout.menu')" flush narrow @close="closeDrawer">
      <AppSidebar embedded @navigate="closeDrawer" />
    </UiDrawer>

    <!-- Main column: topbar + content -->
    <UiStack gap="none" class="min-h-0 min-w-0 flex-1">
      <DemoBanner />
      <AppTopbar @toggle-sidebar="toggleSidebar" />

      <main class="relative min-h-0 flex-1 overflow-auto">
        <!--
          The content gutter lives here, once, rather than being repeated on
          every view root. A flex column with min-h-full so a nested module
          layout can still claim the full height with flex-1, while a long
          page keeps its bottom padding when scrolled to the end.
        -->
        <UiStack gap="none" class="min-h-full w-full p-4 sm:p-6 lg:p-8">
          <RoutedView :key="locale" />
        </UiStack>
        <EmailVerificationBanner />
      </main>
    </UiStack>
  </UiStack>
</template>
