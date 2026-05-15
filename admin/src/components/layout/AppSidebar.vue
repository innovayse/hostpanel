<script setup lang="ts">
/**
 * Admin sidebar with navigation links.
 *
 * Supports expandable submenus for items with children.
 * Responsive behaviour:
 * - lg+: full sidebar (220px), always visible
 * - Emits `navigate` on link click so the mobile drawer can close.
 */
import { ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useAuthStore } from '../../modules/auth/stores/authStore'
import { useRouter } from 'vue-router'

/** Emitted when a nav link is clicked (used to close mobile drawer). */
const emit = defineEmits<{
  /** Emitted after any navigation link is clicked. */
  navigate: []
}>()

const authStore = useAuthStore()
const router = useRouter()
const route = useRoute()

/** Set of expanded parent item labels. */
const expanded = ref<Set<string>>(new Set())

/**
 * Logs out the current user and redirects to login.
 *
 * @returns Promise that resolves after logout.
 */
async function handleLogout(): Promise<void> {
  await authStore.logout()
  await router.push('/login')
}

/** A sub-menu child item. */
interface NavChild {
  /** Route path. */
  to: string
  /** Display label. */
  label: string
}

/** A top-level navigation item, optionally with children. */
interface NavItem {
  /** Route path (used when no children). */
  to: string
  /** Display label. */
  label: string
  /** SVG path data for the icon. */
  icon: string
  /** Optional sub-menu items. */
  children?: NavChild[]
}

/** Top-level navigation items with SVG path icons. */
const navItems: NavItem[] = [
  {
    to: '/dashboard', label: 'Dashboard',
    icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
  },
  {
    to: '/clients', label: 'Clients',
    icon: 'M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2M9 11a4 4 0 100-8 4 4 0 000 8zm12 2v2m0 0v2m0-2h2m-2 0h-2',
    children: [
      { to: '/clients', label: 'View/Search Clients' },
      { to: '/clients/users', label: 'Manage Users' },
      { to: '/clients/shared-hostings', label: 'Shared Hostings' },
      { to: '/clients/addons', label: 'Service Addons' },
      { to: '/clients/domain-registrations', label: 'Domain Registrations' },
      { to: '/clients/cancellations', label: 'Cancellation Requests' },
      { to: '/clients/affiliates', label: 'Manage Affiliates' },
    ],
  },
  {
    to: '/billing', label: 'Billing',
    icon: 'M9 14l6-6m-5.5.5h.01m4.99 5h.01M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16l3.5-2 3.5 2 3.5-2 3.5 2z',
  },
  {
    to: '/domains', label: 'Domains',
    icon: 'M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9',
  },
  {
    to: '/support', label: 'Support',
    icon: 'M18.364 5.636l-3.536 3.536m0 5.656l3.536 3.536M9.172 9.172L5.636 5.636m3.536 9.192l-3.536 3.536M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-5 0a4 4 0 11-8 0 4 4 0 018 0z',
  },
  {
    to: '/plugins', label: 'Plugins',
    icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4',
  },
  {
    to: '/servers', label: 'Servers',
    icon: 'M5 12H3a2 2 0 00-2 2v4a2 2 0 002 2h18a2 2 0 002-2v-4a2 2 0 00-2-2h-2M5 12V8a2 2 0 012-2h10a2 2 0 012 2v4M5 12h14M8 20h.01M12 20h.01M16 20h.01',
  },
  {
    to: '/integrations', label: 'Integrations',
    icon: 'M13 10V3L4 14h7v7l9-11h-7z',
  },
  {
    to: '/settings', label: 'Settings',
    icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z',
  },
]

/**
 * Checks if a nav item or any of its children is currently active.
 *
 * @param item - The navigation item to check.
 * @returns True if the current route matches.
 */
function isActive(item: NavItem): boolean {
  if (item.to !== '/dashboard' && route.path.startsWith(item.to)) return true
  return route.path === item.to
}

/**
 * Checks if a child route is the currently active route.
 *
 * @param to - The child route path.
 * @returns True if the current route matches exactly.
 */
function isChildActive(to: string): boolean {
  if (to === '/clients') return route.path === '/clients'
  return route.path.startsWith(to)
}

/**
 * Toggles the expanded state of a parent nav item.
 *
 * @param label - The label of the item to toggle.
 */
function toggleExpand(label: string): void {
  const next = new Set(expanded.value)
  if (next.has(label)) next.delete(label)
  else next.add(label)
  expanded.value = next
}

/**
 * Returns whether a parent item is currently expanded.
 * Auto-expands if the current route matches any child.
 *
 * @param item - The navigation item.
 * @returns True if expanded.
 */
function isExpanded(item: NavItem): boolean {
  if (expanded.value.has(item.label)) return true
  if (item.children && isActive(item)) return true
  return false
}
</script>

