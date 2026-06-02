<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const expanded = ref(false)

const navItems = [
  { label: 'Daily Performance', to: '/reports/daily-performance', icon: 'chart-area' },
  { label: 'Annual Income', to: '/reports/annual-income', icon: 'bar-chart' },
  { label: 'Income Forecast', to: '/reports/income-forecast', icon: 'trending-up' },
  { label: 'Aging Invoices', to: '/reports/aging-invoices', icon: 'clock' },
  { label: 'Monthly Transactions', to: '/reports/monthly-transactions', icon: 'credit-card' },
  { label: 'Income by Product', to: '/reports/income-by-product', icon: 'package' },
  { label: 'Top 10 Clients', to: '/reports/top-clients', icon: 'star' },
  { label: 'Sales Tax', to: '/reports/sales-tax', icon: 'percent' },
  { label: 'New Customers', to: '/reports/new-customers', icon: 'user-plus' },
  { label: 'Clients by Country', to: '/reports/clients-by-country', icon: 'globe' },
  { label: 'Client Sources', to: '/reports/client-sources', icon: 'pie-chart' },
  { label: 'Monthly Orders', to: '/reports/monthly-orders', icon: 'shopping-cart' },
  { label: 'Services', to: '/reports/services', icon: 'box' },
  { label: 'Domains', to: '/reports/domains', icon: 'link' },
  { label: 'Support Tickets', to: '/reports/support-tickets', icon: 'message-square' },
  { label: 'Promotions', to: '/reports/promotions', icon: 'tag' },
]

function isActive(to: string): boolean {
  return route.path === to
}
</script>

<template>
  <div
    class="flex flex-col bg-surface-card border-r border-border transition-all duration-200 ease-in-out overflow-hidden shrink-0"
    :class="expanded ? 'w-[200px]' : 'w-[48px]'"
    @mouseenter="expanded = true"
    @mouseleave="expanded = false"
  >
    <nav class="flex flex-col gap-0.5 pt-2">
      <RouterLink
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        class="relative flex items-center gap-2.5 px-3 py-2.5 transition-all duration-150 no-underline"
        :class="isActive(item.to)
          ? 'text-primary-400 bg-primary-500/8'
          : 'text-text-muted hover:text-text-secondary hover:bg-white/[0.04]'"
      >
        <span v-if="isActive(item.to)" class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand" />

        <!-- chart-area -->
        <svg v-if="item.icon === 'chart-area'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>
        </svg>
        <!-- bar-chart -->
        <svg v-else-if="item.icon === 'bar-chart'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/><line x1="2" y1="20" x2="22" y2="20"/>
        </svg>
        <!-- trending-up -->
        <svg v-else-if="item.icon === 'trending-up'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="23 6 13.5 15.5 8.5 10.5 1 18"/><polyline points="17 6 23 6 23 12"/>
        </svg>
        <!-- clock -->
        <svg v-else-if="item.icon === 'clock'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
        </svg>
        <!-- credit-card -->
        <svg v-else-if="item.icon === 'credit-card'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <rect x="1" y="4" width="22" height="16" rx="2"/><line x1="1" y1="10" x2="23" y2="10"/>
        </svg>
        <!-- package -->
        <svg v-else-if="item.icon === 'package'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 003 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z"/><polyline points="3.27 6.96 12 12.01 20.73 6.96"/><line x1="12" y1="22.08" x2="12" y2="12"/>
        </svg>
        <!-- star -->
        <svg v-else-if="item.icon === 'star'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/>
        </svg>
        <!-- percent -->
        <svg v-else-if="item.icon === 'percent'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="19" y1="5" x2="5" y2="19"/><circle cx="6.5" cy="6.5" r="2.5"/><circle cx="17.5" cy="17.5" r="2.5"/>
        </svg>
        <!-- user-plus -->
        <svg v-else-if="item.icon === 'user-plus'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M16 21v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2"/><circle cx="9" cy="7" r="4"/><line x1="19" y1="8" x2="19" y2="14"/><line x1="22" y1="11" x2="16" y2="11"/>
        </svg>
        <!-- globe -->
        <svg v-else-if="item.icon === 'globe'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z"/>
        </svg>
        <!-- pie-chart -->
        <svg v-else-if="item.icon === 'pie-chart'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21.21 15.89A10 10 0 118 2.83"/><path d="M22 12A10 10 0 0012 2v10z"/>
        </svg>
        <!-- shopping-cart -->
        <svg v-else-if="item.icon === 'shopping-cart'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/><path d="M1 1h4l2.68 13.39a2 2 0 001.99 1.61h9.72a2 2 0 001.99-1.61L23 6H6"/>
        </svg>
        <!-- box -->
        <svg v-else-if="item.icon === 'box'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 003 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z"/>
        </svg>
        <!-- link -->
        <svg v-else-if="item.icon === 'link'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M10 13a5 5 0 007.54.54l3-3a5 5 0 00-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 00-7.54-.54l-3 3a5 5 0 007.07 7.07l1.71-1.71"/>
        </svg>
        <!-- message-square -->
        <svg v-else-if="item.icon === 'message-square'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/>
        </svg>
        <!-- tag -->
        <svg v-else-if="item.icon === 'tag'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M20.59 13.41l-7.17 7.17a2 2 0 01-2.83 0L2 12V2h10l8.59 8.59a2 2 0 010 2.82z"/><line x1="7" y1="7" x2="7.01" y2="7"/>
        </svg>

        <span
          class="text-[0.78rem] font-medium whitespace-nowrap transition-opacity duration-200"
          :class="expanded ? 'opacity-100' : 'opacity-0'"
        >
          {{ item.label }}
        </span>
      </RouterLink>
    </nav>
  </div>
</template>
