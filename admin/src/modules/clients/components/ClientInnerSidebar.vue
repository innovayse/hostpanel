<script setup lang="ts">
/**
 * Collapsible inner sidebar for client detail sub-pages.
 * Shows icons only by default (~48px), expands to show labels on hover (~180px).
 */
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'

/** Client identifier used to build RouterLink paths. */
const props = defineProps<{
  /** Client ID for constructing sub-page route paths. */
  clientId: string
}>()

const route = useRoute()

/** Whether the sidebar is expanded (hovered). */
const expanded = ref(false)

/** Navigation item definition for the inner sidebar. */
interface InnerNavItem {
  /** SVG icon markup (path elements). */
  icon: string
  /** Display label shown when expanded. */
  label: string
  /** Router path prefix for the item. */
  to: string
}

/** Inner sidebar navigation items. */
const navItems = computed<InnerNavItem[]>(() => [
  {
    icon: 'profile',
    label: 'Profile',
    to: `/clients/${props.clientId}/profile`,
  },
  {
    icon: 'services',
    label: 'Products/Services',
    to: `/clients/${props.clientId}/services`,
  },
  {
    icon: 'domains',
    label: 'Domains',
    to: `/clients/${props.clientId}/domains`,
  },
  {
    icon: 'users',
    label: 'Users',
    to: `/clients/${props.clientId}/users`,
  },
  {
    icon: 'contacts',
    label: 'Contacts',
    to: `/clients/${props.clientId}/contacts`,
  },
  {
    icon: 'billable',
    label: 'Billable Items',
    to: `/clients/${props.clientId}/billable-items`,
  },
  {
    icon: 'invoices',
    label: 'Invoices',
    to: `/clients/${props.clientId}/invoices`,
  },
  {
    icon: 'quotes',
    label: 'Quotes',
    to: `/clients/${props.clientId}/quotes`,
  },
  {
    icon: 'transactions',
    label: 'Transactions',
    to: `/clients/${props.clientId}/transactions`,
  },
])

/**
 * Checks whether a navigation item is currently active based on the route path.
 *
 * @param item - The navigation item to check.
 * @returns True if the current route starts with the item's path.
 */
function isActive(item: InnerNavItem): boolean {
  return route.path.startsWith(item.to)
}
</script>

<template>
  <div
    class="flex flex-col bg-surface-card border-r border-border transition-all duration-200 ease-in-out overflow-hidden shrink-0"
    :class="expanded ? 'w-[180px]' : 'w-[48px]'"
    @mouseenter="expanded = true"
    @mouseleave="expanded = false"
  >
    <nav class="flex flex-col gap-0.5 pt-2">
      <RouterLink
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        class="relative flex items-center gap-2.5 px-3 py-3 transition-all duration-150 no-underline"
        :class="isActive(item)
          ? 'text-primary-400 bg-primary-500/8'
          : 'text-text-muted hover:text-text-secondary hover:bg-white/[0.04]'"
      >
        <!-- Active indicator -->
        <span
          v-if="isActive(item)"
          class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand"
        />

        <!-- Profile icon -->
        <svg
          v-if="item.icon === 'profile'"
          class="w-5 h-5 shrink-0"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2" />
          <circle cx="12" cy="7" r="4" />
        </svg>

        <!-- Products/Services icon -->
        <svg
          v-else-if="item.icon === 'services'"
          class="w-5 h-5 shrink-0"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 003 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z" />
          <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
          <line x1="12" y1="22.08" x2="12" y2="12" />
        </svg>

        <!-- Domains icon -->
        <svg v-else-if="item.icon === 'domains'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10"/>
          <line x1="2" y1="12" x2="22" y2="12"/>
          <path d="M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z"/>
        </svg>

        <!-- Users icon -->
        <svg v-else-if="item.icon === 'users'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/>
          <circle cx="9" cy="7" r="4"/>
          <path d="M23 21v-2a4 4 0 00-3-3.87"/>
          <path d="M16 3.13a4 4 0 010 7.75"/>
        </svg>

        <!-- Contacts icon -->
        <svg v-else-if="item.icon === 'contacts'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M2 3h6a4 4 0 014 4v14a3 3 0 00-3-3H2z"/>
          <path d="M22 3h-6a4 4 0 00-4 4v14a3 3 0 013-3h7z"/>
        </svg>

        <!-- Billable Items icon -->
        <svg v-else-if="item.icon === 'billable'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="1" x2="12" y2="23" />
          <path d="M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6" />
        </svg>

        <!-- Invoices icon (receipt/file-text) -->
        <svg v-else-if="item.icon === 'invoices'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
          <polyline points="14 2 14 8 20 8" />
          <line x1="16" y1="13" x2="8" y2="13" />
          <line x1="16" y1="17" x2="8" y2="17" />
          <polyline points="10 9 9 9 8 9" />
        </svg>

        <!-- Quotes icon (clipboard) -->
        <svg v-else-if="item.icon === 'quotes'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M16 4h2a2 2 0 012 2v14a2 2 0 01-2 2H6a2 2 0 01-2-2V6a2 2 0 012-2h2" />
          <rect x="8" y="2" width="8" height="4" rx="1" />
          <line x1="8" y1="11" x2="16" y2="11" />
          <line x1="8" y1="15" x2="12" y2="15" />
        </svg>

        <!-- Transactions icon (credit card) -->
        <svg v-else-if="item.icon === 'transactions'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <rect x="1" y="4" width="22" height="16" rx="2" ry="2" />
          <line x1="1" y1="10" x2="23" y2="10" />
        </svg>

        <!-- Label -->
        <span
          class="text-[0.82rem] font-medium whitespace-nowrap transition-opacity duration-200"
          :class="expanded ? 'opacity-100' : 'opacity-0'"
        >
          {{ item.label }}
        </span>
      </RouterLink>
    </nav>
  </div>
</template>
