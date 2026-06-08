<script setup lang="ts">
/**
 * Client summary dashboard -- shows aggregated billing, service, and activity
 * data across 7 information cards and 3 mini tables.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useClientSummaryStore } from '../stores/clientSummaryStore'
import { useClientsStore } from '../stores/clientsStore'
import { formatDate } from '../../../utils/format'
import {
  CLIENT_STATUS_STYLES,
  SERVICE_STATUS_STYLES,
  DOMAIN_STATUS_STYLES,
  QUOTE_STAGE_STYLES,
  CONTACT_TYPE_STYLES,
} from '../../../utils/constants'

const route = useRoute()
const router = useRouter()
const summaryStore = useClientSummaryStore()
const clientsStore = useClientsStore()

/** Client ID from route params. */
const clientId = computed(() => Number(route.params.id))

/** Shorthand for the current client detail. */
const client = computed(() => clientsStore.current)

/** Shorthand for the summary data. */
const summary = computed(() => summaryStore.summary)

/** Local admin notes for the textarea. */
const adminNotes = ref('')

/** True while saving admin notes. */
const savingNotes = ref(false)

/**
 * Computes a human-readable duration from a given ISO date to now.
 *
 * @param iso - ISO 8601 date string.
 * @returns Duration string (e.g. "2 months", "1 year, 3 months").
 */
function computeDuration(iso: string | null | undefined): string {
  if (!iso) return '\u2014'
  const start = new Date(iso)
  if (isNaN(start.getTime())) return '\u2014'
  const now = new Date()
  let years = now.getFullYear() - start.getFullYear()
  let months = now.getMonth() - start.getMonth()
  if (months < 0) {
    years--
    months += 12
  }
  const parts: string[] = []
  if (years > 0) parts.push(`${years} year${years !== 1 ? 's' : ''}`)
  if (months > 0) parts.push(`${months} month${months !== 1 ? 's' : ''}`)
  return parts.length > 0 ? parts.join(', ') : 'Less than a month'
}

/**
 * Formats a number as a currency string.
 *
 * @param value - Numeric value.
 * @returns Formatted string (e.g. "$1,234.56").
 */
function formatCurrency(value: number): string {
  return `$${value.toFixed(2)}`
}

/**
 * Formats a price with its currency symbol.
 *
 * @param price - Numeric price value.
 * @param currency - ISO 4217 currency code.
 * @returns Formatted price string (e.g. "$4.99 USD").
 */
function formatPrice(price: number, currency: string): string {
  const symbols: Record<string, string> = { USD: '$', EUR: '\u20AC', GBP: '\u00A3', AMD: '\u058F', RUB: '\u20BD' }
  const sym = symbols[currency] ?? ''
  return `${sym}${price.toFixed(2)} ${currency}`
}

/**
 * Capitalizes the first letter of a billing cycle string.
 *
 * @param cycle - Raw billing cycle value (e.g. "monthly").
 * @returns Capitalized string (e.g. "Monthly").
 */
function capitalizeCycle(cycle: string): string {
  if (!cycle) return '\u2014'
  return cycle.charAt(0).toUpperCase() + cycle.slice(1)
}

/**
 * Returns a human-readable label for a quote stage value.
 *
 * @param stage - The quote stage.
 * @returns Display label.
 */
function stageLabel(stage: string): string {
  if (stage === 'OnHold') return 'On Hold'
  return stage
}

/**
 * Saves admin notes for the current client.
 *
 * @returns Promise that resolves when save completes.
 */
async function saveNotes(): Promise<void> {
  if (!client.value) return
  savingNotes.value = true
  try {
    await clientsStore.updateClient(clientId.value, { adminNotes: adminNotes.value })
  } finally {
    savingNotes.value = false
  }
}

/** Initialize admin notes from the client detail when it loads. */
watch(() => client.value?.adminNotes, (val) => {
  adminNotes.value = val ?? ''
}, { immediate: true })

