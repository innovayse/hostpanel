<script setup lang="ts">
/**
 * CWP7 module landing page.
 *
 * Shows module info and quick-action buttons to create servers or products
 * linked to the CWP7 provisioning module.
 * Lists existing CWP7 servers with their status.
 */
import { computed, onMounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useServersStore } from '../../servers/stores/serversStore'
import type { ServerDto } from '../../servers/types/server.types'

const router = useRouter()
const serversStore = useServersStore()

onMounted(() => {
  serversStore.fetchServers()
})

/** CWP7 servers only. */
const cwp7Servers = computed<ServerDto[]>(() =>
  serversStore.servers.filter(s => s.module === 'Cwp7')
)

/**
 * Navigates to the servers page with CWP7 module pre-selected.
 */
function goToCreateServer(): void {
  router.push({ path: '/servers', query: { create: 'Cwp7' } })
}

/**
 * Navigates to the product creation page.
 */
function goToCreateProduct(): void {
  router.push({ path: '/settings/products', query: { create: 'true', module: 'Cwp7' } })
}

</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full flex flex-col gap-6">

    <!-- Breadcrumb -->
    <div class="flex items-center gap-2 text-[0.78rem]">
      <RouterLink
        to="/integrations"
        class="text-text-secondary hover:text-primary-400 transition-colors no-underline"
      >
        Integrations
      </RouterLink>
      <svg class="w-3.5 h-3.5 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <polyline points="9 18 15 12 9 6"/>
      </svg>
      <span class="text-text-primary font-medium">CWP7</span>
    </div>

    <!-- Module header card -->
    <div class="bg-surface-card border border-border rounded-2xl p-6" style="border-left: 3px solid #0369a1">
      <div class="flex items-start gap-5">
        <div class="w-14 h-14 rounded-xl bg-sky-800 flex items-center justify-center shrink-0">
          <img src="/integrations/cwp7.svg" alt="CWP7" class="w-8 h-8 integration-logo" />
        </div>
        <div class="flex-1 min-w-0">
          <h1 class="font-display text-xl font-bold text-text-primary leading-tight mb-1">CWP7</h1>
          <p class="text-[0.82rem] text-text-muted mb-4">Control Web Panel 7 — Provisioning provider for managing hosting accounts, packages, and server resources.</p>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 text-[0.78rem]">
            <div>
              <span class="text-text-muted">Category</span>
              <p class="text-text-primary font-medium">Hosting / Provisioning</p>
            </div>
            <div>
              <span class="text-text-muted">Module Type</span>
              <p class="text-text-primary font-medium">Server Module</p>
            </div>
            <div>
              <span class="text-text-muted">Developer</span>
              <p class="text-text-primary font-medium">Innovayse</p>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Action buttons -->
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <button
        class="bg-surface-card border border-border rounded-2xl p-5 flex items-center gap-4 transition-all duration-200 hover:border-primary-500/40 hover:shadow-lg hover:shadow-primary-500/5 cursor-pointer text-left group"
        @click="goToCreateServer"
      >
        <div class="w-11 h-11 rounded-xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center shrink-0">
          <svg class="w-5 h-5 text-primary-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="2" y="2" width="20" height="8" rx="2"/>
            <rect x="2" y="14" width="20" height="8" rx="2"/>
            <line x1="6" y1="6" x2="6.01" y2="6"/>
            <line x1="6" y1="18" x2="6.01" y2="18"/>
          </svg>
        </div>
        <div>
          <h3 class="text-[0.9rem] font-semibold text-text-primary group-hover:text-primary-400 transition-colors">Create New Server</h3>
          <p class="text-[0.75rem] text-text-muted">Add a CWP7 server with hostname, credentials, and nameservers</p>
        </div>
      </button>

      <button
        class="bg-surface-card border border-border rounded-2xl p-5 flex items-center gap-4 transition-all duration-200 hover:border-primary-500/40 hover:shadow-lg hover:shadow-primary-500/5 cursor-pointer text-left group"
        @click="goToCreateProduct"
      >
        <div class="w-11 h-11 rounded-xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center shrink-0">
          <svg class="w-5 h-5 text-primary-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/>
            <polyline points="3.27 6.96 12 12.01 20.73 6.96"/>
            <line x1="12" y1="22.08" x2="12" y2="12"/>
          </svg>
        </div>
        <div>
          <h3 class="text-[0.9rem] font-semibold text-text-primary group-hover:text-primary-400 transition-colors">Create New Product</h3>
          <p class="text-[0.75rem] text-text-muted">Create a hosting plan linked to a CWP7 server</p>
        </div>
      </button>
    </div>

    <!-- CWP7 Servers list -->
    <div>
      <div class="flex items-center justify-between mb-3">
        <h2 class="font-display font-semibold text-[0.95rem] text-text-primary">CWP7 Servers</h2>
        <span class="text-[0.72rem] text-text-muted">{{ cwp7Servers.length }} server{{ cwp7Servers.length !== 1 ? 's' : '' }}</span>
      </div>

      <!-- Empty state -->
      <div
        v-if="!serversStore.loading && cwp7Servers.length === 0"
        class="bg-surface-card border border-border rounded-2xl p-8 text-center"
      >
        <p class="text-[0.85rem] text-text-muted mb-3">No CWP7 servers configured yet.</p>
        <button
          class="text-[0.82rem] font-medium text-primary-400 hover:text-primary-300 transition-colors"
          @click="goToCreateServer"
        >
          Add your first server →
        </button>
      </div>

      <!-- Loading -->
      <div v-else-if="serversStore.loading" class="flex items-center gap-3 text-text-secondary text-sm">
        <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
        Loading servers...
      </div>

      <!-- Server cards -->
      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <RouterLink
          v-for="server in cwp7Servers"
          :key="server.id"
          :to="`/servers`"
          class="bg-surface-card border border-border rounded-xl p-4 transition-all duration-200 hover:border-primary-500/40 no-underline group"
        >
          <div class="flex items-center gap-3 mb-3">
            <span
              class="w-2.5 h-2.5 rounded-full shrink-0"
              :class="server.isOnline === true ? 'bg-status-green animate-pulse' : server.isOnline === false ? 'bg-status-red' : 'bg-text-muted'"
            />
            <h3 class="text-[0.85rem] font-semibold text-text-primary truncate group-hover:text-primary-400 transition-colors">
              {{ server.name }}
            </h3>
            <span
              v-if="server.isDisabled"
              class="ml-auto text-[0.6rem] font-semibold text-status-red bg-status-red/10 border border-status-red/20 rounded-full px-1.5 py-px"
            >
              Disabled
            </span>
          </div>
          <div class="text-[0.75rem] text-text-muted space-y-1">
            <p>{{ server.hostname }}</p>
            <p v-if="server.ipAddress">{{ server.ipAddress }}</p>
            <p v-if="server.accountsCount !== null">{{ server.accountsCount }} / {{ server.maxAccounts ?? '∞' }} accounts</p>
          </div>
        </RouterLink>
      </div>
    </div>

  </div>
</template>

<style scoped>
.integration-logo {
  filter: brightness(0) invert(1);
}
</style>
