<script setup lang="ts">
/**
 * TLD Pricing list view -- searchable, filterable table of all TLD configurations.
 *
 * Displays TLD name, provider, status, cost/sell pricing, margin, categories,
 * last sync time, and edit/delete actions. Supports import from registrar
 * providers and manual price sync. Uses AppSelect for filters and ConfirmModal
 * for delete confirmation.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useTldConfigsStore } from '../stores/tldConfigsStore'
import AppSelect from '@/components/AppSelect.vue'
import ConfirmModal from '@/components/ConfirmModal.vue'
import type { TldConfigListItem } from '@/types/models'

const router = useRouter()
const store = useTldConfigsStore()

/** Local search model bound to the input. */
const searchInput = ref('')

/** Provider filter value. */
const providerFilter = ref('')

/** Status filter value. */
const statusFilter = ref('')

/** Whether the import dropdown is open. */
const showImportDropdown = ref(false)

/** Whether an import operation is in progress. */
const importing = ref(false)

/** Whether a sync operation is in progress. */
const syncing = ref(false)

/** Result message from import or sync operations. */
const resultMessage = ref<string | null>(null)

/** Whether the result message is an error. */
const resultIsError = ref(false)

/** Debounce timer handle for search input. */
let searchTimer: ReturnType<typeof setTimeout> | null = null

/** Current page number (1-based). */
const currentPage = ref(1)

/** Number of items displayed per page. */
const PAGE_SIZE = 20

/** The TLD config pending deletion, or null if modal is closed. */
const deleteTarget = ref<TldConfigListItem | null>(null)

/** Whether a delete operation is in progress. */
const deleting = ref(false)

/** Provider badge style classes keyed by registrar module name. */
const providerClasses: Record<string, string> = {
  NameAm: 'text-cyan-400 bg-cyan-400/10 border border-cyan-400/20',
  Namecheap: 'text-orange-400 bg-orange-400/10 border border-orange-400/20',
}

/** Currency symbols for formatting prices. */
const currencySymbols: Record<string, string> = {
  USD: '$',
  EUR: '\u20AC',
  GBP: '\u00A3',
  AMD: '\u058F',
  RUB: '\u20BD',
}

/** Options for the provider filter dropdown. */
const providerFilterOptions = [
  { value: '', label: 'All Providers' },
  { value: 'NameAm', label: 'Name.am' },
  { value: 'Namecheap', label: 'Namecheap' },
]

/** Options for the status filter dropdown. */
const statusFilterOptions = [
  { value: '', label: 'All Status' },
  { value: 'Enabled', label: 'Enabled' },
  { value: 'Disabled', label: 'Disabled' },
]

/**
 * Filtered list of TLD configs based on search, provider, and status filters.
 *
 * @returns Filtered array of TLD config list items.
 */
const filteredConfigs = computed<TldConfigListItem[]>(() => {
  let result = store.configs

  if (searchInput.value.trim()) {
    const q = searchInput.value.toLowerCase().trim()
    result = result.filter(c =>
      c.tld.toLowerCase().includes(q) ||
      c.registrarModule.toLowerCase().includes(q) ||
      c.categories.some(cat => cat.toLowerCase().includes(q)),
    )
  }

  if (providerFilter.value) {
    result = result.filter(c => c.registrarModule === providerFilter.value)
  }

  if (statusFilter.value === 'Enabled') {
    result = result.filter(c => c.isEnabled)
  } else if (statusFilter.value === 'Disabled') {
    result = result.filter(c => !c.isEnabled)
  }

  return result
})

/**
 * Total number of pages based on filtered results.
 *
 * @returns Page count (minimum 1).
 */
const totalPages = computed(() => Math.max(1, Math.ceil(filteredConfigs.value.length / PAGE_SIZE)))

/**
 * Subset of filtered configs for the current page.
 *
 * @returns Paginated array of TLD config list items.
 */
const paginatedConfigs = computed<TldConfigListItem[]>(() => {
  const start = (currentPage.value - 1) * PAGE_SIZE
  return filteredConfigs.value.slice(start, start + PAGE_SIZE)
})

/**
 * Navigates to a specific page.
 *
 * @param page - Target page number.
 */
function goToPage(page: number): void {
  if (page >= 1 && page <= totalPages.value) {
    currentPage.value = page
  }
}

/**
 * Handles search input with debouncing.
 */
function handleSearch(): void {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    currentPage.value = 1
  }, 350)
}

/**
 * Navigates to the create TLD config page.
 */
function navigateCreate(): void {
  router.push('/settings/tld-pricing/create')
}