onMounted(() => {
  summaryStore.fetchAll(clientId.value)
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full space-y-6">

    <!-- Header -->
    <h1 class="text-[0.875rem] font-semibold text-text-primary">Summary</h1>

    <!-- Error -->
    <div v-if="summaryStore.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ summaryStore.error }}
    </div>

    <!-- Loading -->
    <div v-if="summaryStore.loading && !summary" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading summary...
    </div>

    <template v-else>

      <!-- ===== TOP SECTION — 7 Cards ===== -->
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">

        <!-- Card 1: Client Information -->
        <div v-if="client" class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Client Information</h2>
          <div class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
            <span class="text-[0.75rem] text-text-muted">First Name</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.firstName || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">Last Name</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.lastName || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">Company Name</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.companyName || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">Email Address</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.email || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">Phone Number</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.phone || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">Address 1</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.street || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">Address 2</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.address2 || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">City</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.city || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">State/Region</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.state || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">Postcode</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.postCode || '\u2014' }}</span>
            <span class="text-[0.75rem] text-text-muted">Country</span>
            <span class="text-[0.82rem] text-text-primary">{{ client.country || '\u2014' }}</span>
          </div>
        </div>

        <!-- Card 2: Other Information -->
        <div v-if="client" class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Other Information</h2>
          <div class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
            <span class="text-[0.75rem] text-text-muted">Status</span>
            <span>
              <span
                class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
                :class="CLIENT_STATUS_STYLES[client.status] ?? 'text-text-muted bg-white/[0.04] border-border'"
              >
                {{ client.status }}
              </span>
            </span>
            <span class="text-[0.75rem] text-text-muted">Signup Date</span>
            <span class="text-[0.82rem] text-text-primary">{{ formatDate(client.createdAt) }}</span>
            <span class="text-[0.75rem] text-text-muted">Client For</span>
            <span class="text-[0.82rem] text-text-primary">{{ computeDuration(client.createdAt) }}</span>
            <span class="text-[0.75rem] text-text-muted">Tax Exempt</span>
            <span class="text-[0.82rem] font-medium" :class="client.taxExempt ? 'text-status-green' : 'text-status-red'">
              {{ client.taxExempt ? 'Yes' : 'No' }}
            </span>
            <span class="text-[0.75rem] text-text-muted">Auto CC Processing</span>
            <span class="text-[0.82rem] font-medium" :class="!client.disableCcProcessing ? 'text-status-green' : 'text-status-red'">
              {{ !client.disableCcProcessing ? 'Yes' : 'No' }}
            </span>
            <span class="text-[0.75rem] text-text-muted">Overdue Reminders</span>
            <span class="text-[0.82rem] font-medium" :class="client.overdueNotices ? 'text-status-green' : 'text-status-red'">
              {{ client.overdueNotices ? 'Yes' : 'No' }}
            </span>
            <span class="text-[0.75rem] text-text-muted">Late Fees</span>
            <span class="text-[0.82rem] font-medium" :class="client.lateFees ? 'text-status-green' : 'text-status-red'">
              {{ client.lateFees ? 'Yes' : 'No' }}
            </span>
            <span class="text-[0.75rem] text-text-muted">Two-Factor Auth</span>
            <span class="text-[0.82rem] font-medium" :class="client.twoFactorEnabled ? 'text-status-green' : 'text-status-red'">
              {{ client.twoFactorEnabled ? 'Enabled' : 'Disabled' }}
            </span>
          </div>
        </div>

        <!-- Card 3: Contacts -->
        <div v-if="client" class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Contacts</h2>
          <div v-if="client.contacts.length > 0" class="space-y-3">
            <div v-for="contact in client.contacts" :key="contact.id" class="flex items-center justify-between gap-2">
              <div class="min-w-0">
                <p class="text-[0.82rem] text-text-primary truncate">{{ contact.firstName }} {{ contact.lastName }}</p>
                <p class="text-[0.75rem] text-text-muted truncate">{{ contact.email }}</p>
              </div>
              <span
                class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border shrink-0"
                :class="CONTACT_TYPE_STYLES[contact.type] ?? 'text-text-muted bg-white/[0.04] border-border'"
              >
                {{ contact.type }}
              </span>
            </div>
          </div>
          <p v-else class="text-[0.82rem] text-text-muted">No additional contacts</p>
          <RouterLink
            :to="`/clients/${clientId}/contacts`"
            class="inline-block mt-4 text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
          >
            View All &rarr;
          </RouterLink>
        </div>

        <!-- Card 4: Invoices / Billing -->
        <div v-if="summary" class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Invoices / Billing</h2>

          <!-- Invoice status rows -->
          <div class="space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Paid</span>
              <div class="flex items-center gap-3">
                <span class="text-[0.82rem] text-text-secondary">{{ summary.paid.count }}</span>
                <span class="text-[0.82rem] text-text-primary font-medium w-24 text-right">{{ formatCurrency(summary.paid.total) }}</span>
              </div>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Draft</span>
              <div class="flex items-center gap-3">
                <span class="text-[0.82rem] text-text-secondary">{{ summary.draft.count }}</span>
                <span class="text-[0.82rem] text-text-primary font-medium w-24 text-right">{{ formatCurrency(summary.draft.total) }}</span>
              </div>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Unpaid/Due</span>
              <div class="flex items-center gap-3">
                <span class="text-[0.82rem] text-text-secondary">{{ summary.unpaid.count }}</span>
                <span class="text-[0.82rem] text-text-primary font-medium w-24 text-right">{{ formatCurrency(summary.unpaid.total) }}</span>
              </div>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Overdue</span>
              <div class="flex items-center gap-3">
                <span class="text-[0.82rem] text-text-secondary">{{ summary.overdue.count }}</span>
                <span class="text-[0.82rem] text-text-primary font-medium w-24 text-right">{{ formatCurrency(summary.overdue.total) }}</span>
              </div>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Cancelled</span>
              <div class="flex items-center gap-3">
                <span class="text-[0.82rem] text-text-secondary">{{ summary.cancelled.count }}</span>
                <span class="text-[0.82rem] text-text-primary font-medium w-24 text-right">{{ formatCurrency(summary.cancelled.total) }}</span>
              </div>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Refunded</span>
              <div class="flex items-center gap-3">
                <span class="text-[0.82rem] text-text-secondary">{{ summary.refunded.count }}</span>
                <span class="text-[0.82rem] text-text-primary font-medium w-24 text-right">{{ formatCurrency(summary.refunded.total) }}</span>
              </div>
            </div>
          </div>

          <hr class="border-border my-3">

          <!-- Income section -->
          <div class="space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Gross Revenue</span>
              <span class="text-[0.82rem] text-text-primary font-medium">{{ formatCurrency(summary.grossRevenue) }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Client Expenses</span>
              <span class="text-[0.82rem] text-text-primary font-medium">{{ formatCurrency(summary.clientExpenses) }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Net Income</span>
              <span class="text-[0.82rem] text-text-primary font-medium">{{ formatCurrency(summary.netIncome) }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Credit Balance</span>
              <span class="text-[0.82rem] text-text-primary font-medium">{{ formatCurrency(summary.creditBalance) }}</span>
            </div>
          </div>

          <hr class="border-border my-3">

          <!-- Quick actions -->
          <div class="flex flex-wrap gap-3">
            <RouterLink
              :to="`/clients/${clientId}/invoices`"
              class="text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
            >
              Create Invoice
            </RouterLink>
            <RouterLink
              :to="`/clients/${clientId}/billable-items`"
              class="text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
            >
              Add Billable Item
            </RouterLink>
            <RouterLink
              :to="`/billing/quotes/add?clientId=${clientId}`"
              class="text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
            >
              Create New Quote
            </RouterLink>
          </div>
        </div>

        <!-- Card 5: Products / Services & Counts -->
        <div v-if="summary" class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Products / Services</h2>
          <div class="space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Active Services</span>
              <span class="text-[0.82rem] text-text-primary font-medium">{{ summary.activeServicesCount }} ({{ summary.totalServicesCount }} Total)</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Domains</span>
              <span class="text-[0.82rem] text-text-primary font-medium">{{ summary.totalDomainsCount }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Accepted Quotes</span>
              <span class="text-[0.82rem] text-text-primary font-medium">{{ summary.acceptedQuotesCount }} ({{ summary.totalQuotesCount }} Total)</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-[0.75rem] text-text-muted">Open Tickets</span>
              <span class="text-[0.82rem] text-text-primary font-medium">{{ summary.openTicketsCount }} ({{ summary.totalTicketsCount }} Total)</span>
            </div>
          </div>

          <hr class="border-border my-3">

          <div class="flex flex-wrap gap-3">
            <RouterLink
              :to="`/clients/${clientId}/tickets`"
              class="text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
            >
              View all Tickets
            </RouterLink>
            <RouterLink
              :to="`/clients/${clientId}/tickets/new`"
              class="text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
            >
              Open New Ticket
            </RouterLink>
          </div>
        </div>

        <!-- Card 6: Recent Emails -->
        <div v-if="summary" class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Recent Emails</h2>
          <div v-if="summary.recentEmails.length > 0" class="space-y-2">
            <div
              v-for="email in summary.recentEmails"
              :key="email.id"
              class="flex items-start gap-3"
            >
              <span class="text-[0.75rem] text-text-muted shrink-0 mt-0.5">{{ formatDate(email.sentAt) }}</span>
              <span class="text-[0.82rem] text-text-primary truncate">{{ email.subject }}</span>
            </div>
          </div>
          <p v-else class="text-[0.82rem] text-text-muted">No emails sent</p>
          <RouterLink
            :to="`/clients/${clientId}/emails`"
            class="inline-block mt-4 text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
          >
            View All &rarr;
          </RouterLink>
        </div>

        <!-- Card 7: Admin Notes -->
        <div v-if="client" class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Admin Notes</h2>
          <textarea
            v-model="adminNotes"
            rows="6"
            class="w-full bg-surface-input border border-border rounded-xl px-3 py-2 text-[0.82rem] text-text-primary placeholder:text-text-muted/50 focus:outline-none focus:border-primary-500/40 resize-none"
            placeholder="Add notes about this client..."
          />
          <button
            type="button"
            class="mt-3 gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            :disabled="savingNotes"
            @click="saveNotes"
          >
            {{ savingNotes ? 'Saving...' : 'Save' }}
          </button>
        </div>
      </div>

      <!-- ===== BOTTOM SECTION — 3 Mini Tables ===== -->

      <!-- Table 1: Products/Services -->
      <div>
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-[0.82rem] font-semibold text-text-primary">Products/Services</h2>
          <RouterLink
            :to="`/clients/${clientId}/services`"
            class="text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
          >
            View All &rarr;
          </RouterLink>
        </div>
        <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
          <!-- Header row -->
          <div class="hidden sm:grid grid-cols-[0.4fr_1.5fr_1.5fr_1fr_0.8fr_1fr_0.7fr_50px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Product/Service</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Price</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Billing Cycle</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Next Due</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
          </div>
          <!-- Rows -->
          <div
            v-for="service in summaryStore.services"
            :key="service.id"
            class="grid grid-cols-1 sm:grid-cols-[0.4fr_1.5fr_1.5fr_1fr_0.8fr_1fr_0.7fr_50px] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors cursor-pointer"
            @click="router.push(`/clients/${clientId}/services/${service.id}`)"
          >
            <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ service.id }}</span>
            <span class="text-[0.875rem] font-medium text-text-primary truncate">{{ service.productName }}</span>
            <span class="text-[0.82rem] text-text-secondary truncate">{{ service.domain ?? '\u2014' }}</span>
            <span class="text-[0.82rem] text-text-secondary">{{ formatPrice(service.price, service.priceCurrency) }}</span>
            <span class="text-[0.82rem] text-text-secondary">{{ capitalizeCycle(service.billingCycle) }}</span>
            <span class="text-[0.82rem] text-text-muted">{{ formatDate(service.nextDueDate) }}</span>
            <span
              class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
              :class="SERVICE_STATUS_STYLES[service.status] ?? SERVICE_STATUS_STYLES.Terminated"
            >
              {{ service.status }}
            </span>
            <span class="flex items-center">
              <button
                type="button"
                class="text-text-muted hover:text-text-primary transition-colors"
                @click.stop="router.push(`/clients/${clientId}/services/${service.id}`)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
                  <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
                </svg>
              </button>
            </span>
          </div>
          <!-- Empty -->
          <div v-if="summaryStore.services.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
            <p class="text-[0.82rem] text-text-muted">No services found</p>
          </div>
        </div>
      </div>

      <!-- Table 2: Domains -->
      <div>
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-[0.82rem] font-semibold text-text-primary">Domains</h2>
          <RouterLink
            :to="`/clients/${clientId}/domains`"
            class="text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
          >
            View All &rarr;
          </RouterLink>
        </div>
        <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
          <!-- Header row -->
          <div class="hidden sm:grid grid-cols-[0.4fr_1.5fr_1fr_0.8fr_1fr_0.7fr_50px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain Name</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Registrar</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Expiry Date</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Auto-Renew</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
          </div>
          <!-- Rows -->
          <div
            v-for="domain in summaryStore.domains"
            :key="domain.id"
            class="grid grid-cols-1 sm:grid-cols-[0.4fr_1.5fr_1fr_0.8fr_1fr_0.7fr_50px] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors cursor-pointer"
            @click="router.push(`/clients/${clientId}/domains/${domain.id}`)"
          >
            <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ domain.id }}</span>
            <span class="text-[0.875rem] font-medium text-text-primary truncate">{{ domain.name }}</span>
            <span class="text-[0.82rem] text-text-secondary truncate">{{ domain.registrar ?? '\u2014' }}</span>
            <span
              class="inline-flex px-2 py-0.5 rounded-full text-[0.68rem] font-semibold border w-fit"
              :class="DOMAIN_STATUS_STYLES[domain.status] ?? DOMAIN_STATUS_STYLES.Cancelled"
            >
              {{ domain.status }}
            </span>
            <span class="text-[0.82rem] text-text-muted">{{ formatDate(domain.expiresAt) }}</span>
            <span
              class="text-[0.82rem] font-medium"
              :class="domain.autoRenew ? 'text-status-green' : 'text-text-muted'"
            >
              {{ domain.autoRenew ? 'Yes' : 'No' }}
            </span>
            <span class="flex items-center">
              <button
                type="button"
                class="text-text-muted hover:text-text-primary transition-colors"
                @click.stop="router.push(`/clients/${clientId}/domains/${domain.id}`)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
                  <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
                </svg>
              </button>
            </span>
          </div>
          <!-- Empty -->
          <div v-if="summaryStore.domains.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
            <p class="text-[0.82rem] text-text-muted">No domains found</p>
          </div>
        </div>
      </div>

      <!-- Table 3: Current Quotes -->
      <div>
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-[0.82rem] font-semibold text-text-primary">Current Quotes</h2>
          <RouterLink
            :to="`/clients/${clientId}/quotes`"
            class="text-primary-400 hover:text-primary-300 text-[0.78rem] font-medium"
          >
            View All &rarr;
          </RouterLink>
        </div>
        <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
          <!-- Header row -->
          <div class="hidden sm:grid grid-cols-[0.4fr_1fr_0.7fr_0.7fr_0.6fr_0.6fr_50px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
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
            v-for="quote in summaryStore.quotes"
            :key="quote.id"
            class="grid grid-cols-1 sm:grid-cols-[0.4fr_1fr_0.7fr_0.7fr_0.6fr_0.6fr_50px] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors cursor-pointer"
            @click="router.push(`/billing/quotes/${quote.id}`)"
          >
            <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">#{{ quote.id }}</span>
            <span class="text-[0.82rem] text-text-primary font-medium hidden sm:block truncate">{{ quote.subject }}</span>
            <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ formatDate(quote.dateCreated) }}</span>
            <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ formatDate(quote.validUntil) }}</span>
            <span class="text-[0.82rem] text-text-primary font-medium hidden sm:block">${{ quote.total.toFixed(2) }}</span>
            <span class="hidden sm:block">
              <span
                class="inline-block px-2 py-0.5 rounded-full text-[0.7rem] font-medium border"
                :class="QUOTE_STAGE_STYLES[quote.stage] ?? 'text-text-muted bg-white/[0.04] border-border'"
              >
                {{ stageLabel(quote.stage) }}
              </span>
            </span>
            <span class="hidden sm:flex items-center">
              <button
                type="button"
                class="text-text-muted hover:text-text-primary transition-colors"
                @click.stop="router.push(`/billing/quotes/${quote.id}`)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
                  <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
                </svg>
              </button>
            </span>
          </div>
          <!-- Empty -->
          <div v-if="summaryStore.quotes.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
            <p class="text-[0.82rem] text-text-muted">No quotes found</p>
          </div>
        </div>
      </div>

    </template>
  </div>
</template>
