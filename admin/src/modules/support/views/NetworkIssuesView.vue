<script setup lang="ts">
/**
 * Network issues list view -- displays a paginated, filterable list of
 * network issues with status filter links and CRUD actions.
 */
import { ref, computed, onMounted } from 'vue'
import { useApi } from '../../../composables/useApi'
import { formatDate } from '../../../utils/format'
import ConfirmModal from '../../../components/ConfirmModal.vue'

/** Shape of a network issue returned by the API. */
interface NetworkIssue {
  /** Unique issue identifier. */
  id: number
  /** Issue title. */
  title: string
  /** Issue type (e.g. Server, Other). */
  type: string
  /** Priority level. */
  priority: string
  /** Current status. */
  status: string
  /** ISO 8601 start date. */
  startDate: string
  /** ISO 8601 end date, or null if ongoing. */
  endDate: string | null
}

/** Paginated response wrapper. */
interface PaginatedResponse {
  /** Array of network issue records. */
  items: NetworkIssue[]
  /** Total number of matching records. */
  totalCount: number
}

/** Status filter definition. */
interface StatusFilter {
  /** Display label. */
  label: string
  /** API filter value. */
  value: string
  /** Sub-heading label when active. */
  heading: string
}

const { request } = useApi()

/** Available status filters. */
const statusFilters: StatusFilter[] = [
  { label: 'Open', value: 'open', heading: 'Open Issues' },
  { label: 'Scheduled', value: 'Scheduled', heading: 'Scheduled Issues' },
  { label: 'Resolved', value: 'Resolved', heading: 'Resolved Issues' },
]

/** Number of records per page. */
const pageSize = 20

/** All fetched network issues. */
const issues = ref<NetworkIssue[]>([])

/** Total record count from server. */
const totalCount = ref(0)

/** Current page (1-based). */
const page = ref(1)

/** Active status filter value. */
const activeFilter = ref('open')

/** Whether a fetch is in flight. */
const loading = ref(false)

/** Error message from the last operation. */
const error = ref<string | null>(null)

/** ID of issue pending deletion. */
const deleteTarget = ref<number | null>(null)

/** Whether a delete request is in flight. */
const deleting = ref(false)

/** Total pages. */
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))

/** Active filter object for display. */
const activeFilterObj = computed(() =>
  statusFilters.find(f => f.value === activeFilter.value) ?? statusFilters[0]
)

/**
 * Returns Tailwind classes for a priority badge.
 *
 * @param priority - The priority string.
 * @returns Tailwind class string.
 */
function priorityClass(priority: string): string {
  const map: Record<string, string> = {
    Critical: 'text-status-red bg-status-red/10 border border-status-red/20',
    High: 'text-orange-400 bg-orange-400/10 border border-orange-400/20',
    Medium: 'text-status-yellow bg-status-yellow/10 border border-status-yellow/20',
    Low: 'text-text-muted bg-white/[0.04] border border-border',
  }
  return map[priority] ?? 'text-text-muted bg-white/[0.04] border border-border'
}

/**
 * Returns Tailwind classes for a status badge.
 *
 * @param status - The status string.
 * @returns Tailwind class string.
 */
function statusClass(status: string): string {
  const map: Record<string, string> = {
    Reported: 'text-status-yellow bg-status-yellow/10 border border-status-yellow/20',
    Investigating: 'text-orange-400 bg-orange-400/10 border border-orange-400/20',
    Scheduled: 'text-primary-400 bg-primary-500/10 border border-primary-500/20',
    Resolved: 'text-green-400 bg-green-400/10 border border-green-400/20',
  }
  return map[status] ?? 'text-text-muted bg-white/[0.04] border border-border'
}

/**
 * Formats an ISO date string as DD/MM/YYYY HH:MM.
 *
 * @param iso - ISO 8601 date string or null.
 * @returns Formatted date-time string or dash.
 */
function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '\u2014'
  const d = new Date(iso)
  if (isNaN(d.getTime())) return '\u2014'
  const day = String(d.getDate()).padStart(2, '0')
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const year = d.getFullYear()
  const hours = String(d.getHours()).padStart(2, '0')
  const minutes = String(d.getMinutes()).padStart(2, '0')
  return `${day}/${month}/${year} ${hours}:${minutes}`
}

/**
 * Fetches network issues for the current page and filter.
 */
async function fetchIssues(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const data = await request<PaginatedResponse>(
      `/network-issues?page=${page.value}&pageSize=${pageSize}&status=${activeFilter.value}`
    )
    issues.value = data.items
    totalCount.value = data.totalCount
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load network issues'
  } finally {
    loading.value = false
  }
}

/**
 * Applies a status filter and reloads.
 *
 * @param value - Filter value.
 */
function applyFilter(value: string): void {
  activeFilter.value = value
  page.value = 1
  fetchIssues()
}

/**
 * Navigates to a specific page.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  fetchIssues()
}

/**
 * Opens the delete confirmation modal.
 *
 * @param id - Network issue ID to delete.
 */
function confirmDelete(id: number): void {
  deleteTarget.value = id
}

/**
 * Executes the deletion of the target network issue.
 */
async function handleDelete(): Promise<void> {
  if (deleteTarget.value === null) return
  deleting.value = true
  try {
    await request(`/network-issues/${deleteTarget.value}`, { method: 'DELETE' })
    deleteTarget.value = null
    await fetchIssues()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete network issue'
  } finally {
    deleting.value = false
  }
}

