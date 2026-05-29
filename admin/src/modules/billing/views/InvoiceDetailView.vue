<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useBillingStore } from '../stores/billingStore'
import AppSelect from '../../../components/AppSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'

const router = useRouter()
const route = useRoute()
const store = useBillingStore()

const activeTab = ref<'summary' | 'payment' | 'options' | 'credit' | 'refund' | 'notes'>('summary')
const isManageMode = ref(route.params.action === 'edit')
const selectedEvent = ref('Invoice Payment Confirmation')
const invoiceId = computed(() => Number(route.params.id))

// Summary Payment Method
const summaryPaymentMethod = ref('')

// Options form state
const optionsInvoiceDate = ref<string>(new Date().toISOString().split('T')[0])
const optionsDueDate = ref<string>(new Date().toISOString().split('T')[0])
const optionsPaymentMethod = ref('')
const optionsTaxRate1 = ref(0)
const optionsTaxRate1Percent = ref(0)
const optionsTaxRate2 = ref(0)
const optionsTaxRate2Percent = ref(0)
const optionsInvoiceNumber = ref('')
const optionsStatus = ref('Draft')

// Add Payment form state
const paymentDate = ref<string>(new Date().toISOString().split('T')[0])
const paymentMethod = ref('')
const transactionId = ref('')
const paymentAmount = ref(0)
const transactionFees = ref(0)
const sendPaymentEmail = ref(true)

// Credit form state
const addCreditAmount = ref(0)
const removeCreditAmount = ref(0)

// Refund form state
const refundTransactionId = ref('')
const refundAmount = ref(0)
const refundType = ref('Full Refund')
const reversePayment = ref(false)
const sendRefundEmail = ref(true)
const createCredit = ref(false)
const noRefundCredit = ref(false)

// Invoice Items edit state
const editableItems = ref<any[]>([])
const selectedItemIds = ref<Set<number>>(new Set())
const itemAction = ref('With Selected')
const deleteConfirmItemId = ref<number | null>(null)

const invoice = computed(() => {
  return store.invoices.find(inv => inv.id === invoiceId.value)
})

const isPaymentMethodEditable = computed(() => {
  return invoice.value?.status && !['Paid', 'Cancelled'].includes(invoice.value.status)
})

const eventOptions = [
  'Invoice Payment Confirmation',
  'Credit Card Payment Confirmation',
  'Invoice Created',
  'Credit Card Invoice Created',
  'Invoice Payment Reminder',
  'First Invoice Overdue Notice',
  'Second Invoice Overdue Notice',
  'Third Invoice Overdue Notice',
  'Credit Card Payment Due',
  'Credit Card Payment Failed',
  'Invoice Refund Confirmation',
  'Direct Debit Payment Failed',
  'Direct Debit Payment Confirmation',
  'Direct Debit Payment Pending',
  'Credit Card Payment Pending',
  'Invoice Modified',
]

const eventSelectOptions = computed(() =>
  eventOptions.map(option => ({ value: option, label: option }))
)

const paymentMethodOptions = [
  { value: 'Stripe', label: 'Stripe' },
  { value: 'PayPal', label: 'PayPal' },
  { value: 'Credit/Debit Card', label: 'Credit/Debit Card' },
]

const refundTypeOptions = [
  { value: 'Full Refund', label: 'Full Refund' },
  { value: 'Partial Refund', label: 'Partial Refund' },
  { value: 'Credit Only', label: 'Credit Only' },
]

const transactionOptions = computed(() => {
  if (!invoice.value?.transactions || invoice.value.transactions.length === 0) {
    return []
  }
  return invoice.value.transactions.map((tx: any) => ({
    value: tx.id,
    label: `${new Date(tx.createdAt).toLocaleDateString()} | ${tx.amount.toFixed(2)} USD`
  }))
})

const tabs = computed(() => {
  const allTabs = [
    { id: 'summary', label: 'Summary' },
    { id: 'payment', label: 'Add Payment' },
    { id: 'options', label: 'Options', manageOnly: true },
    { id: 'credit', label: 'Credit' },
    { id: 'refund', label: 'Refund' },
    { id: 'notes', label: 'Notes' },
  ] as const
  return isManageMode.value ? allTabs : allTabs.filter(t => !('manageOnly' in t && t.manageOnly))
})

function goBack() {
  router.back()
}

