<script setup lang="ts">
/**
 * Quote detail page -- displays full quote data with editable fields,
 * line items table, notes sections, and action buttons.
 * Supports both create (new) and edit modes.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuoteStore } from '../stores/quoteStore'
import { QUOTE_STAGE_OPTIONS, QUOTE_STAGE_STYLES } from '../../../utils/constants'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'
import type { QuoteStage } from '../../../types/models'

const route = useRoute()
const router = useRouter()
const store = useQuoteStore()

/** Whether this is a new quote (create mode). */
const isNew = computed(() => route.params.id === undefined || route.path === '/quotes/new')

/** Quote ID from route params (0 when creating). */
const quoteId = computed(() => isNew.value ? 0 : Number(route.params.id))

/** Client ID from query param (for new quotes). */
const queryClientId = computed(() => route.query.clientId as string | undefined)

// ---- Form state ----

/** Quote subject. */
const subject = ref('')

/** Quote stage. */
const stage = ref<QuoteStage>('Draft')

/** Date created (YYYY-MM-DD). */
const dateCreated = ref('')

/** Valid until (YYYY-MM-DD). */
const validUntil = ref('')

/** Proposal text. */
const proposalText = ref('')

/** Customer notes. */
const customerNotes = ref('')

/** Admin-only notes. */
const adminNotes = ref('')

/** Editable line items. */
const items = ref<Array<{
  id?: number
  quantity: number
  description: string
  unitPrice: number
  discountPercent: number
  taxed: boolean
}>>([])

/** Whether the delete confirm modal is visible. */
const showDeleteModal = ref(false)

/** Whether the convert confirm modal is visible. */
const showConvertModal = ref(false)

/** Stage options without the "All Stages" entry. */
const stageOptions = computed(() => QUOTE_STAGE_OPTIONS.filter(o => o.value !== ''))

/**
 * Computes the line total for a single item.
 *
 * @param item - The line item.
 * @returns Computed total after discount.
 */
function lineTotal(item: { quantity: number; unitPrice: number; discountPercent: number }): number {
  const base = item.quantity * item.unitPrice
  const discount = base * (item.discountPercent / 100)
  return base - discount
}

/** Sub-total: sum of all line totals. */
const subTotal = computed(() => items.value.reduce((sum, item) => sum + lineTotal(item), 0))

/** Total due (same as sub-total in this implementation). */
const totalDue = computed(() => subTotal.value)

/**
 * Returns the CSS class for a stage badge.
 *
 * @param s - The quote stage.
 * @returns Tailwind class string.
 */
function stageClass(s: string): string {
  return QUOTE_STAGE_STYLES[s] ?? 'text-text-muted bg-white/[0.04] border-border'
}

/**
 * Returns a human-readable label for a stage value.
 *
 * @param s - The quote stage.
 * @returns Display label.
 */
function stageLabel(s: string): string {
  if (s === 'OnHold') return 'On Hold'
  return s
}

/** Adds a blank line item. */
function addItem(): void {
  items.value.push({ quantity: 1, description: '', unitPrice: 0, discountPercent: 0, taxed: false })
}

/**
 * Removes a line item by index.
 *
 * @param index - Index to remove.
 */
function removeItem(index: number): void {
  items.value.splice(index, 1)
}

/**
 * Populates form fields from the loaded quote.
 */
function populateForm(): void {
  const q = store.currentQuote
  if (!q) return
  subject.value = q.subject
  stage.value = q.stage
  dateCreated.value = q.dateCreated ? q.dateCreated.split('T')[0] : ''
  validUntil.value = q.validUntil ? q.validUntil.split('T')[0] : ''
  proposalText.value = q.proposalText ?? ''
  customerNotes.value = q.customerNotes ?? ''
  adminNotes.value = q.adminNotes ?? ''
  items.value = q.items.map(i => ({
    id: i.id,
    quantity: i.quantity,
    description: i.description,
    unitPrice: i.unitPrice,
    discountPercent: i.discountPercent,
    taxed: i.taxed,
  }))
}

/**
 * Saves the quote (create or update).
 *
 * @returns Promise that resolves when save completes.
 */
async function handleSave(): Promise<void> {
  store.successMessage = null
  if (isNew.value) {
    const cid = Number(queryClientId.value || 0)
    if (!cid) {
      store.error = 'Client ID is required.'
      return
    }
    const id = await store.createQuote({
      clientId: cid,
      subject: subject.value,
      stage: stage.value,
      validUntil: validUntil.value,
      proposalText: proposalText.value || undefined,
      customerNotes: customerNotes.value || undefined,
      adminNotes: adminNotes.value || undefined,
      items: items.value.map(i => ({
        quantity: i.quantity,
        description: i.description,
        unitPrice: i.unitPrice,
        discountPercent: i.discountPercent,
        taxed: i.taxed,
      })),
    })
    if (id) {
      router.push(`/billing/quotes/${id}`)
    }
  } else {
    await store.updateQuote(quoteId.value, {
      subject: subject.value,
      stage: stage.value,
      validUntil: validUntil.value,
      proposalText: proposalText.value || undefined,
      customerNotes: customerNotes.value || undefined,
      adminNotes: adminNotes.value || undefined,
      items: items.value.map(i => ({
        id: i.id,
        quantity: i.quantity,
        description: i.description,
        unitPrice: i.unitPrice,
        discountPercent: i.discountPercent,
        taxed: i.taxed,
      })),
    })
  }
}

