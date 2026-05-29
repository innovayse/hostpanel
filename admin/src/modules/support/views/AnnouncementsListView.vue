<script setup lang="ts">
/**
 * Announcements list view -- displays all announcements with create, edit,
 * and delete actions, paginated with 20 records per page.
 */
import { ref, computed, onMounted } from 'vue'
import { useApi } from '../../../composables/useApi'
import { formatDate } from '../../../utils/format'
import ConfirmModal from '../../../components/ConfirmModal.vue'

/** Shape of an announcement returned by the API. */
interface Announcement {
  /** Unique announcement identifier. */
  id: number
  /** Title of the announcement. */
  title: string
  /** ISO 8601 creation/publish date. */
  date: string
  /** Whether the announcement is published. */
  published: boolean
}

/** Paginated response wrapper. */
interface PaginatedResponse {
  /** Array of announcement records. */
  items: Announcement[]
  /** Total number of matching records. */
  totalCount: number
}

const { request } = useApi()

/** Number of records per page. */
const pageSize = 20

/** All fetched announcements. */
const announcements = ref<Announcement[]>([])

/** Total record count from server. */
const totalCount = ref(0)

/** Current page (1-based). */
const page = ref(1)

/** Whether a fetch is in flight. */
const loading = ref(false)

/** Error message from the last fetch. */
const error = ref<string | null>(null)

/** ID of announcement pending deletion confirmation. */
const deleteTarget = ref<number | null>(null)

/** Whether a delete request is in flight. */
const deleting = ref(false)

/** Total pages. */
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))

/**
 * Formats an ISO date string as DD/MM/YYYY HH:MM.
 *
 * @param iso - ISO 8601 date string.
 * @returns Formatted date-time string.
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
 * Fetches announcements for the current page.
 */
async function fetchAnnouncements(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const data = await request<PaginatedResponse>(
      `/announcements?page=${page.value}&pageSize=${pageSize}`
    )
    announcements.value = data.items
    totalCount.value = data.totalCount
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load announcements'
  } finally {
    loading.value = false
  }
}

/**
 * Navigates to a specific page and reloads.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  fetchAnnouncements()
}

/**
 * Opens the delete confirmation modal.
 *
 * @param id - Announcement ID to delete.
 */
function confirmDelete(id: number): void {
  deleteTarget.value = id
}

/**
 * Executes the deletion of the target announcement.
 */
async function handleDelete(): Promise<void> {
  if (deleteTarget.value === null) return
  deleting.value = true
  try {
    await request(`/announcements/${deleteTarget.value}`, { method: 'DELETE' })
    deleteTarget.value = null
    await fetchAnnouncements()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete announcement'
  } finally {
    deleting.value = false
  }
}

/**
 * Closes the delete confirmation modal.
 */
function cancelDelete(): void {
  deleteTarget.value = null
}

onMounted(() => {
  fetchAnnouncements()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between mb-5">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Announcements</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">Manage client-facing announcements</p>
      </div>
      <RouterLink
        to="/support/announcements/new"
        class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity hover:opacity-90"
      >
        Add New Announcement
      </RouterLink>
    </div>

    <!-- Results metadata -->
    <div class="flex items-center justify-between mb-3 text-[0.75rem] text-text-muted">
      <span>{{ totalCount }} Records Found</span>
      <span>Page {{ page }} of {{ totalPages }}</span>
    </div>

    <!-- Loading -->
    <div v-if="loading && announcements.length === 0" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading announcements...
    </div>

    <!-- Error -->
    <div v-else-if="error && announcements.length === 0" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ error }}
    </div>

    <!-- Table -->
    <div v-else-if="announcements.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Table Header -->
      <div class="hidden sm:grid grid-cols-[1fr_2fr_0.6fr_40px_40px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Title</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Published</span>
        <span />
        <span />
      </div>

      <!-- Rows -->
      <div
        v-for="ann in announcements"
        :key="ann.id"
        class="grid grid-cols-1 sm:grid-cols-[1fr_2fr_0.6fr_40px_40px] gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center"
      >
        <!-- Date -->
        <div class="text-[0.82rem] text-text-secondary">
          {{ formatDateTime(ann.date) }}
        </div>

        <!-- Title -->
        <div class="text-[0.82rem] text-text-secondary">
          {{ ann.title }}
        </div>

        <!-- Published -->
        <div>
          <span v-if="ann.published" class="text-[0.82rem] text-green-400 font-medium">Yes</span>
          <span v-else class="text-[0.82rem] text-text-muted">No</span>
        </div>

        <!-- Edit -->
        <div class="flex items-center justify-center">
          <RouterLink
            :to="`/support/announcements/${ann.id}/edit`"
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
            @click="confirmDelete(ann.id)"
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
        <path d="M19 20H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v1m2 13a2 2 0 0 1-2-2V9a2 2 0 0 0-2-2h-1" />
      </svg>
      <p class="text-[0.875rem] font-medium text-text-secondary">No announcements found</p>
      <p class="text-[0.78rem] text-text-muted">Create your first announcement to get started.</p>
    </div>

    <!-- Pagination -->
    <div v-if="announcements.length > 0" class="flex items-center justify-between mt-4">
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
      title="Delete Announcement"
      message="Are you sure you want to delete this announcement? This action cannot be undone."
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="deleting"
      variant="danger"
      @confirm="handleDelete"
      @close="cancelDelete"
    />

  </div>
</template>
