<script setup lang="ts">
/**
 * Admin dashboard — displays high-level system statistics.
 */
import { onMounted } from 'vue'
import { useDashboardStore } from '../stores/dashboardStore'

const store = useDashboardStore()

onMounted(store.fetchStats)

/** Dashboard stat card definitions. */
const statConfig = [
  {
    key: 'totalRevenue' as const,
    label: 'Total Revenue',
    format: (v: number) => `$${v.toLocaleString()}`,
    color: 'text-text-primary',
    glow: true,
    icon: 'M12 2v20M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6',
  },
  {
    key: 'monthlyRevenue' as const,
    label: 'Monthly Revenue',
    format: (v: number) => `$${v.toLocaleString()}`,
    color: 'text-text-primary',
    glow: false,
    icon: 'M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z',
  },
  {
    key: 'activeServices' as const,
    label: 'Active Services',
    format: (v: number) => v.toString(),
    color: 'text-status-green',
    glow: false,
    icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z',
  },
  {
    key: 'overdueInvoices' as const,
    label: 'Overdue Invoices',
    format: (v: number) => v.toString(),
    color: 'text-status-red',
    glow: false,
    icon: 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z',
  },
  {
    key: 'openTickets' as const,
    label: 'Open Tickets',
    format: (v: number) => v.toString(),
    color: 'text-status-yellow',
    glow: false,
    icon: 'M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-3 3v-3z',
  },
  {
    key: 'totalClients' as const,
    label: 'Total Clients',
    format: (v: number) => v.toString(),
    color: 'text-text-primary',
    glow: false,
    icon: 'M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2M9 7a4 4 0 100 8 4 4 0 000-8zm14 4a4 4 0 10-4-4 4 4 0 004 4z',
  },
]
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 max-w-5xl w-full">

    <!-- Page header -->
    <div class="flex items-start justify-between mb-8">
      <div>
        <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">Dashboard</h1>
        <p class="text-sm text-text-secondary">Welcome back — here's what's happening</p>
      </div>

      <!-- Live badge -->
      <div class="flex items-center gap-2 text-[0.68rem] font-semibold uppercase tracking-widest text-status-green bg-status-green/8 border border-status-green/20 rounded-full px-3 py-1.5">
        <span class="w-1.5 h-1.5 rounded-full bg-status-green animate-pulse shrink-0"/>
        Live
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading metrics…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Stats grid -->
    <div v-else-if="store.stats" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <div
        v-for="stat in statConfig"
        :key="stat.key"
        class="relative overflow-hidden bg-surface-card border border-border rounded-2xl p-6 transition-all duration-200 hover:border-primary-500/20 hover:-translate-y-0.5"
        :class="stat.glow ? 'border-primary-500/15 bg-gradient-to-br from-primary-500/5 to-secondary-500/5' : ''"
      >
        <!-- Glow overlay for featured card -->
        <div
          v-if="stat.glow"
          class="pointer-events-none absolute inset-0 opacity-10"
          style="background: radial-gradient(circle at 100% 0%, #0ea5e9, transparent 60%);"
        />

        <p class="text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-3">{{ stat.label }}</p>

        <p class="font-display text-[2rem] font-bold tracking-tight leading-none" :class="stat.color">
          {{ stat.format(store.stats[stat.key]) }}
        </p>

        <!-- Icon chip -->
        <div class="absolute bottom-5 right-5 flex items-center justify-center w-9 h-9 rounded-[9px] bg-white/[0.04] border border-white/[0.06]">
          <svg class="w-4 h-4 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
            <path :d="stat.icon"/>
          </svg>
        </div>
      </div>
    </div>

  </div>
</template>
