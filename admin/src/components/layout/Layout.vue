<script setup lang="ts">
/**
 * Main authenticated layout — sidebar + topbar + content area.
 *
 * Handles responsive sidebar state:
 * - Desktop (lg+): always visible, full width
 * - Tablet (md): icon-only collapsed sidebar
 * - Mobile (<md): hidden drawer, toggled via topbar hamburger
 */
import { ref } from 'vue'
import Sidebar from './Sidebar.vue'
import Topbar from './Topbar.vue'
import EmailVerificationBanner from './EmailVerificationBanner.vue'
import { RouterView } from 'vue-router'

/** Controls the mobile drawer open state. */
const drawerOpen = ref(false)

/** Toggles the mobile sidebar drawer. */
function toggleSidebar(): void {
  drawerOpen.value = !drawerOpen.value
}

/** Closes the mobile drawer (called on nav item click or backdrop). */
function closeDrawer(): void {
  drawerOpen.value = false
}
</script>

<template>
  <div class="flex h-dvh overflow-hidden bg-surface-base">

    <!-- Mobile backdrop -->
    <Transition name="fade">
      <div
        v-if="drawerOpen"
        class="fixed inset-0 z-30 bg-black/60 backdrop-blur-sm lg:hidden"
        @click="closeDrawer"
      />
    </Transition>

    <!-- Sidebar -->
    <!-- Desktop: always visible | Mobile: fixed drawer -->
    <div
      class="fixed inset-y-0 left-0 z-40 lg:static lg:z-auto lg:flex transition-transform duration-300 ease-in-out"
      :class="drawerOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'"
    >
      <Sidebar @navigate="closeDrawer" />
    </div>

    <!-- Main column: topbar + content -->
    <div class="flex flex-col flex-1 min-w-0 min-h-0 lg:ml-0">
      <Topbar @toggle-sidebar="toggleSidebar" />

      <main class="relative flex-1 min-h-0 overflow-auto">
        <!--
          The content gutter lives here, once, rather than being repeated on
          every view root. A flex column with min-h-full so a nested module
          layout can still claim the full height with flex-1, while a long
          page keeps its bottom padding when scrolled to the end.
        -->
        <div class="flex min-h-full w-full flex-col p-4 sm:p-6 lg:p-8">
          <RouterView />
        </div>
        <EmailVerificationBanner />
      </main>
    </div>

  </div>
</template>

<style>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
