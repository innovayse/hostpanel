<script setup lang="ts">
/**
 * Client tickets list page -- displays support tickets with summary cards,
 * search, table, and pagination. Follows the same layout as ClientTransactionsView.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useClientTicketStore } from '../stores/clientTicketStore'
import { formatDate } from '../../../utils/format'
import { TICKET_STATUS_STYLES } from '../../../utils/constants'

const route = useRoute()
const store = useClientTicketStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(20)

/** Search term for subject filtering. */
const search = ref('')

/** Debounce timer for search input. */
let searchTimer: ReturnType<typeof setTimeout> | undefined

/** Total pages based on totalCount and pageSize. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / pageSize.value)))

/**
 * Fetches tickets for the current client with current pagination and search.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function fetchTickets(): Promise<void> {
  await store.fetchClientTickets(clientId.value, page.value, pageSize.value, search.value || undefined)
}

/**
 * Handles debounced search input.
 */
function onSearchInput(): void {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    page.value = 1
    fetchTickets()
  }, 300)
}

/**
 * Navigates to a specific page and re-fetches.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  fetchTickets()
}

/**
 * Returns Tailwind classes for a ticket status badge.
 *
 * @param status - The ticket status string.
 * @returns Tailwind class string.
 */
function statusClass(status: string): string {
  return TICKET_STATUS_STYLES[status] ?? 'text-text-muted bg-white/[0.04] border border-border'
}

/**
 * Returns a human-readable label for a status value.
 *
 * @param status - The ticket status string.
 * @returns Display label.
 */
function statusLabel(status: string): string {
  if (status === 'AwaitingReply') return 'Awaiting Reply'
  return status
}

onMounted(() => {
  fetchTickets()
  store.fetchStats(clientId.value)
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between gap-2.5 mb-5">
      <h1 class="text-[0.875rem] font-semibold text-text-primary">Tickets</h1>
      <RouterLink
        :to="`/clients/${clientId}/tickets/new`"
        class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity no-underline"
      >
        Open New Ticket +
      </RouterLink>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
      {{ store.error }}
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.tickets.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading tickets...
    </div>

    <template v-else>

      <!-- Summary cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <!-- Opened This Month -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 border-l-4 border-l-primary-500">
          <p class="text-xl font-bold text-text-primary">{{ store.stats.openedThisMonth }}</p>
          <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-1">Opened This Month</p>
        </div>

        <!-- Opened Last Month -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 border-l-4 border-l-primary-400">
          <p class="text-xl font-bold text-text-primary">{{ store.stats.openedLastMonth }}</p>
          <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-1">Opened Last Month</p>
        </div>

        <!-- Opened This Year -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 border-l-4 border-l-status-green">
          <p class="text-xl font-bold text-text-primary">{{ store.stats.openedThisYear }}</p>
          <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-1">Opened This Year</p>
        </div>

        <!-- Opened Last Year -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 border-l-4 border-l-text-muted">
          <p class="text-xl font-bold text-text-primary">{{ store.stats.openedLastYear }}</p>
          <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-1">Opened Last Year</p>
        </div>
      </div>

      <!-- Search -->
      <div class="flex items-center gap-3 mb-4">
        <label class="text-[0.78rem] text-text-muted whitespace-nowrap">Search Subject:</label>
        <input
          v-model="search"
          type="text"
          placeholder="Search..."
          class="w-full max-w-xs bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          @input="onSearchInput"
        />
      </div>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- Header row -->
        <div class="hidden sm:grid grid-cols-[0.8fr_0.8fr_1.5fr_0.7fr_0.8fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date Opened</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Department</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Subject</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Last Reply</span>
        </div>

        <!-- Rows -->
        <div
          v-for="ticket in store.tickets"
          :key="ticket.id"
          class="grid grid-cols-1 sm:grid-cols-[0.8fr_0.8fr_1.5fr_0.7fr_0.8fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.82rem] text-text-secondary">{{ formatDate(ticket.createdAt) }}</span>
          <span class="text-[0.82rem] text-text-secondary">{{ ticket.departmentName ?? '\u2014' }}</span>
          <RouterLink
            :to="`/clients/${clientId}/tickets/${ticket.id}`"
            class="text-[0.82rem] text-primary-400 hover:underline truncate"
          >
            {{ ticket.subject }}
          </RouterLink>
          <span>
            <span
              class="inline-block px-2.5 py-0.5 rounded-full text-[0.7rem] font-medium"
              :class="statusClass(ticket.status)"
            >
              {{ statusLabel(ticket.status) }}
            </span>
          </span>
          <span class="text-[0.82rem] text-text-secondary">{{ formatDate(ticket.lastReplyAt) }}</span>
        </div>

        <!-- Empty state -->
        <div v-if="store.tickets.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No data available in table</p>
        </div>

        <!-- Pagination -->
        <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
          <span class="text-[0.75rem] text-text-muted">
            {{ store.totalCount }} ticket{{ store.totalCount !== 1 ? 's' : '' }}
          </span>
          <div class="flex items-center gap-1">
            <button
              :disabled="page <= 1"
              class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
              @click="goToPage(page - 1)"
            >
              Prev
            </button>
            <span class="text-[0.75rem] text-text-muted px-2">{{ page }} / {{ totalPages }}</span>
            <button
              :disabled="page >= totalPages"
              class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
              @click="goToPage(page + 1)"
            >
              Next
            </button>
          </div>
        </div>
      </div>

    </template>
  </div>
</template>