/**
 * Navigates to the edit page for a given TLD config.
 *
 * @param item - The TLD config to edit.
 */
function navigateEdit(item: TldConfigListItem): void {
  router.push(`/settings/tld-pricing/${item.id}/edit`)
}

/**
 * Opens the delete confirmation modal for a TLD config.
 *
 * @param item - The TLD config to delete.
 */
function promptDelete(item: TldConfigListItem): void {
  deleteTarget.value = item
}

/**
 * Confirms and deletes the targeted TLD config.
 */
async function confirmDelete(): Promise<void> {
  if (!deleteTarget.value) return
  deleting.value = true
  try {
    await store.remove(deleteTarget.value.id)
  } catch {
    // error is set in the store
  } finally {
    deleting.value = false
    deleteTarget.value = null
  }
}

/**
 * Imports TLDs from the specified registrar provider.
 *
 * @param module - Registrar module name.
 */
async function handleImport(module: string): Promise<void> {
  showImportDropdown.value = false
  importing.value = true
  resultMessage.value = null
  resultIsError.value = false
  try {
    const result = await store.importFromProvider(module)
    resultMessage.value = `Import complete: ${result.imported} imported, ${result.updated} updated.`
    setTimeout(() => { resultMessage.value = null }, 5000)
  } catch {
    resultMessage.value = 'Import failed. Please try again.'
    resultIsError.value = true
    setTimeout(() => { resultMessage.value = null }, 5000)
  } finally {
    importing.value = false
  }
}

/**
 * Triggers a manual price sync from all providers.
 */
async function handleSync(): Promise<void> {
  syncing.value = true
  resultMessage.value = null
  resultIsError.value = false
  try {
    await store.syncPrices()
    resultMessage.value = 'Price sync complete.'
    setTimeout(() => { resultMessage.value = null }, 5000)
  } catch {
    resultMessage.value = 'Sync failed. Please try again.'
    resultIsError.value = true
    setTimeout(() => { resultMessage.value = null }, 5000)
  } finally {
    syncing.value = false
  }
}

/**
 * Formats a price with the appropriate currency symbol.
 *
 * @param price - Numeric price value, or null.
 * @param currency - ISO 4217 currency code.
 * @returns Formatted price string or em dash if null.
 */
function formatPrice(price: number | null, currency: string): string {
  if (price === null || price === undefined) return '\u2014'
  const sym = currencySymbols[currency] ?? ''
  return `${sym}${price.toFixed(2)}`
}

/**
 * Formats a margin percentage for display.
 *
 * @param margin - Margin percentage, or null.
 * @returns Formatted margin string with sign, or em dash if null.
 */
function formatMargin(margin: number | null): string {
  if (margin === null || margin === undefined) return '\u2014'
  const sign = margin >= 0 ? '+' : ''
  return `${sign}${margin.toFixed(1)}%`
}

/**
 * Returns CSS classes for margin text color.
 *
 * @param margin - Margin percentage, or null.
 * @returns Tailwind color class.
 */
function marginClass(margin: number | null): string {
  if (margin === null || margin === undefined) return 'text-text-muted'
  return margin >= 0 ? 'text-status-green' : 'text-status-red'
}

/**
 * Formats a date string as relative time (e.g. "2h ago").
 *
 * @param dateStr - ISO 8601 date string or null.
 * @returns Relative time string or "Never".
 */
function formatRelativeTime(dateStr: string | null): string {
  if (!dateStr) return 'Never'
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMin = Math.floor(diffMs / 60000)
  if (diffMin < 1) return 'Just now'
  if (diffMin < 60) return `${diffMin}m ago`
  const diffHr = Math.floor(diffMin / 60)
  if (diffHr < 24) return `${diffHr}h ago`
  const diffDay = Math.floor(diffHr / 24)
  if (diffDay < 30) return `${diffDay}d ago`
  return date.toLocaleDateString()
}

onMounted(() => store.fetchAll())

