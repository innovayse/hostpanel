<script setup lang="ts">
/**
 * Displays a paginated log of emails sent to the current client.
 *
 * Read-only view — no actions available.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useClientEmailStore } from '../stores/clientEmailStore'
import { formatDate } from '../../../utils/format'

const route = useRoute()
const store = useClientEmailStore()

/** Current 1-based page number. */
const page = ref(1)

/** Number of entries per page. */
const pageSize = 20

/** Total number of pages. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / pageSize)))

/** Numeric client ID from route params. */
const clientId = computed(() => Number(route.params.id))

/**
 * Loads email logs for the current page.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function load(): Promise<void> {
  await store.fetchClientEmails(clientId.value, page.value, pageSize)
}

/**
 * Navigates to a specific page and reloads.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  load()
}

onMounted(load)
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between gap-2.5 mb-5">
      <h1 class="text-[0.875rem] font-semibold text-text-primary">Emails</h1>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
      {{ store.error }}
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.emails.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading emails...
    </div>

    <template v-else>
      <!-- Table card -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

        <!-- Header row -->
        <div class="hidden sm:grid grid-cols-[200px_1fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Subject</span>
        </div>

        <!-- Rows -->
        <div
          v-for="email in store.emails"
          :key="email.id"
          class="grid grid-cols-1 sm:grid-cols-[200px_1fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.82rem] text-text-secondary whitespace-nowrap">{{ formatDate(email.sentAt) }}</span>
          <span class="text-[0.82rem] text-text-secondary">{{ email.subject }}</span>
        </div>

        <!-- Empty state -->
        <div v-if="store.emails.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No Records Found</p>
        </div>

        <!-- Pagination -->
        <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
          <span class="text-[0.75rem] text-text-muted">
            {{ store.totalCount }} email{{ store.totalCount !== 1 ? 's' : '' }}
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
