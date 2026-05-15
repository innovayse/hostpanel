<script setup lang="ts">
/**
 * Cancellation Requests — paginated table of all service cancellation requests.
 * Shows Date, Product/Service, Client, Reason, Type, Status, and Actions (delete).
 */
import { onMounted } from 'vue'
import { useCancellationRequestsStore } from '../stores/cancellationRequestsStore'

const store = useCancellationRequestsStore()

/** Type badge style map. */
const typeStyles: Record<string, string> = {
  Immediate: 'text-status-red bg-status-red/10 border-status-red/20',
  'End of Billing Period': 'text-primary-400 bg-primary-500/10 border-primary-500/20',
}

/** Status badge style map. */
const statusStyles: Record<string, string> = {
  Open: 'text-status-green bg-status-green/10 border-status-green/20',
  Closed: 'text-text-muted bg-white/[0.04] border-border',
}

/**
 * Formats a date string as a localized date.
 *
 * @param iso - ISO 8601 date string.
 * @returns Formatted date string or "—" if invalid.
 */
function formatDate(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime()) || d.getFullYear() < 2000) return '—'
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

/**
 * Truncates a string to a maximum length, appending an ellipsis.
 *
 * @param text - The string to truncate.
 * @param max - Maximum character count before truncation.
 * @returns The truncated string or the original if shorter than max.
 */
function truncate(text: string, max: number): string {
  if (text.length <= max) return text
  return text.slice(0, max) + '…'
}

/** Total number of pages. */
function totalPages(): number {
  return Math.max(1, Math.ceil(store.totalCount / store.pageSize))
}

/**
 * Navigate to a page.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages()) return
  store.page = p
  store.fetchAll()
}

/**
 * Prompts for confirmation, then deletes a cancellation request.
 *
 * @param id - The cancellation request ID to delete.
 */
function confirmDelete(id: number): void {
  if (confirm('Are you sure you want to delete this cancellation request?')) {
    store.deleteRequest(id)
  }
}

onMounted(() => store.fetchAll())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Cancellation Requests</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Review and process service cancellation requests</p>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.cancellations.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading cancellation requests…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else-if="store.cancellations.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[1fr_1.5fr_1.5fr_2fr_1.2fr_0.8fr_0.6fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Product/Service</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Reason</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Type</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
      </div>

      <!-- Rows -->
      <div
        v-for="item in store.cancellations"
        :key="item.id"
        class="grid grid-cols-1 sm:grid-cols-[1fr_1.5fr_1.5fr_2fr_1.2fr_0.8fr_0.6fr] items-center gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
      >
        <!-- Date -->
        <span class="text-[0.82rem] text-text-muted">{{ formatDate(item.createdAt) }}</span>

        <!-- Product/Service -->
        <span class="text-[0.875rem] font-medium text-text-primary truncate">{{ item.serviceName }}</span>

        <!-- Client -->
        <RouterLink
          :to="`/clients/${item.clientId}`"
          class="text-[0.82rem] text-primary-400 hover:text-primary-300 truncate transition-colors"
        >
          {{ item.clientName }}
        </RouterLink>

        <!-- Reason -->
        <span
          class="text-[0.82rem] text-text-secondary truncate"
          :title="item.reason ?? ''"
        >
          {{ item.reason ? truncate(item.reason, 60) : '—' }}
        </span>

        <!-- Type -->
        <span
          class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
          :class="typeStyles[item.type] ?? typeStyles['End of Billing Period']"
        >
          {{ item.type }}
        </span>

        <!-- Status -->
        <span
          class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
          :class="statusStyles[item.status] ?? statusStyles.Closed"
        >
          {{ item.status }}
        </span>

        <!-- Actions -->
        <div class="flex items-center">
          <button
            class="p-1.5 rounded-lg text-text-muted hover:text-status-red hover:bg-status-red/10 transition-colors"
            title="Delete request"
            @click="confirmDelete(item.id)"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="3 6 5 6 21 6" />
              <path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
              <line x1="10" y1="11" x2="10" y2="17" />
              <line x1="14" y1="11" x2="14" y2="17" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages() > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
        <span class="text-[0.75rem] text-text-muted">
          {{ store.totalCount }} request{{ store.totalCount !== 1 ? 's' : '' }}
        </span>
        <div class="flex items-center gap-1">
          <button
            :disabled="store.page <= 1"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(store.page - 1)"
          >
            Prev
          </button>
          <span class="text-[0.75rem] text-text-muted px-2">{{ store.page }} / {{ totalPages() }}</span>
          <button
            :disabled="store.page >= totalPages()"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(store.page + 1)"
          >
            Next
          </button>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3">
      <div class="w-12 h-12 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center">
        <svg class="w-6 h-6 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10" /><line x1="15" y1="9" x2="9" y2="15" /><line x1="9" y1="9" x2="15" y2="15" />
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">No cancellation requests found</p>
    </div>
  </div>
</template>