/**
 * Duplicates the current quote and navigates to the copy.
 *
 * @returns Promise that resolves when duplication completes.
 */
async function handleDuplicate(): Promise<void> {
  store.successMessage = null
  const newId = await store.duplicateQuote(quoteId.value)
  if (newId) {
    router.push(`/billing/quotes/${newId}`)
  }
}

/**
 * Converts the current quote to an invoice and navigates to it.
 *
 * @returns Promise that resolves when conversion completes.
 */
async function handleConvert(): Promise<void> {
  showConvertModal.value = false
  const invoiceId = await store.convertToInvoice(quoteId.value)
  if (invoiceId) {
    router.push(`/billing/${invoiceId}`)
  }
}

/**
 * Deletes the current quote and navigates back.
 *
 * @returns Promise that resolves when deletion completes.
 */
async function handleDelete(): Promise<void> {
  showDeleteModal.value = false
  await store.deleteQuote(quoteId.value)
  if (!store.error) {
    router.back()
  }
}

/** Navigates back to the previous page. */
function goBack(): void {
  router.back()
}

// Load existing quote when editing
watch(quoteId, (id) => {
  if (id > 0) {
    store.fetchById(id)
  }
})

// Populate form when quote loads
watch(() => store.currentQuote, () => {
  populateForm()
})