function toggleManageView() {
  isManageMode.value = !isManageMode.value
  if (isManageMode.value) {
    // Инициализировать editable items
    const items = (invoice.value?.items || []).map((item: any, idx: number) => ({
      ...item,
      _tempId: idx
    }))
    // Добавить одну пустую строку
    items.push({
      _tempId: Math.max(...items.map((i: any) => i._tempId || 0), -1) + 1,
      description: '',
      amount: 0,
      taxed: false,
      quantity: 1
    })
    editableItems.value = items
  }
}

function addItem() {
  const newId = Math.max(...editableItems.value.map(i => i._tempId || 0), -1) + 1
  editableItems.value.push({
    _tempId: newId,
    description: '',
    amount: 0,
    taxed: false,
    quantity: 1
  })
}

function showDeleteConfirm(tempId: number) {
  deleteConfirmItemId.value = tempId
}

function confirmDelete() {
  if (deleteConfirmItemId.value !== null) {
    editableItems.value = editableItems.value.filter(item => item._tempId !== deleteConfirmItemId.value)
    selectedItemIds.value.delete(deleteConfirmItemId.value)
    deleteConfirmItemId.value = null
  }
}

function cancelDelete() {
  deleteConfirmItemId.value = null
}

function toggleItemSelection(tempId: number) {
  if (selectedItemIds.value.has(tempId)) {
    selectedItemIds.value.delete(tempId)
  } else {
    selectedItemIds.value.add(tempId)
  }
}

function saveChanges() {
  // TODO: Сохранить изменения через API
  isManageMode.value = false
  console.log('Save changes:', editableItems.value)
}

function cancelChanges() {
  isManageMode.value = false
  editableItems.value = []
  selectedItemIds.value.clear()
}

function print() {
  window.print()
}

function download() {
  // TODO: implement PDF download
  console.log('Download invoice')
}

