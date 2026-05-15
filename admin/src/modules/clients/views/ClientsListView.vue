<script setup lang="ts">
/**
 * Clients list view — paginated table with advanced filter bar.
 */
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useClientsStore, type ClientFilters } from '../stores/clientsStore'
import { CLIENT_STATUS_STYLES } from '../../../utils/constants'
import AppSelect from '../../../components/AppSelect.vue'

const router = useRouter()
const store = useClientsStore()

/** Filter form state. */
const nameFilter = ref('')
const emailFilter = ref('')
const phoneFilter = ref('')
const statusFilter = ref('')

/** Builds the current filter object from form state. */
function currentFilters(): ClientFilters {
  return {
    search: nameFilter.value || undefined,
    email: emailFilter.value || undefined,
    phone: phoneFilter.value || undefined,
    status: statusFilter.value || undefined,
  }
}

/** True when any filter field has a value. */
const hasFilters = computed(() =>
  !!(nameFilter.value || emailFilter.value || phoneFilter.value || statusFilter.value)
)

/** Number of active filters. */
const activeFilterCount = computed(() =>
  [nameFilter.value, emailFilter.value, phoneFilter.value, statusFilter.value].filter(Boolean).length
)

/** Applies filters and resets to page 1. */
function applyFilters(): void {
  store.page = 1
  store.fetchAll(currentFilters())
}

/** Clears all filters and re-fetches. */
function clearFilters(): void {
  nameFilter.value = ''
  emailFilter.value = ''
  phoneFilter.value = ''
  statusFilter.value = ''
  store.page = 1
  store.fetchAll()
}

/** Submits on Enter key in any input. */
function onKeydown(e: KeyboardEvent): void {
  if (e.key === 'Enter') applyFilters()
}

/** Status badge style map. */
const statusStyles = CLIENT_STATUS_STYLES

/** Total number of pages. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / store.pageSize)))

/** Navigate to a page. */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  store.page = p
  store.fetchAll(currentFilters())
}

onMounted(() => store.fetchAll())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-start justify-between mb-5">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Clients</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">Manage your client accounts</p>
      </div>
      <button
        class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90 shrink-0"
        @click="router.push('/clients/add')"
      >
        <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        Add Client
      </button>
    </div>

    <!-- Filter bar -->
    <div class="bg-surface-card border border-border rounded-2xl p-4 mb-4">
      <div class="flex flex-col sm:flex-row items-stretch sm:items-end gap-3">

        <!-- Name -->
        <div class="flex-1 min-w-0">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Client / Company Name</label>
          <input
            v-model="nameFilter"
            type="text"
            placeholder="Search by name…"
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
            @keydown="onKeydown"
          />
        </div>

        <!-- Email -->
        <div class="flex-1 min-w-0">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Email Address</label>
          <input
            v-model="emailFilter"
            type="text"
            placeholder="Search by email…"
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
            @keydown="onKeydown"
          />
        </div>

        <!-- Phone -->
        <div class="flex-1 min-w-0">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Phone Number</label>
          <input
            v-model="phoneFilter"
            type="text"
            placeholder="Search by phone…"
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
            @keydown="onKeydown"
          />
        </div>

        <!-- Status -->
        <div class="sm:w-40 shrink-0">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
          <AppSelect
            v-model="statusFilter"
            :options="[
              { value: '', label: 'Any' },
              { value: 'Active', label: 'Active' },
              { value: 'Inactive', label: 'Inactive' },
              { value: 'Suspended', label: 'Suspended' },
              { value: 'Closed', label: 'Closed' },
            ]"
            placeholder="Any"
          />
        </div>

        <!-- Actions -->
        <div class="flex items-end gap-2 shrink-0">
          <button
            class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90"
            @click="applyFilters"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
            </svg>
            Search
            <span
              v-if="activeFilterCount > 0"
              class="ml-0.5 flex items-center justify-center w-4 h-4 rounded-full bg-white/20 text-[0.6rem] font-bold"
            >
              {{ activeFilterCount }}
            </span>
          </button>
          <button
            v-if="hasFilters"
            class="px-3 py-2 text-[0.78rem] text-text-muted hover:text-text-primary transition-colors"
            @click="clearFilters"
          >
            Clear
          </button>
        </div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.clients.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading clients…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else-if="store.clients.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[0.5fr_2fr_2fr_1.5fr_1fr_1fr] gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Email</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Company</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Joined</span>
      </div>

      <!-- Rows -->
      <RouterLink
        v-for="client in store.clients"
        :key="client.id"
        :to="`/clients/${client.id}`"
        class="grid grid-cols-1 sm:grid-cols-[0.5fr_2fr_2fr_1.5fr_1fr_1fr] gap-2 sm:gap-4 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors no-underline"
      >
        <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ client.id }}</span>

        <div class="flex items-center gap-2.5 min-w-0">
          <div class="flex items-center justify-center w-7 h-7 rounded-full bg-primary-500/10 text-primary-400 text-[0.65rem] font-bold shrink-0">
            {{ client.firstName.charAt(0) }}{{ client.lastName.charAt(0) }}
          </div>
          <span class="text-[0.875rem] font-medium text-text-primary truncate">
            {{ client.firstName }} {{ client.lastName }}
          </span>
        </div>

        <span class="text-[0.82rem] text-text-secondary truncate">{{ client.email }}</span>

        <span class="text-[0.82rem] text-text-secondary truncate">{{ client.companyName || '—' }}</span>

        <div class="flex items-center gap-1.5 flex-wrap">
          <span
            v-if="client.isUserDeleted"
            class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold text-status-red bg-status-red/10 border border-status-red/20"
          >
            User Deleted
          </span>
          <span
            v-else
            class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border"
            :class="statusStyles[client.status] ?? statusStyles.Inactive"
          >
            {{ client.status }}
          </span>
        </div>

        <span class="text-[0.82rem] text-text-muted">{{ new Date(client.createdAt).toLocaleDateString() }}</span>
      </RouterLink>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
        <span class="text-[0.75rem] text-text-muted">
          {{ store.totalCount }} client{{ store.totalCount !== 1 ? 's' : '' }}
        </span>
        <div class="flex items-center gap-1">
          <button
            :disabled="store.page <= 1"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(store.page - 1)"
          >
            Prev
          </button>
          <span class="text-[0.75rem] text-text-muted px-2">{{ store.page }} / {{ totalPages }}</span>
          <button
            :disabled="store.page >= totalPages"
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
          <path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/>
          <circle cx="9" cy="7" r="4"/>
          <path d="M23 21v-2a4 4 0 00-3-3.87"/>
          <path d="M16 3.13a4 4 0 010 7.75"/>
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">
        {{ hasFilters ? 'No clients match your filters' : 'No clients found' }}
      </p>
      <button
        v-if="hasFilters"
        class="text-[0.78rem] text-primary-400 hover:text-primary-300 transition-colors"
        @click="clearFilters"
      >
        Clear filters
      </button>
    </div>

  </div>
</template>