<template>
  <aside class="flex flex-col w-[220px] min-h-screen shrink-0 bg-surface-panel border-r border-border">

    <!-- Logo -->
    <div class="flex items-center gap-2.5 px-5 py-[18px] border-b border-border">
      <div class="flex items-center justify-center w-8 h-8 rounded-[8px] shrink-0 bg-primary-500/8 border border-primary-500/20">
        <svg width="16" height="16" viewBox="0 0 22 22" fill="none">
          <path d="M11 2L20 7V15L11 20L2 15V7L11 2Z" stroke="url(#sb-g)" stroke-width="1.5" fill="none"/>
          <path d="M11 7L16 10V14L11 17L6 14V10L11 7Z" fill="url(#sb-g)" opacity="0.7"/>
          <defs>
            <linearGradient id="sb-g" x1="2" y1="2" x2="20" y2="20">
              <stop offset="0%" stop-color="#0ea5e9"/>
              <stop offset="100%" stop-color="#a855f7"/>
            </linearGradient>
          </defs>
        </svg>
      </div>
      <span class="font-display font-bold text-[1rem] gradient-brand-text">Innovayse</span>
    </div>

    <!-- Nav links -->
    <nav class="flex-1 px-2.5 pt-3 flex flex-col gap-0.5 overflow-y-auto pb-2">
      <template v-for="item in navItems" :key="item.to">

        <!-- Item WITH children → toggle button -->
        <template v-if="item.children">
          <button
            class="relative w-full flex items-center gap-2.5 px-3 py-2 rounded-[9px] text-[0.84rem] font-medium transition-all duration-150 text-left"
            :class="isActive(item)
              ? 'text-text-primary bg-primary-500/8'
              : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.04]'"
            @click="toggleExpand(item.label)"
          >
            <span
              v-if="isActive(item)"
              class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand"
            />

            <svg
              class="w-4 h-4 shrink-0 transition-opacity duration-150"
              :class="isActive(item) ? 'opacity-100 text-primary-400' : 'opacity-60'"
              viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"
              stroke-linecap="round" stroke-linejoin="round"
            >
              <path :d="item.icon"/>
            </svg>

            <span class="flex-1">{{ item.label }}</span>

            <!-- Chevron -->
            <svg
              class="w-3 h-3 text-text-muted transition-transform duration-200"
              :class="isExpanded(item) ? 'rotate-90' : ''"
              viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
              stroke-linecap="round" stroke-linejoin="round"
            >
              <polyline points="9 18 15 12 9 6"/>
            </svg>
          </button>

          <!-- Sub-menu -->
          <div
            v-show="isExpanded(item)"
            class="flex flex-col gap-0.5 pl-[1.65rem] mt-0.5 mb-0.5"
          >
            <RouterLink
              v-for="child in item.children"
              :key="child.to"
              :to="child.to"
              class="relative flex items-center gap-2 px-3 py-[5px] rounded-[7px] text-[0.78rem] font-medium transition-all duration-150 no-underline"
              :class="isChildActive(child.to)
                ? 'text-primary-400 bg-primary-500/8'
                : 'text-text-muted hover:text-text-secondary hover:bg-white/[0.03]'"
              @click="emit('navigate')"
            >
              <span
                class="w-1 h-1 rounded-full shrink-0"
                :class="isChildActive(child.to) ? 'bg-primary-400' : 'bg-text-muted/40'"
              />
              {{ child.label }}
            </RouterLink>
          </div>
        </template>

        <!-- Item WITHOUT children → direct link -->
        <RouterLink
          v-else
          :to="item.to"
          class="relative flex items-center gap-2.5 px-3 py-2 rounded-[9px] text-[0.84rem] font-medium transition-all duration-150 no-underline"
          :class="isActive(item)
            ? 'text-text-primary bg-primary-500/8'
            : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.04]'"
          @click="emit('navigate')"
        >
          <span
            v-if="isActive(item)"
            class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand"
          />

          <svg
            class="w-4 h-4 shrink-0 transition-opacity duration-150"
            :class="isActive(item) ? 'opacity-100 text-primary-400' : 'opacity-60'"
            viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"
            stroke-linecap="round" stroke-linejoin="round"
          >
            <path :d="item.icon"/>
          </svg>

          <span>{{ item.label }}</span>
        </RouterLink>

      </template>
    </nav>

    <!-- Footer -->
    <div class="flex items-center justify-between px-3 py-3 border-t border-border">
      <div class="flex items-center gap-2">
        <div class="flex items-center justify-center w-7 h-7 rounded-[7px] gradient-brand text-white text-[0.7rem] font-bold font-display shrink-0">
          A
        </div>
        <span class="text-[0.8rem] text-text-secondary font-medium">Admin</span>
      </div>

      <button
        @click="handleLogout"
        title="Sign out"
        class="flex items-center justify-center w-7 h-7 rounded-[7px] border border-border bg-transparent text-text-muted cursor-pointer transition-all duration-150 hover:text-status-red hover:border-status-red/30 hover:bg-status-red/6"
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9"/>
        </svg>
      </button>
    </div>

  </aside>
</template>
