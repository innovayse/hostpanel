<script setup lang="ts">
/**
 * Invoice detail page -- displays full invoice data with tabbed interface
 * for summary, payments, options, credit, refunds, and notes.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useBillingStore } from '../stores/billingStore'
import { INVOICE_STATUS_STYLES } from '../../../utils/constants'
import InvoiceSummaryTab from '../components/InvoiceSummaryTab.vue'
import InvoicePaymentTab from '../components/InvoicePaymentTab.vue'
import InvoiceOptionsTab from '../components/InvoiceOptionsTab.vue'
import InvoiceCreditTab from '../components/InvoiceCreditTab.vue'
import InvoiceRefundTab from '../components/InvoiceRefundTab.vue'
import InvoiceNotesTab from '../components/InvoiceNotesTab.vue'

const route = useRoute()
const router = useRouter()
const store = useBillingStore()

/** Invoice ID from route params. */
const invoiceId = computed(() => Number(route.params.id))

/** Whether the page is in view-only mode (no editing). */
const isViewMode = computed(() => route.query.mode === 'view')

/** Currently active tab. */
const activeTab = ref('summary')

/** All available tab definitions. */
const allTabs = [
  { key: 'summary', label: 'Summary' },
  { key: 'payment', label: 'Add Payment' },
  { key: 'options', label: 'Options' },
  { key: 'credit', label: 'Credit' },
  { key: 'refund', label: 'Refund' },
  { key: 'notes', label: 'Notes' },
]

/** Tabs filtered by mode — Options tab is hidden in view mode. */
const tabs = computed(() =>
  isViewMode.value ? allTabs.filter(t => t.key !== 'options') : allTabs,
)

/**
 * Returns the CSS class for a status badge.
 *
 * @param status - The invoice status.
 * @returns Tailwind class string.
 */
function statusClass(status: string): string {
  return INVOICE_STATUS_STYLES[status] ?? 'text-text-muted bg-white/[0.04] border-border'
}

/** Reloads the current invoice. */
async function handleUpdated(): Promise<void> {
  await store.fetchById(invoiceId.value)
}

/** Navigates back to the invoices list. */
function goBack(): void {
  router.back()
}

// Re-fetch when route param changes
watch(invoiceId, () => {
  store.fetchById(invoiceId.value)
})

onMounted(() => store.fetchById(invoiceId.value))
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Loading -->
    <div v-if="store.loading && !store.currentInvoice" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading invoice...
    </div>

    <!-- Error -->
    <div v-else-if="store.error && !store.currentInvoice" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ store.error }}
    </div>

    <template v-else-if="store.currentInvoice">

      <!-- Header -->
      <div class="flex items-center justify-between gap-2.5 mb-5">
        <div class="flex items-center gap-3">
          <button
            type="button"
            class="px-3 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors flex items-center gap-1.5"
            @click="goBack"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="15 18 9 12 15 6"/>
            </svg>
            Back
          </button>
          <h1 class="text-[0.875rem] font-semibold text-text-primary">
            Invoice #{{ store.currentInvoice.id }}
          </h1>
          <span
            class="inline-block px-2.5 py-0.5 rounded-full text-[0.7rem] font-medium border"
            :class="statusClass(store.currentInvoice.status)"
          >
            {{ store.currentInvoice.status }}
          </span>
        </div>

        <!-- Error feedback -->
        <div v-if="store.error" class="px-4 py-2.5 text-[0.82rem] text-status-red bg-status-red/10 border border-status-red/20 rounded-xl">
          {{ store.error }}
        </div>
      </div>

      <!-- Tab bar -->
      <div class="flex items-center gap-1 mb-5 border-b border-border">
        <button
          v-for="tab in tabs"
          :key="tab.key"
          type="button"
          class="px-4 py-2.5 text-[0.82rem] font-medium transition-colors border-b-2 -mb-px"
          :class="activeTab === tab.key
            ? 'text-primary-400 border-primary-500'
            : 'text-text-muted hover:text-text-secondary border-transparent'"
          @click="activeTab = tab.key"
        >
          {{ tab.label }}
        </button>
      </div>

      <!-- Tab content -->
      <InvoiceSummaryTab
        v-if="activeTab === 'summary'"
        :invoice="store.currentInvoice"
        :readonly="isViewMode"
        @updated="handleUpdated"
      />
      <InvoicePaymentTab
        v-if="activeTab === 'payment'"
        :invoice="store.currentInvoice"
        :readonly="isViewMode"
        @updated="handleUpdated"
      />
      <InvoiceOptionsTab
        v-if="activeTab === 'options' && !isViewMode"
        :invoice="store.currentInvoice"
        @updated="handleUpdated"
      />
      <InvoiceCreditTab
        v-if="activeTab === 'credit'"
        :invoice="store.currentInvoice"
        :readonly="isViewMode"
        @updated="handleUpdated"
      />
      <InvoiceRefundTab
        v-if="activeTab === 'refund'"
        :invoice="store.currentInvoice"
        :readonly="isViewMode"
        @updated="handleUpdated"
      />
      <InvoiceNotesTab
        v-if="activeTab === 'notes'"
        :invoice="store.currentInvoice"
        :readonly="isViewMode"
        @updated="handleUpdated"
      />

    </template>
  </div>
</template>
