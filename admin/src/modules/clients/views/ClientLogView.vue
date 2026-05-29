<script setup lang="ts">
/**
 * Displays a paginated, filterable activity log for the current client.
 *
 * Read-only audit trail — shows admin actions with date, description, user, and IP.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useClientLogStore } from '../stores/clientLogStore'
import AppDatePicker from '../../../components/AppDatePicker.vue'
/**
 * Formats an ISO timestamp as a localized date + time string (e.g. "23/05/2026 09:02").
 *
 * @param iso - ISO 8601 timestamp string.
 * @returns Formatted date/time string or em-dash if invalid.
 */
function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '\u2014'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '\u2014'
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
    + ' ' + d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
}

const route = useRoute()
const store = useClientLogStore()

/** Client ID from route params. */
const clientId = computed(() => Number(route.params.id))

/** Current 1-based page. */
const page = ref(1)

/** Items per page. */
const pageSize = 25

/** Whether the filter panel is visible. */
const showFilters = ref(false)

/** Filter: date string (ISO date, e.g. "2026-05-23"). */
const filterDate = ref('')

/** Filter: admin name/email partial match. */
const filterAdmin = ref('')

/** Filter: description partial match. */
const filterDescription = ref('')

/** Filter: IP address partial match. */
const filterIp = ref('')

/** Total number of pages. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / pageSize)))

/**
 * Builds the filter object from current filter inputs.
 *
 * @returns Filter params object.
 */
function buildFilters() {
  return {
    date: filterDate.value || undefined,
    adminSearch: filterAdmin.value || undefined,
    description: filterDescription.value || undefined,
    ipAddress: filterIp.value || undefined,
  }
}

/**
 * Loads logs for the current page and filters.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function load(): Promise<void> {
  await store.fetchLogs(clientId.value, page.value, pageSize, buildFilters())
}

/**
 * Navigates to a specific page.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  load()
}

/**
 * Applies current filters and resets to page 1.
 *
 * @returns void
 */
function applyFilters(): void {
  page.value = 1
  load()
}

/**
 * Clears all filters and reloads.
 *
 * @returns void
 */
function clearFilters(): void {
  filterDate.value = ''
  filterAdmin.value = ''
  filterDescription.value = ''
  filterIp.value = ''
  page.value = 1
  load()
}

onMounted(load)
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between gap-2.5 mb-5">
      <h1 class="text-[0.875rem] font-semibold text-text-primary">Activity Log</h1>
      <button
        type="button"
        class="px-4 py-2 text-[0.82rem] font-medium rounded-lg border border-border text-text-secondary hover:text-text-primary hover:bg-white/[0.04] transition-colors"
        @click="showFilters = !showFilters"
      >
        {{ showFilters ? 'Hide Filters' : 'Filter Log' }}
      </button>
    </div>

    <!-- Filter Panel -->
    <div v-if="showFilters" class="bg-surface-card border border-border rounded-2xl p-5 mb-5 grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div class="flex flex-col gap-1.5">
        <label class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</label>
        <AppDatePicker v-model="filterDate" placeholder="Select date" />
      </div>
      <div class="flex flex-col gap-1.5">
        <label class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Username</label>
        <input
          v-model="filterAdmin"
          type="text"
          placeholder="Any"
          class="px-3 py-2 text-[0.82rem] rounded-lg border border-border bg-surface-card text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-400 transition-colors"
        />
      </div>
      <div class="flex flex-col gap-1.5">
        <label class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</label>
        <input
          v-model="filterDescription"
          type="text"
          class="px-3 py-2 text-[0.82rem] rounded-lg border border-border bg-surface-card text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-400 transition-colors"
        />
      </div>
      <div class="flex flex-col gap-1.5">
        <label class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">IP Address</label>
        <input
          v-model="filterIp"
          type="text"
          class="px-3 py-2 text-[0.82rem] rounded-lg border border-border bg-surface-card text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-400 transition-colors"
        />
      </div>
      <div class="sm:col-span-2 flex gap-3 justify-end">
        <button
          type="button"
          class="px-4 py-2 text-[0.82rem] rounded-lg border border-border text-text-secondary hover:bg-white/[0.04] transition-colors"
          @click="clearFilters"
        >
          Clear
        </button>
        <button
          type="button"
          class="gradient-brand px-5 py-2 text-[0.82rem] font-semibold text-white rounded-lg transition-opacity"
          @click="applyFilters"
        >
          Apply
        </button>
      </div>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
      {{ store.error }}
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.logs.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading activity log...
    </div>

    <template v-else>
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

        <!-- Header row -->
        <div class="hidden sm:grid grid-cols-[160px_1fr_200px_140px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Log Entry</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">User</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">IP Address</span>
        </div>

        <!-- Rows -->
        <div
          v-for="log in store.logs"
          :key="log.id"
          class="grid grid-cols-1 sm:grid-cols-[160px_1fr_200px_140px] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.82rem] text-text-secondary whitespace-nowrap">{{ formatDateTime(log.createdAt) }}</span>
          <span class="text-[0.82rem] text-text-secondary">{{ log.description }}</span>
          <span class="flex flex-col">
            <span class="text-[0.82rem] text-text-primary">{{ log.adminName ?? '—' }}</span>
            <span v-if="log.adminEmail" class="text-[0.72rem] text-text-muted">{{ log.adminEmail }}</span>
          </span>
          <span class="text-[0.82rem] text-text-muted font-mono">{{ log.ipAddress ?? '—' }}</span>
        </div>

        <!-- Empty state -->
        <div v-if="store.logs.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No Records Found</p>
        </div>

        <!-- Pagination -->
        <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
          <span class="text-[0.75rem] text-text-muted">
            {{ store.totalCount }} entr{{ store.totalCount !== 1 ? 'ies' : 'y' }}
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
