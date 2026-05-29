<script setup lang="ts">
/**
 * Support tickets list page -- displays a paginated, filterable table of all
 * support tickets with bulk actions, flagging, search, and status filtering.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useSupportStore } from '../stores/supportStore'
import { formatDate } from '../../../utils/format'
import { TICKET_FILTER_OPTIONS, TICKET_STATUS_STYLES } from '../../../utils/constants'
import AppCheckbox from '../../../components/AppCheckbox.vue'

const store = useSupportStore()

/** IDs of tickets selected via checkbox. */
const selectedIds = ref<number[]>([])

/** Whether the select-all checkbox is checked. */
const selectAll = ref(false)

/** Total number of pages based on current filter. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / store.pageSize)))

/** First record index (1-based) for display. */
const showingFrom = computed(() => store.totalCount === 0 ? 0 : (store.page - 1) * store.pageSize + 1)

/** Last record index (1-based) for display. */
const showingTo = computed(() => Math.min(store.page * store.pageSize, store.totalCount))

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

/**
 * Converts an ISO date string to a relative time string like "6d 1h 17m".
 *
 * @param iso - ISO 8601 timestamp, or null.
 * @returns Relative time string or dash.
 */
function relativeTime(iso: string | null): string {
  if (!iso) return '\u2014'
  const now = Date.now()
  const then = new Date(iso).getTime()
  if (isNaN(then)) return '\u2014'

  let diff = Math.max(0, Math.floor((now - then) / 1000))

  const days = Math.floor(diff / 86400)
  diff %= 86400
  const hours = Math.floor(diff / 3600)
  diff %= 3600
  const minutes = Math.floor(diff / 60)

  const parts: string[] = []
  if (days > 0) parts.push(`${days}d`)
  if (hours > 0) parts.push(`${hours}h`)
  parts.push(`${minutes}m`)

  return parts.join(' ')
}

/**
 * Handles selecting or deselecting all visible tickets.
 */
function handleSelectAll(): void {
  if (selectAll.value) {
    selectedIds.value = store.tickets.map(t => t.id)
  } else {
    selectedIds.value = []
  }
}

/**
 * Handles toggling a single ticket selection.
 *
 * @param id - The ticket identifier.
 */
function handleToggleSelect(id: number): void {
  const idx = selectedIds.value.indexOf(id)
  if (idx === -1) {
    selectedIds.value.push(id)
  } else {
    selectedIds.value.splice(idx, 1)
  }
  selectAll.value = selectedIds.value.length === store.tickets.length && store.tickets.length > 0
}

/**
 * Applies a status filter and reloads tickets.
 *
 * @param value - Filter value from TICKET_FILTER_OPTIONS.
 */
function applyFilter(value: string): void {
  store.statusFilter = value || null
  store.page = 1
  selectedIds.value = []
  selectAll.value = false
  store.fetchAll()
}

/**
 * Toggles the flagged state of a ticket.
 *
 * @param id - The ticket identifier.
 */
async function handleToggleFlag(id: number): Promise<void> {
  const ok = await store.toggleFlag(id)
  if (ok) await store.fetchAll()
}

/**
 * Performs a bulk action on selected tickets.
 *
 * @param action - Bulk action key.
 */
async function handleBulkAction(action: string): Promise<void> {
  if (selectedIds.value.length === 0) return
  const ok = await store.bulkAction(selectedIds.value, action)
  if (ok) {
    selectedIds.value = []
    selectAll.value = false
    await store.fetchAll()
  }
}

/**
 * Navigates to a specific page.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  store.page = p
  selectedIds.value = []
  selectAll.value = false
  store.fetchAll()
}

/** Debounce timer for search input. */
let searchTimer: ReturnType<typeof setTimeout> | undefined

/**
 * Handles search input with debounce.
 */
function handleSearch(): void {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    store.page = 1
    selectedIds.value = []
    selectAll.value = false
    store.fetchAll()
  }, 300)
}