onMounted(() => {
  // Загрузить счета если они еще не загружены
  if (store.invoices.length === 0) {
    store.fetchAll(1, store.pageSize)
  }
  // Инициализировать Payment Method
  summaryPaymentMethod.value = invoice.value?.gateway || ''
  // Инициализировать editable items если в режиме edit
  if (isManageMode.value) {
    const items = (invoice.value?.items || []).map((item: any, idx: number) => ({
      ...item,
      _tempId: idx
    }))
    // Добавить одну пустую строку
    items.push({
      _tempId: Math.max(...items.map((i: any) => i._tempId || 0), -1) + 1,
      description: '',
      amount: 0,
      taxed: false,
      quantity: 1
    })
    editableItems.value = items
  }
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div class="flex items-center gap-4">
        <button
          @click="goBack"
          class="p-2 text-text-muted hover:text-text-primary hover:bg-white/[0.05] rounded transition-colors"
          title="Back"
        >
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M19 12H5M12 19l-7-7 7-7" />
          </svg>
        </button>
        <div>
          <h1 class="font-display font-bold text-[1.25rem] text-text-primary">Invoice #{{ invoice?.id }}</h1>
          <p class="text-[0.78rem] text-text-secondary mt-1">Manage and view invoice details</p>
        </div>
      </div>

      <!-- Action Buttons -->
      <div class="flex items-center gap-2">
        <button
          @click="toggleManageView"
          class="px-4 py-2 text-[0.82rem] font-semibold text-white gradient-brand rounded-[9px] transition-opacity hover:opacity-90"
        >
          {{ isManageMode ? 'View Invoice' : 'Manage Invoice' }}
        </button>
        <button
          class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.05] transition-colors"
        >
          View as Client
        </button>
        <button
          @click="print"
          class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.05] transition-colors"
        >
          Print
        </button>
        <button
          @click="download"
          class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.05] transition-colors"
        >
          Download
        </button>
      </div>
    </div>

    <!-- Draft Invoice Notice (before tabs) -->
    <div v-if="isManageMode && invoice?.status === 'Draft'" class="mb-6 bg-status-blue/10 border border-status-blue/20 rounded-lg p-4">
      <p class="text-[0.82rem] text-text-secondary">
        <strong>This is a Draft Invoice.</strong> The client is not able to see or access this invoice until it is published.
      </p>
    </div>

    <!-- Tabs -->
    <div class="mb-6 border-b border-border">
      <div class="flex gap-8">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          @click="activeTab = tab.id"
          :class="[
            'px-2 py-3 text-[0.82rem] font-semibold text-text-secondary border-b-2 transition-colors',
            activeTab === tab.id
              ? 'border-primary-500 text-text-primary'
              : 'border-transparent hover:text-text-primary'
          ]"
        >
          {{ tab.label }}
        </button>
      </div>
    </div>

    <!-- Tab Content -->
    <div v-if="activeTab === 'summary'" class="space-y-6">
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Left Column - Invoice Details -->
        <div class="bg-white/[0.03] border border-border rounded-lg p-5">
          <div class="space-y-3">
            <div class="pb-3 border-b border-border/50">
              <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Client Name</p>
              <p class="text-[0.9rem] font-medium text-text-primary">
                {{ invoice?.clientId }}
                <a href="#" class="text-primary-500 hover:text-primary-400 text-[0.82rem]">( View Invoices )</a>
              </p>
            </div>
            <div class="pb-3 border-b border-border/50">
              <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Invoice Date</p>
              <p class="text-[0.9rem] text-text-primary">{{ new Date(invoice?.createdAt || '').toLocaleDateString() }}</p>
            </div>
            <div class="pb-3 border-b border-border/50">
              <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Due Date</p>
              <p class="text-[0.9rem] text-text-primary">{{ new Date(invoice?.dueDate || '').toLocaleDateString() }}</p>
            </div>
            <div class="pb-3 border-b border-border/50">
              <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Invoice Amount</p>
              <p class="text-[0.9rem] font-medium text-text-primary">{{ invoice?.total.toFixed(2) }} USD</p>
            </div>
            <div>
              <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Balance</p>
              <p class="text-[0.9rem] font-bold" :class="invoice?.status === 'Paid' ? 'text-status-green' : 'text-status-yellow'">
{{ invoice?.status === 'Paid' ? '0.00' : invoice?.total.toFixed(2) }} USD
              </p>
            </div>
          </div>
        </div>

        <!-- Right Column - Status & Actions -->
        <div class="space-y-3">
          <!-- Manage Mode Action Buttons -->
          <div v-if="isManageMode" class="flex gap-2 pb-3 border-b border-border/50">
            <button class="flex-1 px-4 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-[9px] transition-colors">
              Publish
            </button>
            <button class="flex-1 px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-yellow hover:bg-status-yellow/80 rounded-[9px] transition-colors">
              Publish and Send Email
            </button>
          </div>

          <div class="text-center pt-2">
            <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Status</p>
            <h3 :class="[
              'text-[1.75rem] font-bold uppercase',
              invoice?.status === 'Paid' ? 'text-status-green' : 'text-status-red'
            ]">
              {{ invoice?.status }}
            </h3>
          </div>

          <!-- Payment Method Info (Manage Mode) -->
          <div v-if="isManageMode" class="bg-primary-600 text-white rounded-lg p-3 mb-3">
            <p class="text-[0.82rem] font-medium">
              Payment Method: <strong>No Transactions Applied</strong>
            </p>
          </div>

          <!-- View Mode Content -->
          <div v-if="!isManageMode" class="space-y-3 pt-2">
            <div class="pb-3 border-b border-border/50">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Last Capture Attempt</label>
              <p class="text-[0.82rem] text-text-primary">None</p>
            </div>
            <div class="pb-3 border-b border-border/50">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Payment Method</label>
              <p class="text-[0.82rem] font-medium text-text-primary">{{ invoice?.gateway || 'Not set' }}</p>
            </div>
            <div class="pb-3">
              <AppSelect
                v-model="selectedEvent"
                :options="eventSelectOptions"
              />
            </div>
            <button class="w-full px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.05] transition-colors mb-2">
              Send Email
            </button>
            <button class="w-full px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-green hover:bg-status-green/80 rounded-[9px] transition-colors">
              Attempt Capture
            </button>
          </div>

          <!-- Manage Mode Actions -->
          <div v-if="isManageMode" class="space-y-2 pt-2">
            <AppSelect
              v-model="selectedEvent"
              :options="eventSelectOptions"
            />
            <button class="w-full px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.05] transition-colors">
              Send Email
            </button>
            <button class="w-full px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-green hover:bg-status-green/80 rounded-[9px] transition-colors mb-2">
              Attempt Capture
            </button>
            <div class="flex gap-2">
              <button class="flex-1 px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.05] transition-colors">
                Mark Cancelled
              </button>
              <button class="flex-1 px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.05] transition-colors">
                Mark Unpaid
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Add Payment Tab -->
    <div v-if="activeTab === 'payment'" class="space-y-6">
      <!-- Draft Invoice Notice -->
      <div v-if="invoice?.status === 'Draft'" class="bg-status-yellow/10 border border-status-yellow/20 rounded-lg p-4">
        <p class="text-[0.82rem] text-text-secondary">
          <strong>This is a Draft Invoice.</strong> Please Publish first to apply a payment.
        </p>
      </div>

      <div v-else class="bg-white/[0.02] border border-border rounded-lg p-6">
        <form class="space-y-4">
          <!-- First Row -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Left Column -->
            <div class="space-y-4">
              <div class="space-y-2">
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</label>
                <AppDatePicker
                  v-model="paymentDate"
                  placeholder="Select date..."
                />
              </div>
              <div class="space-y-2">
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</label>
                <AppSelect
                  v-model="paymentMethod"
                  :options="paymentMethodOptions"
                  placeholder="Select payment method..."
                />
              </div>
              <div class="space-y-2">
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Transaction ID</label>
                <input type="text" v-model="transactionId" placeholder="Enter transaction ID..." class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors" />
              </div>
            </div>

            <!-- Right Column -->
            <div class="space-y-4">
              <div class="space-y-2">
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount</label>
                <AppSpinner
                  v-model="paymentAmount"
                  :step="0.01"
                  :min="0"
                  placeholder="0.00"
                />
              </div>
              <div class="space-y-2">
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Transaction Fees</label>
                <AppSpinner
                  v-model="transactionFees"
                  :step="0.01"
                  :min="0"
                  placeholder="0.00"
                />
              </div>
              <div class="flex items-center gap-3">
                <input type="checkbox" v-model="sendPaymentEmail" class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors" />
                <label class="text-[0.82rem] text-text-secondary">Send Confirmation Email</label>
              </div>
            </div>
          </div>

          <!-- Add Payment Button -->
          <div class="flex justify-center pt-4">
            <button type="submit" class="px-8 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-[9px] transition-colors">
              Add Payment
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Options Tab -->
    <div v-if="activeTab === 'options'" class="bg-white/[0.02] border border-border rounded-lg p-6">
      <form class="space-y-6">
        <!-- First Row -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <!-- Invoice Date -->
          <div class="space-y-2">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Date</label>
            <AppDatePicker
              v-model="optionsInvoiceDate"
              placeholder="Select date..."
            />
          </div>
          <!-- Due Date -->
          <div class="space-y-2">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Due Date</label>
            <AppDatePicker
              v-model="optionsDueDate"
              placeholder="Select date..."
            />
          </div>
        </div>

        <!-- Second Row -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <!-- Payment Method -->
          <div class="space-y-2">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</label>
            <AppSelect
              v-model="optionsPaymentMethod"
              :options="paymentMethodOptions"
            />
          </div>
          <!-- Invoice # -->
          <div class="space-y-2">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice #</label>
            <input
              v-model="optionsInvoiceNumber"
              type="text"
              placeholder="Invoice number..."
              class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
            />
          </div>
        </div>

        <!-- Tax Rates -->
        <div class="space-y-3">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Tax Rate</label>
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Tax Rate 1 -->
            <div class="flex items-end gap-2">
              <div class="flex-1">
                <AppSpinner
                  v-model="optionsTaxRate1"
                  :step="0.01"
                  :min="0"
                  placeholder="0.00"
                />
              </div>
              <div class="w-20">
                <AppSpinner
                  v-model="optionsTaxRate1Percent"
                  :step="0.01"
                  :min="0"
                  placeholder="0.00"
                />
              </div>
              <span class="text-[0.82rem] text-text-muted">%</span>
            </div>
            <!-- Tax Rate 2 -->
            <div class="flex items-end gap-2">
              <div class="w-12 text-[0.82rem] text-text-muted text-center">2</div>
              <div class="flex-1">
                <AppSpinner
                  v-model="optionsTaxRate2"
                  :step="0.01"
                  :min="0"
                  placeholder="0.00"
                />
              </div>
              <div class="w-20">
                <AppSpinner
                  v-model="optionsTaxRate2Percent"
                  :step="0.01"
                  :min="0"
                  placeholder="0.00"
                />
              </div>
              <span class="text-[0.82rem] text-text-muted">%</span>
            </div>
          </div>
        </div>

        <!-- Status -->
        <div class="space-y-2">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</label>
          <AppSelect
            v-model="optionsStatus"
            :options="[
              { value: 'Draft', label: 'Draft' },
              { value: 'Unpaid', label: 'Unpaid' },
              { value: 'Paid', label: 'Paid' },
              { value: 'Overdue', label: 'Overdue' },
              { value: 'Cancelled', label: 'Cancelled' }
            ]"
          />
        </div>

        <!-- Save Button -->
        <div class="flex justify-center pt-4">
          <button
            type="submit"
            class="px-8 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-[9px] transition-colors"
          >
            Save Changes
          </button>
        </div>
      </form>
    </div>

    <!-- Credit Tab -->
    <div v-if="activeTab === 'credit'" class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <!-- Add Credit Section -->
      <div class="bg-white/[0.02] border border-border rounded-lg p-6">
        <h3 class="text-[0.82rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Add Credit to Invoice</h3>
        <div class="space-y-4">
          <div class="space-y-2">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Credit Amount</label>
            <div class="flex items-end gap-3">
              <div class="flex-1">
                <AppSpinner
                  v-model="addCreditAmount"
                  :step="0.01"
                  :min="0"
                  placeholder="0.00"
                />
              </div>
              <button class="px-4 py-2 text-[0.82rem] font-semibold text-white gradient-brand rounded-[9px] transition-opacity hover:opacity-90 shrink-0">
                Go
              </button>
            </div>
          </div>
          <p class="text-[0.82rem] text-status-green">{{ addCreditAmount.toFixed(2) }} USD Available</p>
        </div>
      </div>

      <!-- Remove Credit Section -->
      <div class="bg-white/[0.02] border border-border rounded-lg p-6">
        <h3 class="text-[0.82rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Remove Credit from Invoice</h3>
        <div class="space-y-4">
          <div class="space-y-2">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Credit Amount</label>
            <div class="flex items-end gap-3">
              <div class="flex-1">
                <AppSpinner
                  v-model="removeCreditAmount"
                  :step="0.01"
                  :min="0"
                  placeholder="0.00"
                />
              </div>
              <button class="px-4 py-2 text-[0.82rem] font-semibold text-white gradient-brand rounded-[9px] transition-opacity hover:opacity-90 shrink-0">
                Go
              </button>
            </div>
          </div>
          <p class="text-[0.82rem] text-status-red">{{ removeCreditAmount.toFixed(2) }} USD Available</p>
        </div>
      </div>
    </div>

    <!-- Refund Tab -->
    <div v-if="activeTab === 'refund'" class="bg-white/[0.02] border border-border rounded-lg p-6">
      <h2 class="text-[0.82rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-6">Refund Invoice</h2>
      <form class="space-y-4 max-w-2xl">
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <!-- Left Column -->
          <div class="space-y-4">
            <div class="space-y-2">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Transaction</label>
              <AppSelect
                v-model="refundTransactionId"
                :options="transactionOptions"
                placeholder="Select transaction..."
              />
            </div>
            <div class="space-y-2">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Refund Amount</label>
              <AppSpinner
                v-model="refundAmount"
                :step="0.01"
                :min="0"
                placeholder="0.00"
              />
              <p class="text-[0.78rem] text-text-muted">Leave blank for full refund</p>
            </div>
            <div class="space-y-2">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Refund Type</label>
              <AppSelect
                v-model="refundType"
                :options="refundTypeOptions"
              />
            </div>
          </div>

          <!-- Right Column -->
          <div class="space-y-4">
            <div class="space-y-3">
              <div class="flex items-center gap-3">
                <input type="checkbox" v-model="reversePayment" class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors" />
                <label class="text-[0.82rem] text-text-secondary">Reverse Payment</label>
              </div>
              <div class="flex items-center gap-3">
                <input type="checkbox" v-model="sendRefundEmail" class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors" />
                <label class="text-[0.82rem] text-text-secondary">Send Email</label>
              </div>
            </div>

            <!-- Credit Info Box -->
            <div class="bg-status-yellow/10 border border-status-yellow/20 rounded-lg p-4 space-y-3">
              <p class="text-[0.82rem] text-text-secondary"><strong>Credit Information:</strong></p>
              <div class="space-y-2">
                <div class="flex items-center gap-3">
                  <input type="checkbox" v-model="createCredit" class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors" />
                  <label class="text-[0.82rem] text-text-secondary">Create Credit for Invoice</label>
                </div>
                <div class="flex items-center gap-3">
                  <input type="checkbox" v-model="noRefundCredit" class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors" />
                  <label class="text-[0.82rem] text-text-secondary">No Refund of Credit</label>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Refund Button -->
        <div class="flex justify-center pt-4">
          <button type="submit" class="px-8 py-2 text-[0.82rem] font-semibold text-white bg-status-red hover:bg-status-red/80 rounded-[9px] transition-colors">
            Process Refund
          </button>
        </div>
      </form>
    </div>

    <!-- Notes Tab -->
    <div v-if="activeTab === 'notes'" class="space-y-4">
      <div v-if="isManageMode" class="bg-white/[0.02] border border-border rounded-lg p-6">
        <form class="space-y-4">
          <div class="space-y-2">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Notes</label>
            <textarea
              placeholder="Add notes about this invoice..."
              class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              rows="8"
            />
          </div>
          <div class="flex justify-center pt-4">
            <button
              type="submit"
              class="px-8 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-[9px] transition-colors"
            >
              Save Changes
            </button>
          </div>
        </form>
      </div>
      <div v-else class="bg-surface-card border border-border rounded-2xl p-6">
        <div class="bg-status-blue/10 border border-status-blue/20 rounded-lg p-4">
          <p class="text-[0.82rem] text-text-secondary">There are no notes on this invoice.</p>
        </div>
      </div>
    </div>

    <!-- Invoice Items Section -->
    <div class="mt-8">
      <h2 class="text-[1rem] font-semibold text-text-primary mb-4">Invoice Items</h2>

      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- View Mode -->
        <div v-if="!isManageMode">
          <!-- Header -->
          <div class="grid grid-cols-[1fr_100px_60px] px-6 py-3 bg-primary-600 text-white font-semibold text-[0.82rem]">
            <div class="text-center">Description</div>
            <div class="border-l border-white/20 pl-4 text-right">Amount</div>
            <div class="border-l border-white/20 pl-4 text-right">Taxed</div>
          </div>
          <!-- Items -->
          <div>
            <div v-if="invoice?.items && invoice.items.length > 0">
              <div v-for="(item, idx) in invoice.items" :key="idx" class="grid grid-cols-[1fr_100px_60px] px-6 py-3 text-[0.82rem] border-b border-border">
                <div class="text-text-secondary">{{ item.description }}</div>
                <div class="border-l border-border pl-4 text-right text-text-primary">{{ (item.quantity * item.amount).toFixed(2) }} USD</div>
                <div class="border-l border-border pl-4 text-right text-text-primary">{{ item.taxed ? 'Yes' : 'No' }}</div>
              </div>
            </div>
          </div>
          <!-- Summary -->
          <div class="bg-white/[0.02] border-t border-border px-6 py-3 space-y-2">
            <div class="grid grid-cols-[1fr_100px_60px]">
              <span class="text-[0.82rem] text-text-secondary text-right pr-4">Sub Total:</span>
              <div class="border-l border-border pl-4 text-right text-[0.82rem] text-text-primary font-medium">{{ invoice?.total.toFixed(2) }} USD</div>
              <div></div>
            </div>
            <div class="grid grid-cols-[1fr_100px_60px]">
              <span class="text-[0.82rem] text-text-secondary text-right pr-4">Credit:</span>
              <div class="border-l border-border pl-4 text-right text-[0.82rem] text-text-primary font-medium">0.00 USD</div>
              <div></div>
            </div>
            <div class="grid grid-cols-[1fr_100px_60px]">
              <span class="text-[0.82rem] text-text-secondary text-right pr-4">Balance:</span>
              <div class="border-l border-border pl-4 text-right text-[0.82rem] text-text-primary font-medium">{{ (invoice?.total ?? 0).toFixed(2) }} USD</div>
              <div></div>
            </div>
          </div>
          <!-- Total -->
          <div class="grid grid-cols-[1fr_100px_60px] px-6 py-4 bg-primary-600 text-white font-semibold text-[0.82rem] border-t border-border">
            <div class="text-right pr-4">Total Due:</div>
            <div class="border-l border-white/20 pl-4 text-right">{{ invoice?.total.toFixed(2) }} USD</div>
            <div></div>
          </div>
        </div>

        <!-- Edit Mode -->
        <div v-else>
          <!-- Header -->
          <div class="grid grid-cols-[40px_1fr_100px_60px_40px] px-6 py-3 bg-primary-600 text-white font-semibold text-[0.82rem]">
            <div></div>
            <div class="text-center">Description</div>
            <div class="border-l border-white/20 pl-4 text-right">Amount</div>
            <div class="border-l border-white/20 pl-4 text-center">Taxed</div>
            <div class="border-l border-white/20 pl-4"></div>
          </div>
          <!-- Items -->
          <div>
            <div v-for="item in editableItems" :key="item._tempId" class="grid grid-cols-[40px_1fr_100px_60px_40px] px-6 py-2 border-b border-border items-center text-[0.82rem]">
              <div class="flex items-center justify-center">
                <input
                  type="checkbox"
                  :checked="selectedItemIds.has(item._tempId)"
                  @change="toggleItemSelection(item._tempId)"
                  class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors"
                />
              </div>
              <input
                v-model="item.description"
                type="text"
                placeholder="Item description..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[6px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
              <input
                v-model.number="item.amount"
                type="number"
                step="0.01"
                min="0"
                placeholder="0.00"
                class="border-l border-border pl-4 px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[6px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors text-right"
              />
              <div class="border-l border-border pl-4 flex items-center justify-center">
                <input
                  type="checkbox"
                  v-model="item.taxed"
                  class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors"
                />
              </div>
              <button
                @click="showDeleteConfirm(item._tempId)"
                class="border-l border-border pl-4 text-status-red hover:text-status-red/80 transition-colors flex justify-center"
                type="button"
                title="Delete"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M19 6.4L17.6 5 12 10.6 6.4 5 5 6.4 10.6 12 5 17.6 6.4 19 12 13.4 17.6 19 19 17.6 13.4 12z"/>
                </svg>
              </button>
            </div>
          </div>
          <!-- Add Item Button Row -->
          <div class="px-6 py-4 border-b border-border flex justify-end">
            <button
              @click="addItem"
              type="button"
              class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[6px] hover:bg-white/[0.05] transition-colors"
            >
              Add Item
            </button>
          </div>

          <!-- Summary -->
          <div class="bg-white/[0.02] border-t border-border px-6 py-3 space-y-2">
            <div class="grid grid-cols-[40px_1fr_100px_60px_40px]">
              <span></span>
              <span class="text-[0.82rem] text-text-secondary text-right pr-4">Sub Total:</span>
              <div class="border-l border-border pl-4 text-right text-[0.82rem] text-text-primary font-medium">
                {{ editableItems.reduce((sum, item) => sum + (item.quantity * item.amount), 0).toFixed(2) }} USD
              </div>
              <div></div>
              <div></div>
            </div>
            <div class="grid grid-cols-[40px_1fr_100px_60px_40px]">
              <span></span>
              <span class="text-[0.82rem] text-text-secondary text-right pr-4">Credit:</span>
              <div class="border-l border-border pl-4 text-right text-[0.82rem] text-text-primary font-medium">0.00 USD</div>
              <div></div>
              <div></div>
            </div>
            <div class="grid grid-cols-[40px_1fr_100px_60px_40px]">
              <span></span>
              <span class="text-[0.82rem] text-text-secondary text-right pr-4">Balance:</span>
              <div class="border-l border-border pl-4 text-right text-[0.82rem] text-text-primary font-medium">
                {{ editableItems.reduce((sum, item) => sum + (item.quantity * item.amount), 0).toFixed(2) }} USD
              </div>
              <div></div>
              <div></div>
            </div>
          </div>

          <!-- Total -->
          <div class="grid grid-cols-[40px_1fr_100px_60px_40px] px-6 py-4 bg-primary-600 text-white font-semibold text-[0.82rem] border-t border-border">
            <div></div>
            <div class="text-right pr-4">Total Due:</div>
            <div class="border-l border-white/20 pl-4 text-right">
              {{ editableItems.reduce((sum, item) => sum + (item.quantity * item.amount), 0).toFixed(2) }} USD
            </div>
            <div></div>
            <div></div>
          </div>

          <!-- With Selected Dropdown and Buttons -->
          <div class="px-6 py-4 space-y-4 border-t border-border">
            <div class="flex items-center gap-3">
              <AppSelect
                v-model="itemAction"
                :options="[
                  { value: 'With Selected', label: 'With Selected' },
                  { value: 'Delete', label: 'Delete' }
                ]"
              />
            </div>
            <div class="flex items-center justify-center gap-3">
              <button
                @click="saveChanges"
                type="button"
                class="px-6 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-[9px] transition-colors"
              >
                Save Changes
              </button>
              <button
                @click="cancelChanges"
                type="button"
                class="px-6 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.05] transition-colors"
              >
                Cancel Changes
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Transactions Section -->
    <div class="mt-8">
      <h2 class="text-[1rem] font-semibold text-text-primary mb-4">Transactions</h2>
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- Header -->
        <div class="grid grid-cols-[1.2fr_1fr_1fr_1fr_0.8fr] gap-4 px-6 py-4 bg-primary-600 text-white font-semibold text-[0.82rem]">
          <div>Date</div>
          <div>Payment Method</div>
          <div>Transaction ID</div>
          <div class="text-right">Amount</div>
          <div class="text-right">Transaction Fees</div>
        </div>
        <!-- Transactions -->
        <div class="divide-y divide-border">
          <div class="px-6 py-3">
            <div class="grid grid-cols-[1.2fr_1fr_1fr_1fr_0.8fr] gap-4 text-[0.82rem]">
              <div class="text-text-secondary">21/05/2026 10:37</div>
              <div class="text-text-primary">Stripe</div>
              <div class="text-text-primary flex items-center gap-2">
                <span>ⓘ</span>
              </div>
              <div class="text-right text-text-primary">69,682.00 USD</div>
              <div class="text-right text-text-primary">0.00 USD</div>
            </div>
          </div>
          <div class="px-6 py-3">
            <div class="grid grid-cols-[1.2fr_1fr_1fr_1fr_0.8fr] gap-4 text-[0.82rem]">
              <div class="text-text-secondary">21/05/2026 10:37</div>
              <div class="text-text-primary">Stripe</div>
              <div class="text-text-primary flex items-center gap-2">
                <span>ⓘ</span>
              </div>
              <div class="text-right text-text-primary">3,555.00 USD</div>
              <div class="text-right text-text-primary">0.00 USD</div>
            </div>
          </div>
          <div class="px-6 py-3">
            <div class="grid grid-cols-[1.2fr_1fr_1fr_1fr_0.8fr] gap-4 text-[0.82rem]">
              <div class="text-text-secondary">21/05/2026 11:47</div>
              <div class="text-text-primary">Stripe</div>
              <div class="text-text-primary flex items-center gap-2">
                <span>ⓘ</span>
              </div>
              <div class="text-right text-text-primary">5,000.00 USD</div>
              <div class="text-right text-text-primary">550.00 USD</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Transaction History Section -->
    <div class="mt-8 mb-8">
      <h2 class="text-[1rem] font-semibold text-text-primary mb-4">Transaction History</h2>
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- Header -->
        <div class="grid grid-cols-[1.2fr_1.2fr_1fr_0.8fr_1.2fr] gap-4 px-6 py-4 bg-primary-600 text-white font-semibold text-[0.82rem]">
          <div>Date</div>
          <div>Payment Method</div>
          <div>Transaction ID</div>
          <div>Status</div>
          <div>Description</div>
        </div>
        <!-- Content -->
        <div class="px-6 py-4 text-text-secondary text-[0.82rem]">
          No Records Found
        </div>
      </div>
    </div>

    <!-- Delete Confirmation Modal -->
    <div v-if="deleteConfirmItemId !== null" class="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div class="bg-surface-card border border-border rounded-2xl p-6 max-w-sm">
        <h3 class="text-[1rem] font-semibold text-text-primary mb-2">Delete Item?</h3>
        <p class="text-[0.82rem] text-text-secondary mb-6">Are you sure you want to delete this item? This action cannot be undone.</p>
        <div class="flex items-center justify-end gap-3">
          <button
            @click="cancelDelete"
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[6px] hover:bg-white/[0.05] transition-colors"
          >
            Cancel
          </button>
          <button
            @click="confirmDelete"
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-red hover:bg-status-red/80 rounded-[6px] transition-colors"
          >
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