onMounted(() => {
  fetchIssues()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Network Issues</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Manage network status and incident reports</p>
    </div>

    <!-- Filter links -->
    <div class="flex flex-wrap items-center gap-1 mb-4 text-[0.82rem]">
      <span class="text-text-muted mr-1">Options:</span>
      <template v-for="(filter, idx) in statusFilters" :key="filter.value">
        <button
          type="button"
          class="transition-colors"
          :class="activeFilter === filter.value
            ? 'text-primary-400 font-semibold'
            : 'text-text-secondary hover:text-text-primary'"
          @click="applyFilter(filter.value)"
        >
          {{ filter.label }}
        </button>
        <span v-if="idx < statusFilters.length - 1" class="text-text-muted">|</span>
      </template>
      <span class="text-text-muted">|</span>
      <RouterLink
        to="/support/network-issues/new"
        class="text-green-400 hover:text-green-300 font-medium transition-colors"
      >
        (+) Create New
      </RouterLink>
    </div>

    <!-- Sub-heading -->
    <h2 class="text-[0.88rem] font-semibold text-text-primary mb-3">{{ activeFilterObj.heading }}</h2>

    <!-- Results metadata -->
    <div class="flex items-center justify-between mb-3 text-[0.75rem] text-text-muted">
      <span>{{ totalCount }} Records Found</span>
      <span>Page {{ page }} of {{ totalPages }}</span>
    </div>

    <!-- Loading -->
    <div v-if="loading && issues.length === 0" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading network issues...
    </div>

    <!-- Error -->
    <div v-else-if="error && issues.length === 0" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ error }}
    </div>

    <!-- Table -->
    <div v-else-if="issues.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Table Header -->
      <div class="hidden sm:grid grid-cols-[1.5fr_0.6fr_0.6fr_0.6fr_1fr_1fr_40px_40px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Title</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Type</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Priority</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Start Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">End Date</span>
        <span />
        <span />
      </div>

      <!-- Rows -->
      <div
        v-for="issue in issues"
        :key="issue.id"
        class="grid grid-cols-1 sm:grid-cols-[1.5fr_0.6fr_0.6fr_0.6fr_1fr_1fr_40px_40px] gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center"
      >
        <!-- Title -->
        <div class="text-[0.82rem] text-text-secondary font-medium">
          {{ issue.title }}
        </div>

        <!-- Type -->
        <div class="text-[0.82rem] text-text-secondary">
          {{ issue.type }}
        </div>

        <!-- Priority -->
        <div>
          <span
            class="inline-block px-2.5 py-0.5 rounded-full text-[0.7rem] font-medium"
            :class="priorityClass(issue.priority)"
          >
            {{ issue.priority }}
          </span>
        </div>

        <!-- Status -->
        <div>
          <span
            class="inline-block px-2.5 py-0.5 rounded-full text-[0.7rem] font-medium"
            :class="statusClass(issue.status)"
          >
            {{ issue.status }}
          </span>
        </div>

        <!-- Start Date -->
        <div class="text-[0.82rem] text-text-secondary">
          {{ formatDateTime(issue.startDate) }}
        </div>

        <!-- End Date -->
        <div class="text-[0.82rem] text-text-secondary">
          {{ formatDateTime(issue.endDate) }}
        </div>

        <!-- Edit -->
        <div class="flex items-center justify-center">
          <RouterLink
            :to="`/support/network-issues/${issue.id}/edit`"
            class="text-text-muted hover:text-primary-400 transition-colors"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
              <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
            </svg>
          </RouterLink>
        </div>

        <!-- Delete -->
        <div class="flex items-center justify-center">
          <button
            type="button"
            class="text-text-muted hover:text-status-red transition-colors"
            @click="confirmDelete(issue.id)"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="3 6 5 6 21 6" />
              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
            </svg>
          </button>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3">
      <svg class="w-10 h-10 text-text-muted/40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
        <circle cx="12" cy="12" r="10" />
        <line x1="12" y1="8" x2="12" y2="12" />
        <line x1="12" y1="16" x2="12.01" y2="16" />
      </svg>
      <p class="text-[0.875rem] font-medium text-text-secondary">No Records Found</p>
      <p class="text-[0.78rem] text-text-muted">No {{ activeFilterObj.heading.toLowerCase() }} at the moment.</p>
    </div>

    <!-- Pagination -->
    <div v-if="issues.length > 0" class="flex items-center justify-between mt-4">
      <button
        type="button"
        :disabled="page <= 1"
        class="px-2.5 py-1 text-[0.75rem] rounded-lg border transition-colors disabled:opacity-40"
        :class="page <= 1 ? 'text-text-muted border-border' : 'text-text-secondary border-border hover:bg-white/[0.04]'"
        @click="goToPage(page - 1)"
      >
        Prev
      </button>
      <span class="text-[0.75rem] text-text-muted">Page {{ page }} of {{ totalPages }}</span>
      <button
        type="button"
        :disabled="page >= totalPages"
        class="px-2.5 py-1 text-[0.75rem] rounded-lg border transition-colors disabled:opacity-40"
        :class="page >= totalPages ? 'text-text-muted border-border' : 'text-text-secondary border-border hover:bg-white/[0.04]'"
        @click="goToPage(page + 1)"
      >
        Next
      </button>
    </div>

    <!-- Delete Confirm Modal -->
    <ConfirmModal
      v-if="deleteTarget !== null"
      title="Delete Network Issue"
      message="Are you sure you want to delete this network issue? This action cannot be undone."
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="deleting"
      variant="danger"
      @confirm="handleDelete"
      @close="deleteTarget = null"
    />

  </div>
</template>