onMounted(() => {
  store.fetchAll()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Support Tickets</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Manage all client support requests</p>
    </div>

    <!-- Status Filter Buttons -->
    <div class="flex flex-wrap gap-2 mb-4">
      <button
        v-for="opt in TICKET_FILTER_OPTIONS"
        :key="opt.value"
        type="button"
        class="px-3.5 py-1.5 text-[0.78rem] font-medium border rounded-[10px] transition-colors"
        :class="store.statusFilter === (opt.value || null)
          ? 'bg-primary-500/10 text-primary-400 border-primary-500/20'
          : 'bg-white/[0.04] text-text-secondary border-border hover:bg-white/[0.06]'"
        @click="applyFilter(opt.value)"
      >
        {{ opt.label }}
      </button>
    </div>

    <!-- Search -->
    <div class="mb-4">
      <input
        v-model="store.search"
        type="text"
        placeholder="Search tickets by subject..."
        class="w-full max-w-md bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
        @input="handleSearch"
      />
    </div>

    <!-- Results metadata -->
    <div class="flex items-center justify-between mb-3 text-[0.75rem] text-text-muted">
      <span>{{ store.totalCount }} Records Found, Showing {{ showingFrom }} to {{ showingTo }}</span>
      <span>Page {{ store.page }} of {{ totalPages }}</span>
    </div>

    <!-- Bulk Actions -->
    <div
      v-if="selectedIds.length > 0"
      class="flex items-center gap-2 mb-3 bg-surface-card border border-border rounded-xl px-4 py-2.5"
    >
      <span class="text-[0.78rem] text-text-muted mr-1">With Selected:</span>
      <button
        type="button"
        class="px-3 py-1 text-[0.75rem] font-medium text-status-yellow bg-status-yellow/10 border border-status-yellow/20 rounded-lg hover:bg-status-yellow/20 transition-colors"
        @click="handleBulkAction('close')"
      >
        Close
      </button>
      <button
        type="button"
        class="px-3 py-1 text-[0.75rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-lg hover:bg-status-red/20 transition-colors"
        @click="handleBulkAction('delete')"
      >
        Delete
      </button>
      <button
        type="button"
        class="px-3 py-1 text-[0.75rem] font-medium text-orange-400 bg-orange-400/10 border border-orange-400/20 rounded-lg hover:bg-orange-400/20 transition-colors"
        @click="handleBulkAction('flag')"
      >
        Flag
      </button>
      <button
        type="button"
        class="px-3 py-1 text-[0.75rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-lg hover:bg-white/[0.06] transition-colors"
        @click="handleBulkAction('unflag')"
      >
        Unflag
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.tickets.length === 0" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading tickets...
    </div>

    <!-- Error -->
    <div v-else-if="store.error && store.tickets.length === 0" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else-if="store.tickets.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Table Header -->
      <div class="hidden sm:grid grid-cols-[40px_36px_1fr_1fr_0.6fr_0.6fr_0.8fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <div class="flex items-center">
          <AppCheckbox :model-value="selectAll" @update:model-value="(v: boolean) => { selectAll = v; handleSelectAll() }" />
        </div>
        <div />
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Subject</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Department</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Requestor</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Last Reply</span>
      </div>

      <!-- Rows -->
      <div
        v-for="ticket in store.tickets"
        :key="ticket.id"
        class="grid grid-cols-1 sm:grid-cols-[40px_36px_1fr_1fr_0.6fr_0.6fr_0.8fr] gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center"
      >
        <!-- Checkbox -->
        <div class="hidden sm:flex items-center">
          <AppCheckbox :model-value="selectedIds.includes(ticket.id)" @update:model-value="handleToggleSelect(ticket.id)" />
        </div>

        <!-- Flag -->
        <div class="hidden sm:flex items-center">
          <button
            type="button"
            class="p-0.5 transition-colors"
            :class="ticket.isFlagged ? 'text-orange-400' : 'text-text-muted/30 hover:text-orange-400/50'"
            @click="handleToggleFlag(ticket.id)"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor" stroke="none">
              <path d="M4 2v20h2v-8l2-2 12 6V4L8 10 6 12V2z" />
            </svg>
          </button>
        </div>

        <!-- Subject -->
        <div>
          <RouterLink
            :to="`/support/tickets/${ticket.id}`"
            class="text-[0.82rem] text-primary-400 hover:underline font-medium"
          >
            #TKT-{{ ticket.id }} - {{ ticket.subject }}
          </RouterLink>
        </div>

        <!-- Department -->
        <div class="text-[0.82rem] text-text-secondary">
          {{ ticket.departmentName ?? '\u2014' }}
        </div>

        <!-- Requestor -->
        <div class="text-[0.82rem] text-text-secondary">
          Client #{{ ticket.clientId }}
        </div>

        <!-- Status -->
        <div>
          <span
            class="inline-block px-2.5 py-0.5 rounded-full text-[0.7rem] font-medium"
            :class="statusClass(ticket.status)"
          >
            {{ statusLabel(ticket.status) }}
          </span>
        </div>

        <!-- Last Reply -->
        <div class="text-[0.82rem] text-text-secondary">
          {{ relativeTime(ticket.lastReplyAt) }}
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3">
      <p class="text-[0.875rem] font-medium text-text-secondary">No tickets found</p>
      <p class="text-[0.78rem] text-text-muted">Try adjusting your filters or search query.</p>
    </div>

    <!-- Pagination -->
    <div v-if="store.tickets.length > 0" class="flex items-center justify-between mt-4">
      <button
        type="button"
        :disabled="store.page <= 1"
        class="px-2.5 py-1 text-[0.75rem] rounded-lg border transition-colors disabled:opacity-40"
        :class="store.page <= 1 ? 'text-text-muted border-border' : 'text-text-secondary border-border hover:bg-white/[0.04]'"
        @click="goToPage(store.page - 1)"
      >
        Prev
      </button>
      <span class="text-[0.75rem] text-text-muted">Page {{ store.page }} of {{ totalPages }}</span>
      <button
        type="button"
        :disabled="store.page >= totalPages"
        class="px-2.5 py-1 text-[0.75rem] rounded-lg border transition-colors disabled:opacity-40"
        :class="store.page >= totalPages ? 'text-text-muted border-border' : 'text-text-secondary border-border hover:bg-white/[0.04]'"
        @click="goToPage(store.page + 1)"
      >
        Next
      </button>
    </div>

  </div>
</template>
