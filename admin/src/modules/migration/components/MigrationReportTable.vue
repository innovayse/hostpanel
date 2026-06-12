<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { useMigrationStore } from '../stores/migrationStore'
import AppSelect from '../../../components/AppSelect.vue'
import type { MigrationLogEntry } from '../types/migration.types'

const props = defineProps<{ jobId: number }>()

const store = useMigrationStore()

const items = ref<MigrationLogEntry[]>([])
const totalCount = ref(0)
const loading = ref(false)

const filterAction = ref('')
const filterEntityType = ref('')
const page = ref(1)
const pageSize = 20

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))

const actionOptions = [
  { value: '', label: 'All statuses' },
  { value: 'Imported', label: 'Imported' },
  { value: 'Skipped', label: 'Skipped' },
  { value: 'Failed', label: 'Failed' },
]

const entityTypeOptions = [
  { value: '', label: 'All types' },
  { value: 'Clients', label: 'Clients' },
  { value: 'Invoices', label: 'Invoices' },
  { value: 'Services', label: 'Services' },
  { value: 'Domains', label: 'Domains' },
  { value: 'Tickets', label: 'Tickets' },
  { value: 'Products', label: 'Products' },
  { value: 'Orders', label: 'Orders' },
  { value: 'Transactions', label: 'Transactions' },
  { value: 'Quotes', label: 'Quotes' },
  { value: 'Knowledgebase', label: 'Knowledgebase' },
  { value: 'Contacts', label: 'Contacts' },
  { value: 'TicketReplies', label: 'Ticket Replies' },
]

async function load() {
  loading.value = true
  try {
    const result = await store.fetchLogs(props.jobId, {
      action: filterAction.value || undefined,
      entityType: filterEntityType.value || undefined,
      page: page.value,
      pageSize,
    })
    items.value = result.items
    totalCount.value = result.totalCount
  } finally {
    loading.value = false
  }
}

watch([filterAction, filterEntityType], () => {
  page.value = 1
  load()
}, { immediate: true })

watch(page, load)

function entityLabel(type: string) {
  return {
    Clients: 'Client', Invoices: 'Invoice', Services: 'Service',
    Domains: 'Domain', Tickets: 'Ticket', Products: 'Product',
    Orders: 'Order', Transactions: 'Transaction', Quotes: 'Quote',
    Knowledgebase: 'KB Article',
    Contacts: 'Contact',
    TicketReplies: 'Reply',
  }[type] ?? type
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

    <!-- Header + filters -->
    <div class="flex flex-wrap items-center gap-3 px-5 py-4 border-b border-border">
      <h3 class="text-[0.88rem] font-semibold text-text-primary flex-1 min-w-0">Import Report</h3>

      <span class="text-[0.72rem] font-semibold px-2 py-0.5 rounded-full bg-status-green/10 text-status-green border border-status-green/20">
        {{ totalCount }} entries
      </span>

      <div class="w-36">
        <AppSelect v-model="filterEntityType" :options="entityTypeOptions" />
      </div>

      <div class="w-36">
        <AppSelect v-model="filterAction" :options="actionOptions" />
      </div>
    </div>

    <!-- Table -->
    <div class="overflow-x-auto">
      <table class="w-full text-[0.78rem]">
        <thead>
          <tr class="border-b border-border">
            <th class="text-left px-5 py-3 text-[0.7rem] font-semibold text-text-muted uppercase tracking-wider w-24">Type</th>
            <th class="text-left px-4 py-3 text-[0.7rem] font-semibold text-text-muted uppercase tracking-wider">Identifier</th>
            <th class="text-left px-4 py-3 text-[0.7rem] font-semibold text-text-muted uppercase tracking-wider w-28">Status</th>
            <th class="text-left px-4 py-3 text-[0.7rem] font-semibold text-text-muted uppercase tracking-wider">Reason</th>
            <th class="text-left px-4 py-3 text-[0.7rem] font-semibold text-text-muted uppercase tracking-wider w-24">Time</th>
          </tr>
        </thead>
        <tbody>
          <!-- Loading -->
          <tr v-if="loading && items.length === 0">
            <td colspan="5" class="text-center py-10">
              <span class="inline-block w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
            </td>
          </tr>

          <!-- Empty -->
          <tr v-else-if="items.length === 0">
            <td colspan="5" class="text-center py-10 text-text-muted text-[0.82rem]">No records found</td>
          </tr>

          <!-- Rows -->
          <tr
            v-for="entry in items"
            :key="entry.id"
            class="border-b border-border/50 hover:bg-white/[0.02] transition-colors"
          >
            <td class="px-5 py-2.5 text-[0.72rem] font-medium text-text-muted">
              {{ entityLabel(entry.entityType) }}
            </td>

            <td class="px-4 py-2.5 font-mono text-text-secondary">
              {{ entry.identifier }}
            </td>

            <td class="px-4 py-2.5">
              <span
                class="inline-flex items-center gap-1 text-[0.68rem] font-semibold px-2 py-0.5 rounded-full border"
                :class="{
                  'text-status-green bg-status-green/10 border-status-green/20': entry.action === 'Imported',
                  'text-amber-400 bg-amber-400/10 border-amber-400/20': entry.action === 'Skipped',
                  'text-status-red bg-status-red/10 border-status-red/20': entry.action === 'Failed',
                }"
              >
                <span
                  class="w-1.5 h-1.5 rounded-full"
                  :class="{
                    'bg-status-green': entry.action === 'Imported',
                    'bg-amber-400': entry.action === 'Skipped',
                    'bg-status-red': entry.action === 'Failed',
                  }"
                />
                {{ entry.action }}
              </span>
            </td>

            <td class="px-4 py-2.5 text-text-muted">
              {{ entry.reason ?? '—' }}
            </td>

            <td class="px-4 py-2.5 text-text-muted font-mono text-[0.72rem]">
              {{ formatTime(entry.createdAt) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border">
      <span class="text-[0.72rem] text-text-muted">
        Page {{ page }} of {{ totalPages }} ({{ totalCount }} entries)
      </span>
      <div class="flex gap-2">
        <button
          class="px-3 py-1 rounded-lg text-[0.75rem] font-medium border border-border text-text-secondary hover:border-primary-500/40 transition disabled:opacity-40"
          :disabled="page <= 1"
          @click="page--"
        >Prev</button>
        <button
          class="px-3 py-1 rounded-lg text-[0.75rem] font-medium border border-border text-text-secondary hover:border-primary-500/40 transition disabled:opacity-40"
          :disabled="page >= totalPages"
          @click="page++"
        >Next</button>
      </div>
    </div>

  </div>
</template>
