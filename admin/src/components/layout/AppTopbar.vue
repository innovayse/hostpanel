<script setup lang="ts">
/**
 * Admin top navigation bar.
 *
 * Shows current page title, search, notifications, and user menu.
 * On mobile emits toggle-sidebar to open the drawer.
 */
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'

/** Emitted when the hamburger button is clicked on mobile. */
const emit = defineEmits<{
  /** Emitted to toggle the sidebar drawer on mobile/tablet. */
  'toggle-sidebar': []
}>()

const route = useRoute()

/** Maps route paths to human-readable page titles. */
const titles: Record<string, string> = {
  '/dashboard':   'Dashboard',
  '/clients':     'Clients',
  '/billing':     'Billing',
  '/services':    'Services',
  '/domains':     'Domains',
  '/support':     'Support',
  '/plugins':     'Plugin Manager',
  '/servers':     'Servers',
  '/integrations':'Integrations',
  '/settings':    'Settings',
}

/** Current page title derived from the active route. */
const pageTitle = computed(() => {
  const match = Object.keys(titles).find(k => route.path === k || route.path.startsWith(k + '/'))
  return match ? titles[match] : 'Admin'
})

/** Controls the notification popover visibility. */
const showNotifications = ref(false)

/** Mock notification count — will come from store later. */
const notificationCount = ref(3)

/** Toggles notification dropdown. */
function toggleNotifications(): void {
  showNotifications.value = !showNotifications.value
}
</script>

<template>
  <header class="flex items-center justify-between h-14 px-4 lg:px-6 border-b border-border bg-surface-panel shrink-0">

    <!-- Left: hamburger + page title -->
    <div class="flex items-center gap-3">
      <!-- Hamburger — visible on mobile/tablet -->
      <button
        class="flex lg:hidden items-center justify-center w-8 h-8 rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.05] transition-all"
        @click="emit('toggle-sidebar')"
        aria-label="Toggle sidebar"
      >
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round">
          <line x1="3" y1="6" x2="21" y2="6"/>
          <line x1="3" y1="12" x2="21" y2="12"/>
          <line x1="3" y1="18" x2="21" y2="18"/>
        </svg>
      </button>

      <!-- Page title -->
      <h2 class="font-display font-semibold text-[0.95rem] text-text-primary tracking-tight">
        {{ pageTitle }}
      </h2>
    </div>

    <!-- Right: search + notifications + avatar -->
    <div class="flex items-center gap-2">

      <!-- Search -->
      <div class="hidden md:flex items-center gap-2 h-8 px-3 rounded-lg bg-white/[0.04] border border-border text-text-muted text-[0.8rem] w-52 cursor-pointer hover:border-primary-500/30 hover:bg-primary-500/[0.03] transition-all group">
        <svg class="w-3.5 h-3.5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
          <circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>
        </svg>
        <span class="flex-1">Search…</span>
        <kbd class="text-[0.65rem] px-1 py-0.5 rounded bg-white/[0.06] border border-border font-mono text-text-muted group-hover:border-primary-500/20">⌘K</kbd>
      </div>

      <!-- Notifications -->
      <div class="relative">
        <button
          class="relative flex items-center justify-center w-8 h-8 rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.05] transition-all"
          @click="toggleNotifications"
          aria-label="Notifications"
        >
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round">
            <path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0"/>
          </svg>
          <!-- Badge -->
          <span
            v-if="notificationCount > 0"
            class="absolute top-1 right-1 w-[7px] h-[7px] rounded-full gradient-brand"
          />
        </button>

        <!-- Dropdown -->
        <div
          v-if="showNotifications"
          class="absolute right-0 top-full mt-2 w-72 bg-surface-elevated border border-border rounded-xl shadow-2xl z-50 overflow-hidden"
        >
          <div class="flex items-center justify-between px-4 py-3 border-b border-border">
            <span class="text-[0.8rem] font-semibold font-display text-text-primary">Notifications</span>
            <span class="text-[0.65rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-full px-2 py-0.5">{{ notificationCount }} new</span>
          </div>
          <div class="divide-y divide-border">
            <div class="px-4 py-3 hover:bg-white/[0.03] transition-colors cursor-pointer">
              <p class="text-[0.8rem] text-text-primary mb-0.5">New client registered</p>
              <p class="text-[0.72rem] text-text-muted">2 minutes ago</p>
            </div>
            <div class="px-4 py-3 hover:bg-white/[0.03] transition-colors cursor-pointer">
              <p class="text-[0.8rem] text-text-primary mb-0.5">Invoice #1042 overdue</p>
              <p class="text-[0.72rem] text-text-muted">1 hour ago</p>
            </div>
            <div class="px-4 py-3 hover:bg-white/[0.03] transition-colors cursor-pointer">
              <p class="text-[0.8rem] text-text-primary mb-0.5">Domain expiring soon</p>
              <p class="text-[0.72rem] text-text-muted">3 hours ago</p>
            </div>
          </div>
          <div class="px-4 py-2.5 border-t border-border">
            <button class="text-[0.75rem] text-primary-400 hover:text-primary-300 transition-colors">View all notifications</button>
          </div>
        </div>

        <!-- Backdrop to close -->
        <div v-if="showNotifications" class="fixed inset-0 z-40" @click="showNotifications = false" />
      </div>

      <!-- Divider -->
      <div class="w-px h-5 bg-border mx-1" />

      <!-- User avatar -->
      <div class="flex items-center gap-2 cursor-pointer group">
        <div class="flex items-center justify-center w-7 h-7 rounded-lg gradient-brand text-white text-[0.7rem] font-bold font-display shrink-0">
          A
        </div>
        <span class="hidden sm:block text-[0.8rem] font-medium text-text-secondary group-hover:text-text-primary transition-colors">Admin</span>
        <svg class="hidden sm:block w-3.5 h-3.5 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="6 9 12 15 18 9"/>
        </svg>
      </div>

    </div>
  </header>
</template>
