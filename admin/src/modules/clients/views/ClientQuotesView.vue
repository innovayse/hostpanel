<script setup lang="ts">
/**
 * Client-scoped quotes list -- shows quotes for a specific client
 * with pagination, delete actions, and navigation to quote detail.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuoteStore } from '../../billing/stores/quoteStore'
import { QUOTE_STAGE_STYLES } from '../../../utils/constants'
import { formatDate } from '../../../utils/format'
import ConfirmModal from '../../../components/ConfirmModal.vue'

const route = useRoute()
const router = useRouter()
const store = useQuoteStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(25)

/** Total pages. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.clientQuotesTotal / pageSize.value)))

/** Quote ID pending deletion. */
const deleteTargetId = ref<number | null>(null)

/** Whether the delete confirm modal is visible. */
const showDeleteModal = ref(false)

/**
 * Fetches client quotes with current pagination.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function fetchQuotes(): Promise<void> {
  await store.fetchClientQuotes(clientId.value, page.value, pageSize.value)
}

/**
 * Navigates to a specific page.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  fetchQuotes()
}

/**
 * Opens the delete confirmation modal for a quote.
 *
 * @param id - The quote ID to delete.
 */
function confirmDelete(id: number): void {
  deleteTargetId.value = id
  showDeleteModal.value = true
}

/**
 * Deletes the targeted quote and refreshes the list.
 *
 * @returns Promise that resolves when deletion completes.
 */
async function executeDelete(): Promise<void> {
  if (!deleteTargetId.value) return
  await store.deleteQuote(deleteTargetId.value)
  showDeleteModal.value = false
  deleteTargetId.value = null
  await fetchQuotes()
}

/**
 * Returns the CSS class string for a stage badge.
 *
 * @param stage - The quote stage.
 * @returns Tailwind class string.
 */
function stageClass(stage: string): string {
  return QUOTE_STAGE_STYLES[stage] ?? 'text-text-muted bg-white/[0.04] border-border'
}

/**
 * Returns a human-readable label for a stage value.
 *
 * @param stage - The quote stage.
 * @returns Display label.
 */
function stageLabel(stage: string): string {
  if (stage === 'OnHold') return 'On Hold'
  return stage
}

onMounted(() => fetchQuotes())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between gap-2.5 mb-5">
      <h1 class="text-[0.875rem] font-semibold text-text-primary">Quotes</h1>
      <button
        type="button"
        class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity"
        @click="router.push(`/billing/quotes/add?clientId=${clientId}`)"
      >
        + Create New Quote
      </button>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
      {{ store.error }}
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.clientQuotes.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading quotes...
    </div>

    <template v-else>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- Header row -->
        <div class="hidden sm:grid grid-cols-[0.4fr_1fr_0.7fr_0.7fr_0.6fr_0.6fr_auto] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Subject</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Create Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Valid Until</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Stage</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
        </div>

        <!-- Rows -->
        <div
          v-for="quote in store.clientQuotes"
          :key="quote.id"
          class="grid grid-cols-1 sm:grid-cols-[0.4fr_1fr_0.7fr_0.7fr_0.6fr_0.6fr_auto] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">#{{ quote.id }}</span>
          <span class="text-[0.82rem] text-text-primary font-medium hidden sm:block truncate">{{ quote.subject }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ formatDate(quote.dateCreated) }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ formatDate(quote.validUntil) }}</span>
          <span class="text-[0.82rem] text-text-primary font-medium hidden sm:block">${{ quote.total.toFixed(2) }} USD</span>
          <span class="hidden sm:block">
            <span
              class="inline-block px-2 py-0.5 rounded-full text-[0.7rem] font-medium border"
              :class="stageClass(quote.stage)"
            >
              {{ stageLabel(quote.stage) }}
            </span>
          </span>
          <span class="hidden sm:flex items-center gap-1.5">
            <button
              type="button"
              class="px-2.5 py-1 text-[0.72rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-lg hover:bg-primary-500/20 transition-colors"
              @click="router.push(`/billing/quotes/${quote.id}`)"
            >
              Edit
            </button>
            <button
              type="button"
              class="px-2.5 py-1 text-[0.72rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-lg hover:bg-status-red/20 transition-colors"
              @click="confirmDelete(quote.id)"
            >
              Delete
            </button>
          </span>
        </div>

        <!-- Empty state -->
        <div v-if="store.clientQuotes.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No quotes found.</p>
        </div>

        <!-- Pagination -->
        <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
          <span class="text-[0.75rem] text-text-muted">
            {{ store.clientQuotesTotal }} quote{{ store.clientQuotesTotal !== 1 ? 's' : '' }}
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

    <!-- Delete Confirm Modal -->
    <ConfirmModal
      v-if="showDeleteModal"
      title="Delete Quote"
      :message="`Are you sure you want to delete quote #${deleteTargetId}?`"
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="store.loading"
      variant="danger"
      @confirm="executeDelete"
      @close="showDeleteModal = false"
    />
  </div>
</template>
