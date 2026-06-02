<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import AppSelect from '../../../components/AppSelect.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import { useTransactionsStore } from '../stores/transactionsStore'
import { useApi } from '../../../composables/useApi'

const router = useRouter()
const route = useRoute()
const store = useTransactionsStore()
const { request } = useApi()

const clients = ref<{ id: number; name: string; email?: string; status?: string }[]>([])

const currencyOptions = [
  { value: 'USD', label: 'USD' },
  { value: 'EUR', label: 'EUR' },
  { value: 'GBP', label: 'GBP' },
  { value: 'AMD', label: 'AMD' },
]

const paymentMethodOptions = [
  { value: '', label: 'None' },
  { value: 'Stripe', label: 'Stripe' },
  { value: 'PayPal', label: 'PayPal' },
  { value: 'Credit/Debit Card', label: 'Credit/Debit Card' },
]

const form = ref({
  date: new Date().toISOString().split('T')[0],
  clientId: '',
  transactionId: '',
  paymentMethod: '',
  description: '',
  amountIn: 0,
  amountOut: 0,
  fees: 0,
  currency: 'USD',
  invoiceIds: '',
  addToCredit: false,
})

async function loadClients() {
  try {
    const data = await request<any>('/clients')
    clients.value = (data.items || []).map((c: any) => ({
      id: c.id,
      name: `${c.firstName} ${c.lastName}${c.companyName ? ` (${c.companyName})` : ''}`,
      email: c.email,
      status: c.status
    }))
  } catch (e) {
    console.error('Failed to load clients:', e)
  }
}

async function loadTransaction() {
  const id = route.params.id as string
  await store.fetchById(parseInt(id))

  if (store.currentTransaction) {
    const tx = store.currentTransaction
    form.value = {
      date: new Date(tx.createdAt).toISOString().split('T')[0],
      clientId: String(tx.clientId),
      transactionId: tx.transactionId || '',
      paymentMethod: tx.gateway || '',
      description: tx.description,
      amountIn: tx.type === 'Credit' ? tx.amount : 0,
      amountOut: tx.type === 'Debit' ? tx.amount : 0,
      fees: tx.fees,
      currency: tx.currency,
      invoiceIds: tx.invoiceId ? String(tx.invoiceId) : '',
      addToCredit: false,
    }
  }
}

async function submit() {
  try {
    const id = parseInt(route.params.id as string)
    await store.update(id, {
      clientId: parseInt(form.value.clientId),
      description: form.value.description,
      amount: form.value.amountIn > 0 ? form.value.amountIn : form.value.amountOut,
      fees: form.value.fees,
      currency: form.value.currency,
      gateway: form.value.paymentMethod,
      transactionId: form.value.transactionId,
      type: form.value.amountIn > 0 ? 'Credit' : 'Debit'
    })
    router.push('/billing/transactions')
  } catch (e) {
    console.error(e)
  }
}

onMounted(() => {
  loadClients()
  loadTransaction()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Edit Transaction</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Update payment transaction details</p>
    </div>

    <!-- Form -->
    <form @submit.prevent="submit" class="bg-surface-card border border-border rounded-2xl p-6 max-w-3xl">

      <!-- Date and Currency Row -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-5">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Date</label>
          <AppDatePicker
            v-model="form.date"
            placeholder="Select date..."
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Currency</label>
          <AppSelect
            v-model="form.currency"
            :options="currencyOptions"
          />
        </div>
      </div>

      <!-- Client Selection -->
      <div class="mb-5">
        <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Related Client</label>
        <AppClientSelect
          v-model="form.clientId"
          :clients="clients"
          placeholder="Select a client..."
        />
      </div>

      <!-- Amount In/Out -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-5">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Amount In</label>
          <AppSpinner
            v-model="form.amountIn"
            :step="0.01"
            :min="0"
            placeholder="0.00"
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Amount Out</label>
          <AppSpinner
            v-model="form.amountOut"
            :step="0.01"
            :min="0"
            placeholder="0.00"
          />
        </div>
      </div>

      <!-- Description and Fees -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-5">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Description</label>
          <input
            v-model="form.description"
            type="text"
            placeholder="What is this transaction for?"
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Fees</label>
          <AppSpinner
            v-model="form.fees"
            :step="0.01"
            :min="0"
            placeholder="0.00"
          />
        </div>
      </div>

      <!-- Transaction ID and Payment Method -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-5">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Transaction ID</label>
          <input
            v-model="form.transactionId"
            type="text"
            placeholder="External transaction ID..."
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Payment Method</label>
          <AppSelect
            v-model="form.paymentMethod"
            :options="paymentMethodOptions"
          />
        </div>
      </div>

      <!-- Invoice IDs -->
      <div class="mb-5">
        <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Invoice IDs (Comma Separated)</label>
        <input
          v-model="form.invoiceIds"
          type="text"
          placeholder="e.g., 1, 2, 3"
          class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
        />
      </div>

      <!-- Actions -->
      <div class="flex items-center gap-3 pt-2">
        <button
          type="submit"
          class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90"
        >
          Update Transaction
        </button>
        <router-link
          to="/billing/transactions"
          class="px-4 py-2 text-[0.82rem] font-semibold text-text-secondary hover:text-text-primary transition-colors"
        >
          Cancel
        </router-link>
      </div>
    </form>

  </div>
</template>
