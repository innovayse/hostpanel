<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import AppSelect from '../../../components/AppSelect.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import { useBillableItemsStore } from '../stores/billableItemsStore'
import { useApi } from '../../../composables/useApi'

const router = useRouter()
const store = useBillableItemsStore()
const { request } = useApi()

const clients = ref<{ id: number; name: string; email?: string; status?: string }[]>([])

const productOptions = [
  { value: '', label: 'None' },
]

const currencyOptions = [
  { value: 'USD', label: 'USD' },
  { value: 'EUR', label: 'EUR' },
  { value: 'GBP', label: 'GBP' },
  { value: 'AMD', label: 'AMD' },
]

const invoiceActionOptions = [
  { value: 'dont_invoice', label: "Don't Invoice for Now" },
  { value: 'next_cron', label: 'Invoice on Next Cron Run' },
  { value: 'next_invoice', label: "Add to User's Next Invoice" },
  { value: 'normal_due', label: 'Invoice as Normal for Due Date' },
  { value: 'recurring', label: 'Recurring Every' },
]

const recurringPeriodOptions = [
  { value: 'day', label: 'Day(s)' },
  { value: 'week', label: 'Week(s)' },
  { value: 'month', label: 'Month(s)' },
  { value: 'year', label: 'Year(s)' },
]

const form = ref({
  clientId: '',
  productId: '',
  description: '',
  quantity: 0,
  quantityType: 'hours',
  amount: 0,
  currency: 'USD',
  invoiceAction: 'dont_invoice',
  nextDueDate: new Date().toISOString().split('T')[0],
  invoiceCount: 0,
  recurringPeriod: 1,
  recurringPeriodType: 'month',
})

function loadClients() {
  clients.value = [
    { id: 1, name: 'John Doe', email: 'john@example.com', status: 'active' },
    { id: 2, name: 'Jane Smith (Acme Corp)', email: 'jane@example.com', status: 'active' },
    { id: 3, name: 'Bob Johnson', email: 'bob@example.com', status: 'active' },
    { id: 4, name: 'Alice Williams (Tech Inc)', email: 'alice@example.com', status: 'inactive' },
  ]
}

async function submit() {
  try {
    await store.create({
      clientId: parseInt(form.value.clientId),
      description: form.value.description,
      amount: form.value.amount,
      currency: form.value.currency,
      type: form.value.invoiceAction === 'recurring' ? 'Recurring' : 'OneTime',
      recurringPeriod: form.value.invoiceAction === 'recurring' ? `${form.value.recurringPeriod} ${form.value.recurringPeriodType}` : undefined,
      nextDueDate: form.value.nextDueDate || undefined,
    })
    router.push('/billing/billable-items')
  } catch (e) {
    console.error(e)
  }
}

onMounted(() => {
  loadClients()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Add Billable Item</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Create a new billable item for a client</p>
    </div>

    <!-- Form -->
    <form @submit.prevent="submit" class="bg-surface-card border border-border rounded-2xl p-4 space-y-3">

      <!-- Row 1: Client and Product -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-3">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Client</label>
          <AppClientSelect
            v-model="form.clientId"
            :clients="clients"
            placeholder="Select a client..."
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Product/Service</label>
          <AppSelect
            v-model="form.productId"
            :options="productOptions"
          />
        </div>
      </div>

      <!-- Row 2: Description -->
      <div>
        <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Description</label>
        <input
          v-model="form.description"
          type="text"
          required
          placeholder="What is this charge for?"
          class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
        />
      </div>

      <!-- Row 3: Hours/Qty and Amount -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-3">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Hours/Qty</label>
          <div class="mb-2">
            <AppSpinner
              v-model="form.quantity"
              :step="0.01"
              :min="0"
              placeholder="0.0"
            />
          </div>
          <div class="flex items-center gap-4 text-[0.82rem]">
            <label class="flex items-center gap-2 cursor-pointer">
              <input type="radio" v-model="form.quantityType" value="hours" class="cursor-pointer" />
              <span>Hours</span>
            </label>
            <label class="flex items-center gap-2 cursor-pointer">
              <input type="radio" v-model="form.quantityType" value="qty" class="cursor-pointer" />
              <span>Qty</span>
            </label>
          </div>
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Amount</label>
          <AppSpinner
            v-model="form.amount"
            :step="0.01"
            :min="0"
            placeholder="0.00"
          />
        </div>
      </div>

      <!-- Row 4: Invoice Action -->
      <div>
        <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Invoice Action</label>
        <div class="space-y-1.5">
          <div v-for="option in invoiceActionOptions" :key="option.value" class="flex items-center">
            <label class="flex items-center gap-2 cursor-pointer text-[0.82rem]">
              <input type="radio" v-model="form.invoiceAction" :value="option.value" class="cursor-pointer" />
              <span>{{ option.label }}</span>
            </label>
          </div>
        </div>
      </div>

      <!-- Row 5: Recur Every (conditional) -->
      <div v-if="form.invoiceAction === 'recurring'" class="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Recur Every</label>
          <AppSpinner
            v-model="form.recurringPeriod"
            :step="1"
            :min="1"
            placeholder="1"
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">&nbsp;</label>
          <AppSelect
            v-model="form.recurringPeriodType"
            :options="recurringPeriodOptions"
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">For Times</label>
          <AppSpinner
            v-model="form.invoiceCount"
            :step="1"
            :min="0"
            placeholder="0"
          />
        </div>
      </div>

      <!-- Row 6: Next Due Date -->
      <div>
        <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">(Next) Due Date</label>
        <AppDatePicker
          v-model="form.nextDueDate"
          placeholder="Select date..."
        />
      </div>

      <!-- Actions -->
      <div class="flex items-center justify-center gap-3 pt-4">
        <button
          type="submit"
          class="px-6 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-[9px] transition-colors"
        >
          Save Changes
        </button>
      </div>
    </form>

  </div>
</template>
