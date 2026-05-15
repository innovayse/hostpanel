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
import AppSidebar from './AppSidebar.vue'
import AppTopbar from './AppTopbar.vue'
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
  <div class="flex min-h-screen bg-surface-base">

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
      <AppSidebar @navigate="closeDrawer" />
    </div>

    <!-- Main column: topbar + content -->
    <div class="flex flex-col flex-1 min-w-0 lg:ml-0">
      <AppTopbar @toggle-sidebar="toggleSidebar" />

      <main class="relative flex-1 overflow-auto">
        <RouterView />
        <EmailVerificationBanner />
      </main>
    </div>

  </div>
</template>

<style>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