watch(searchInput, handleSearch)
watch(providerFilter, () => { currentPage.value = 1 })
watch(statusFilter, () => { currentPage.value = 1 })
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between mb-7">
      <div>
        <h1 class="font-display text-[1.75rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">
          TLD Pricing
        </h1>
        <p class="text-sm text-text-secondary">Configure domain extension pricing and providers</p>
      </div>
      <div class="flex items-center gap-2">
        <!-- Import from Provider -->
        <div class="relative">
          <button
            class="flex items-center gap-1.5 px-4 py-2.5 text-[0.85rem] font-medium text-text-secondary bg-surface-card border border-border rounded-[10px] hover:text-text-primary hover:border-white/20 transition-colors"
            :disabled="importing"
            @click="showImportDropdown = !showImportDropdown"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
            <span v-if="importing">Importing...</span>
            <span v-else>Import</span>
          </button>
          <div
            v-if="showImportDropdown"
            class="absolute right-0 mt-1.5 w-44 bg-surface-card border border-border rounded-xl shadow-xl z-20 overflow-hidden"
          >
            <button
              class="w-full text-left px-4 py-2.5 text-sm text-text-secondary hover:bg-white/[0.04] hover:text-text-primary transition-colors"
              @click="handleImport('NameAm')"
            >
              Name.am
            </button>
            <button
              class="w-full text-left px-4 py-2.5 text-sm text-text-secondary hover:bg-white/[0.04] hover:text-text-primary transition-colors"
              @click="handleImport('Namecheap')"
            >
              Namecheap
            </button>
          </div>
        </div>

        <!-- Sync Prices -->
        <button
          class="flex items-center gap-1.5 px-4 py-2.5 text-[0.85rem] font-medium text-text-secondary bg-surface-card border border-border rounded-[10px] hover:text-text-primary hover:border-white/20 transition-colors"
          :disabled="syncing"
          @click="handleSync"
        >
          <svg class="w-4 h-4" :class="{ 'animate-spin': syncing }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="23 4 23 10 17 10" />
            <polyline points="1 20 1 14 7 14" />
            <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15" />
          </svg>
          <span v-if="syncing">Syncing...</span>
          <span v-else>Sync Prices</span>
        </button>

        <!-- Add TLD -->
        <button
          class="gradient-brand text-white rounded-[10px] px-5 py-2.5 text-[0.85rem] font-semibold transition-all duration-150 hover:-translate-y-px flex items-center gap-1.5"
          @click="navigateCreate"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          Add TLD
        </button>
      </div>
    </div>

    <!-- Result message -->
    <div
      v-if="resultMessage"
      class="text-sm rounded-xl p-4 mb-5 flex items-center gap-2"
      :class="resultIsError
        ? 'bg-status-red/8 border border-status-red/20 text-status-red'
        : 'bg-primary-500/8 border border-primary-500/20 text-primary-400'"
    >
      <svg v-if="!resultIsError" class="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <polyline points="20 6 9 17 4 12" />
      </svg>
      {{ resultMessage }}
    </div>

    <!-- Filters -->
    <div class="flex items-center gap-3 mb-5">
      <!-- Search -->
      <div class="relative flex-1 max-w-sm">
        <svg
          class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-muted pointer-events-none"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <circle cx="11" cy="11" r="8" />
          <line x1="21" y1="21" x2="16.65" y2="16.65" />
        </svg>
        <input
          v-model="searchInput"
          type="text"
          placeholder="Search TLDs..."
          class="w-full bg-white/[0.04] border border-border rounded-[10px] pl-9 pr-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
        />
      </div>

      <!-- Provider filter -->
      <AppSelect
        :model-value="providerFilter"
        :options="providerFilterOptions"
        class="w-44"
        @update:model-value="providerFilter = $event"
      />

      <!-- Status filter -->
      <AppSelect
        :model-value="statusFilter"
        :options="statusFilterOptions"
        class="w-36"
        @update:model-value="statusFilter = $event"
      />
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading TLD configurations...
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border">
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">TLD</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Provider</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Status</th>
            <th class="px-4 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Cost (1yr)</th>
            <th class="px-4 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Sell (1yr)</th>
            <th class="px-4 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Margin</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Categories</th>
            <th class="px-4 py-3.5 text-left text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Last Synced</th>
            <th class="px-4 py-3.5 text-right text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-border">
          <tr
            v-for="item in paginatedConfigs"
            :key="item.id"
            class="hover:bg-white/[0.02] transition-colors"
          >
            <!-- TLD -->
            <td class="px-4 py-3.5">
              <span class="font-semibold text-text-primary">.{{ item.tld }}</span>
            </td>

            <!-- Provider -->
            <td class="px-4 py-3.5">
              <span
                class="text-[0.65rem] font-semibold rounded-full px-2.5 py-1"
                :class="providerClasses[item.registrarModule] ?? 'text-text-muted bg-white/[0.04] border border-border'"
              >
                {{ item.registrarModule }}
              </span>
            </td>

            <!-- Status -->
            <td class="px-4 py-3.5">
              <span
                class="text-[0.65rem] font-semibold rounded-full px-2.5 py-1"
                :class="item.isEnabled
                  ? 'text-status-green bg-status-green/10 border border-status-green/20'
                  : 'text-status-red bg-status-red/10 border border-status-red/20'"
              >
                {{ item.isEnabled ? 'Enabled' : 'Disabled' }}
              </span>
            </td>

            <!-- Cost (1yr) -->
            <td class="px-4 py-3.5 text-right text-text-secondary tabular-nums">
              {{ formatPrice(item.costRegister1yr, item.currency) }}
            </td>

            <!-- Sell (1yr) -->
            <td class="px-4 py-3.5 text-right text-text-secondary tabular-nums">
              {{ formatPrice(item.sellRegister1yr, item.sellCurrency) }}
            </td>

            <!-- Margin -->
            <td class="px-4 py-3.5 text-right tabular-nums">
              <span :class="marginClass(item.marginPercent)" class="font-medium">
                {{ formatMargin(item.marginPercent) }}
              </span>
            </td>

            <!-- Categories -->
            <td class="px-4 py-3.5">
              <div class="flex flex-wrap gap-1">
                <span
                  v-for="cat in item.categories"
                  :key="cat"
                  class="text-[0.6rem] font-medium rounded-full px-2 py-0.5 text-text-muted bg-white/[0.04] border border-border"
                >
                  {{ cat }}
                </span>
                <span v-if="item.categories.length === 0" class="text-text-muted">&mdash;</span>
              </div>
            </td>

            <!-- Last Synced -->
            <td class="px-4 py-3.5 text-text-muted text-xs">
              {{ formatRelativeTime(item.lastSyncedAt) }}
            </td>

            <!-- Actions -->
            <td class="px-4 py-3.5 text-right">
              <div class="flex items-center justify-end gap-1">
                <button
                  class="text-text-muted hover:text-primary-400 transition-colors p-1"
                  title="Edit TLD"
                  @click="navigateEdit(item)"
                >
                  <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                    <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                  </svg>
                </button>
                <button
                  class="text-text-muted hover:text-status-red transition-colors p-1"
                  title="Delete TLD"
                  @click="promptDelete(item)"
                >
                  <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M3 6h18"/>
                    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                    <line x1="10" y1="11" x2="10" y2="17"/>
                    <line x1="14" y1="11" x2="14" y2="17"/>
                  </svg>
                </button>
              </div>
            </td>
          </tr>
        </tbody>
        <tbody v-if="paginatedConfigs.length === 0 && !store.loading">
          <tr>
            <td colspan="9" class="px-5 py-8 text-center text-text-muted">
              No TLD configurations found. Add your first TLD or import from a provider.
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div
      v-if="totalPages > 1"
      class="flex items-center justify-between mt-4 text-sm"
    >
      <span class="text-text-muted">
        Showing {{ (currentPage - 1) * PAGE_SIZE + 1 }}–{{ Math.min(currentPage * PAGE_SIZE, filteredConfigs.length) }}
        of {{ filteredConfigs.length }} TLDs
      </span>
      <div class="flex items-center gap-1">
        <button
          class="px-3 py-1.5 rounded-lg border border-border text-text-secondary hover:text-text-primary hover:border-white/20 transition-colors disabled:opacity-40 disabled:pointer-events-none"
          :disabled="currentPage === 1"
          @click="goToPage(currentPage - 1)"
        >
          Prev
        </button>
        <template v-for="page in totalPages" :key="page">
          <button
            v-if="page === 1 || page === totalPages || (page >= currentPage - 2 && page <= currentPage + 2)"
            class="w-8 h-8 rounded-lg text-[0.8rem] font-medium transition-colors"
            :class="page === currentPage
              ? 'gradient-brand text-white'
              : 'text-text-secondary hover:text-text-primary hover:bg-white/[0.04]'"
            @click="goToPage(page)"
          >
            {{ page }}
          </button>
          <span
            v-else-if="page === currentPage - 3 || page === currentPage + 3"
            class="text-text-muted px-1"
          >
            &hellip;
          </span>
        </template>
        <button
          class="px-3 py-1.5 rounded-lg border border-border text-text-secondary hover:text-text-primary hover:border-white/20 transition-colors disabled:opacity-40 disabled:pointer-events-none"
          :disabled="currentPage === totalPages"
          @click="goToPage(currentPage + 1)"
        >
          Next
        </button>
      </div>
    </div>

    <!-- Delete confirmation modal -->
    <ConfirmModal
      v-if="deleteTarget"
      title="Delete TLD Configuration"
      :message="`Delete &quot;.${deleteTarget.tld}&quot; TLD configuration? This action cannot be undone.`"
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="deleting"
      variant="danger"
      @confirm="confirmDelete"
      @close="deleteTarget = null"
    />

  </div>
</template>