onMounted(() => {
  if (!isNew.value && quoteId.value > 0) {
    store.fetchById(quoteId.value)
  } else {
    // Initialize with one blank item for new quotes
    const today = new Date()
    dateCreated.value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`
    items.value = [{ quantity: 1, description: '', unitPrice: 0, discountPercent: 0, taxed: false }]
  }
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Loading -->
    <div v-if="store.loading && !store.currentQuote && !isNew" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading quote...
    </div>

    <!-- Error (only when no data) -->
    <div v-else-if="store.error && !store.currentQuote && !isNew" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ store.error }}
    </div>

    <template v-else>

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
            {{ isNew ? 'New Quote' : `Quote #${quoteId}` }}
          </h1>
          <span
            v-if="!isNew"
            class="inline-block px-2.5 py-0.5 rounded-full text-[0.7rem] font-medium border"
            :class="stageClass(stage)"
          >
            {{ stageLabel(stage) }}
          </span>
        </div>

        <!-- Success feedback -->
        <div v-if="store.successMessage" class="px-4 py-2.5 text-[0.82rem] text-status-green bg-status-green/10 border border-status-green/20 rounded-xl">
          {{ store.successMessage }}
        </div>

        <!-- Error feedback -->
        <div v-if="store.error" class="px-4 py-2.5 text-[0.82rem] text-status-red bg-status-red/10 border border-status-red/20 rounded-xl">
          {{ store.error }}
        </div>
      </div>

      <!-- General Information Card -->
      <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
        <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">General Information</h2>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Subject</label>
            <input
              v-model="subject"
              type="text"
              placeholder="Quote subject"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Stage</label>
            <AppSelect v-model="stage" :options="stageOptions" placeholder="Select stage" />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Date Created</label>
            <AppDatePicker v-model="dateCreated" :disabled="!isNew" />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Valid Until</label>
            <AppDatePicker v-model="validUntil" />
          </div>
        </div>
      </div>

      <!-- Action Buttons (top) -->
      <div class="flex items-center gap-2.5 mb-5 flex-wrap">
        <button
          type="button"
          :disabled="store.loading"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="handleSave"
        >
          {{ store.loading ? 'Saving...' : 'Save Changes' }}
        </button>
        <template v-if="!isNew">
          <button
            type="button"
            :disabled="store.loading"
            class="px-4 py-2 text-[0.82rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors disabled:opacity-50"
            @click="handleDuplicate"
          >
            Duplicate
          </button>
          <button
            type="button"
            :disabled="store.loading"
            class="px-4 py-2 text-[0.82rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-[10px] hover:bg-primary-500/20 transition-colors disabled:opacity-50"
            @click="showConvertModal = true"
          >
            Convert to Invoice
          </button>
          <button
            type="button"
            :disabled="store.loading"
            class="px-4 py-2 text-[0.82rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-[10px] hover:bg-status-red/20 transition-colors disabled:opacity-50"
            @click="showDeleteModal = true"
          >
            Delete
          </button>
        </template>
      </div>

      <!-- Line Items -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-5">
        <div class="px-5 py-3 border-b border-border bg-white/[0.02]">
          <h2 class="text-[0.82rem] font-semibold text-text-primary">Line Items</h2>
        </div>

        <!-- Items header -->
        <div class="hidden sm:grid grid-cols-[60px_1fr_100px_100px_60px_80px_40px] gap-3 px-5 py-2.5 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Qty</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Unit Price</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Discount %</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Taxed</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-right">Total</span>
          <span />
        </div>

        <!-- Item rows -->
        <div
          v-for="(item, idx) in items"
          :key="idx"
          class="grid grid-cols-1 sm:grid-cols-[60px_1fr_100px_100px_60px_80px_40px] gap-2 sm:gap-3 px-5 py-3 border-b border-border last:border-0"
        >
          <div>
            <AppNumberInput v-model="item.quantity" :min="1" placeholder="1" />
          </div>
          <div>
            <textarea
              v-model="item.description"
              rows="1"
              placeholder="Description"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none"
            />
          </div>
          <div>
            <AppNumberInput v-model="item.unitPrice" :step="0.01" :min="0" placeholder="0.00" />
          </div>
          <div>
            <AppNumberInput v-model="item.discountPercent" :step="1" :min="0" :max="100" placeholder="0" />
          </div>
          <div class="flex items-center justify-center">
            <AppCheckbox v-model="item.taxed" />
          </div>
          <div class="flex items-center justify-end">
            <span class="text-[0.82rem] text-text-primary font-medium">${{ lineTotal(item).toFixed(2) }}</span>
          </div>
          <div class="flex items-center justify-center">
            <button
              v-if="items.length > 1"
              type="button"
              class="text-status-red hover:text-status-red/80 transition-colors"
              @click="removeItem(idx)"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="3 6 5 6 21 6" />
                <path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
              </svg>
            </button>
          </div>
        </div>

        <!-- Add item button -->
        <div class="px-5 py-3 border-t border-border">
          <button
            type="button"
            class="text-primary-400 text-[0.82rem] hover:underline"
            @click="addItem"
          >
            + Add Item
          </button>
        </div>

        <!-- Totals -->
        <div class="border-t border-border px-5 py-3 space-y-1.5">
          <div class="flex items-center justify-end gap-4">
            <span class="text-[0.78rem] text-text-muted">Sub Total</span>
            <span class="text-[0.82rem] text-text-primary font-medium w-24 text-right">${{ subTotal.toFixed(2) }}</span>
          </div>
          <div class="flex items-center justify-end gap-4">
            <span class="text-[0.82rem] text-text-primary font-semibold">Total Due</span>
            <span class="text-[0.9rem] text-text-primary font-bold w-24 text-right">${{ totalDue.toFixed(2) }}</span>
          </div>
        </div>
      </div>

      <!-- Notes Section -->
      <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
        <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Notes</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Proposal Text</label>
            <textarea
              v-model="proposalText"
              rows="4"
              placeholder="Proposal text visible to the client..."
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
            />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Customer Notes</label>
            <textarea
              v-model="customerNotes"
              rows="3"
              placeholder="Notes visible to the customer..."
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
            />
          </div>
          <div>
            <label class="block text-[0.75rem] text-text-muted mb-1.5">Admin Only Notes</label>
            <textarea
              v-model="adminNotes"
              rows="3"
              placeholder="Internal notes (not visible to the client)..."
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
            />
          </div>
        </div>
      </div>

      <!-- Action Buttons (bottom) -->
      <div class="flex items-center gap-2.5 flex-wrap">
        <button
          type="button"
          :disabled="store.loading"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="handleSave"
        >
          {{ store.loading ? 'Saving...' : 'Save Changes' }}
        </button>
        <template v-if="!isNew">
          <button
            type="button"
            :disabled="store.loading"
            class="px-4 py-2 text-[0.82rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors disabled:opacity-50"
            @click="handleDuplicate"
          >
            Duplicate
          </button>
          <button
            type="button"
            :disabled="store.loading"
            class="px-4 py-2 text-[0.82rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-[10px] hover:bg-primary-500/20 transition-colors disabled:opacity-50"
            @click="showConvertModal = true"
          >
            Convert to Invoice
          </button>
          <button
            type="button"
            :disabled="store.loading"
            class="px-4 py-2 text-[0.82rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-[10px] hover:bg-status-red/20 transition-colors disabled:opacity-50"
            @click="showDeleteModal = true"
          >
            Delete
          </button>
        </template>
      </div>

    </template>

    <!-- Delete Confirm Modal -->
    <ConfirmModal
      v-if="showDeleteModal"
      title="Delete Quote"
      :message="`Are you sure you want to delete quote #${quoteId}? This action cannot be undone.`"
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="store.loading"
      variant="danger"
      @confirm="handleDelete"
      @close="showDeleteModal = false"
    />

    <!-- Convert Confirm Modal -->
    <ConfirmModal
      v-if="showConvertModal"
      title="Convert to Invoice"
      :message="`Convert quote #${quoteId} to an invoice? The quote will remain unchanged.`"
      confirm-label="Convert"
      loading-label="Converting..."
      :loading="store.loading"
      variant="primary"
      @confirm="handleConvert"
      @close="showConvertModal = false"
    />
  </div>
</template>
