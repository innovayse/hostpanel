<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppSelect from '../../../components/AppSelect.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppDatePickerFuture from '../../../components/AppDatePickerFuture.vue'
import AppPhoneInput from '../../../components/AppPhoneInput.vue'
import AppCountrySelect from '../../../components/AppCountrySelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import { useQuotesStore } from '../stores/quotesStore'
import { useApi } from '../../../composables/useApi'

const route = useRoute()
const router = useRouter()
const store = useQuotesStore()
const { request } = useApi()

const clients = ref<{ id: number; name: string; email?: string; status?: string }[]>([])
const showProductModal = ref(false)
const selectedProductId = ref('')

const productOptions = [
  { value: '1', label: 'Web Hosting - Basic' },
  { value: '2', label: 'Web Hosting - Professional' },
  { value: '3', label: 'SSL Certificate - 1 Year' },
  { value: '4', label: 'Domain Registration' },
  { value: '5', label: 'Email Hosting - 10 Accounts' },
  { value: '6', label: 'VPS - Standard' },
  { value: '7', label: 'Dedicated Server' },
  { value: '8', label: 'Development Services' },
]

const products = {
  '1': { name: 'Web Hosting - Basic', price: 99.99 },
  '2': { name: 'Web Hosting - Professional', price: 199.99 },
  '3': { name: 'SSL Certificate - 1 Year', price: 49.99 },
  '4': { name: 'Domain Registration', price: 12.99 },
  '5': { name: 'Email Hosting - 10 Accounts', price: 59.99 },
  '6': { name: 'VPS - Standard', price: 299.99 },
  '7': { name: 'Dedicated Server', price: 499.99 },
  '8': { name: 'Development Services', price: 0 },
} as Record<string, { name: string; price: number }>

const stageOptions = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Delivered', label: 'Delivered' },
  { value: 'On Hold', label: 'On Hold' },
  { value: 'Accepted', label: 'Accepted' },
  { value: 'Lost', label: 'Lost' },
  { value: 'Dead', label: 'Dead' },
]

const currencyOptions = [
  { value: 'USD', label: 'USD' },
  { value: 'EUR', label: 'EUR' },
  { value: 'GBP', label: 'GBP' },
  { value: 'AMD', label: 'AMD' },
]

const form = ref({
  dateCreated: new Date().toISOString().split('T')[0],
  validUntil: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
  subject: '',
  stage: 'Draft',
  quoteType: 'existing',
  clientId: '',
  firstName: '',
  lastName: '',
  email: '',
  company: '',
  phoneNumber: '',
  phoneCountry: 'AM',
  address1: '',
  address2: '',
  city: '',
  state: '',
  country: 'AM',
  postalCode: '',
  currency: 'USD',
  lineItems: [
    { description: '', quantity: 1, unitPrice: 0, discount: 0, taxed: false }
  ],
  proposalText: '',
  customNotes: '',
  termsAndConditions: '',
})

async function loadClients() {
  try {
    const result = await request<{ items: Array<{ id: number; firstName: string; lastName: string; companyName?: string; email: string; status: string }> }>('/clients?page=1&pageSize=100')
    clients.value = result.items.map(c => ({
      id: c.id,
      name: c.companyName ? `${c.firstName} ${c.lastName} (${c.companyName})` : `${c.firstName} ${c.lastName}`,
      email: c.email,
      status: c.status,
    }))
  } catch {
    clients.value = []
  }
}

function openProductModal() {
  showProductModal.value = true
  selectedProductId.value = ''
}

function addPredefinedProduct() {
  if (selectedProductId.value) {
    const product = products[selectedProductId.value]
    form.value.lineItems.push({
      description: product.name,
      quantity: 1,
      unitPrice: product.price,
      discount: 0,
      taxed: false
    })
    showProductModal.value = false
    selectedProductId.value = ''
  }
}

function addLineItem() {
  form.value.lineItems.push({ description: '', unitPrice: 0, quantity: 1, discount: 0, taxed: false })
}

function removeLineItem(index: number) {
  form.value.lineItems.splice(index, 1)
}

function calculateLineAmount(index: number): number {
  const item = form.value.lineItems[index]
  const subtotal = item.unitPrice * item.quantity
  const discountAmount = subtotal * (item.discount / 100)
  return subtotal - discountAmount
}

function calculateTotal(): number {
  return form.value.lineItems.reduce((sum, item) => sum + (item.unitPrice * item.quantity), 0)
}

async function submit() {
  try {
    await store.create({
      clientId: parseInt(form.value.clientId),
      subject: form.value.subject,
      expiryDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
      notes: form.value.proposalText,
      items: form.value.lineItems.map(item => ({
        description: item.description,
        unitPrice: item.unitPrice,
        quantity: item.quantity
      }))
    })
    router.push('/billing/quotes')
  } catch (e) {
    console.error(e)
  }
}

onMounted(() => {
  loadClients()
  const qClientId = route.query.clientId as string | undefined
  if (qClientId) {
    form.value.clientId = qClientId
    form.value.quoteType = 'existing'
  }
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Create Quote</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Create a new quote for a client</p>
    </div>

    <!-- Form -->
    <form @submit.prevent="submit" class="space-y-6">

      <!-- General Information Card -->
      <div class="bg-surface-card border border-border rounded-2xl p-6 space-y-4">
        <div class="space-y-4">
          <h2 class="text-[0.95rem] font-semibold text-text-primary">General Information</h2>

          <!-- First Row: Subject and Stage -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
            <div>
              <label class="block text-[0.82rem] font-medium text-text-primary mb-2">Subject</label>
              <input
                v-model="form.subject"
                type="text"
                required
                placeholder=""
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[4px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
            <div>
              <label class="block text-[0.82rem] font-medium text-text-primary mb-2">Stage</label>
              <AppSelect
                v-model="form.stage"
                :options="stageOptions"
              />
            </div>
          </div>

          <!-- Second Row: Date Created and Valid Until -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
            <div>
              <label class="block text-[0.82rem] font-medium text-text-primary mb-2">Date Created</label>
              <AppDatePickerFuture
                v-model="form.dateCreated"
                placeholder=""
              />
            </div>
            <div>
              <label class="block text-[0.82rem] font-medium text-text-primary mb-2">Valid Until</label>
              <AppDatePickerFuture
                v-model="form.validUntil"
                placeholder=""
              />
            </div>
          </div>
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center justify-start gap-2 pt-4 border-t border-border flex-wrap">
          <button
            type="submit"
            class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded transition-colors"
          >
            Save Changes
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Duplicate
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Printable Version
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            View PDF
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Download PDF
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Email as PDF
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Convert to Invoice
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-red hover:bg-status-red/90 rounded transition-colors ml-auto"
          >
            Delete
          </button>
        </div>
      </div>

      <!-- Client Information Card -->
      <div class="bg-surface-card border border-border rounded-2xl p-4 space-y-4">
        <h2 class="text-[0.95rem] font-semibold text-text-primary">Client Information</h2>

        <!-- Quote Type Selection -->
        <div class="space-y-2">
          <div class="flex items-center gap-4">
            <label class="flex items-center gap-2 cursor-pointer">
              <input type="radio" v-model="form.quoteType" value="existing" class="cursor-pointer" />
              <span class="text-[0.82rem] text-text-primary">Quote for existing client</span>
            </label>
            <label class="flex items-center gap-2 cursor-pointer">
              <input type="radio" v-model="form.quoteType" value="new" class="cursor-pointer" />
              <span class="text-[0.82rem] text-text-primary">Quote for new client</span>
            </label>
          </div>
        </div>

        <!-- Existing Client Selection -->
        <div v-if="form.quoteType === 'existing'" class="lg:col-span-2">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Client</label>
          <AppClientSelect
            v-model="form.clientId"
            :clients="clients"
            placeholder="Select a client..."
          />
        </div>

        <!-- New Client Form -->
        <div v-else class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <!-- Left Column -->
          <div class="space-y-3">
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">First Name</label>
              <input
                v-model="form.firstName"
                type="text"
                placeholder="First name..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Email Address</label>
              <input
                v-model="form.email"
                type="email"
                placeholder="Email..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Phone Number</label>
              <AppPhoneInput
                v-model:phoneNumber="form.phoneNumber"
                v-model:countryCode="form.phoneCountry"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 1</label>
              <input
                v-model="form.address1"
                type="text"
                placeholder="Street address..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">City</label>
              <input
                v-model="form.city"
                type="text"
                placeholder="City..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
          </div>

          <!-- Right Column -->
          <div class="space-y-3">
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Last Name</label>
              <input
                v-model="form.lastName"
                type="text"
                placeholder="Last name..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Company</label>
              <input
                v-model="form.company"
                type="text"
                placeholder="Company name..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Country</label>
              <AppCountrySelect
                v-model="form.country"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 2</label>
              <input
                v-model="form.address2"
                type="text"
                placeholder="Apartment, suite, etc..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">State/Region</label>
              <input
                v-model="form.state"
                type="text"
                placeholder="State..."
                class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
              />
            </div>
          </div>
        </div>

        <!-- Postal Code and Currency (Full width - 2 columns) -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Postal Code</label>
            <input
              v-model="form.postalCode"
              type="text"
              placeholder="ZIP code..."
              class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
            />
          </div>
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Currency</label>
            <AppSelect
              v-model="form.currency"
              :options="currencyOptions"
            />
          </div>
        </div>
      </div>

      <!-- Line Items Card -->
      <div class="bg-surface-card border border-border rounded-2xl p-6 space-y-4">
        <h2 class="text-[0.95rem] font-semibold text-text-primary mb-4">Line Items</h2>

        <!-- Table Header -->
        <div class="overflow-x-auto">
          <div class="grid grid-cols-[60px_2fr_100px_100px_100px_100px_40px] gap-2 bg-primary-600 text-white rounded-t-lg p-3 text-[0.82rem] font-semibold">
            <div class="text-center">Qty</div>
            <div>Description</div>
            <div class="text-center">Unit Price</div>
            <div class="text-center">Discount %</div>
            <div class="text-center">Total</div>
            <div class="text-center">Taxed</div>
            <div></div>
          </div>

          <!-- Table Rows -->
          <div>
            <div
              v-for="(item, index) in form.lineItems"
              :key="index"
              class="grid grid-cols-[60px_2fr_100px_100px_100px_100px_40px] gap-2 p-3 border-b border-border items-center hover:bg-white/[0.02] transition-colors"
            >
              <!-- Qty -->
              <input
                v-model.number="item.quantity"
                type="number"
                min="0"
                :step="1"
                class="px-2 py-1.5 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded text-center focus:outline-none focus:border-primary-500/40 transition-colors"
              />

              <!-- Description -->
              <textarea
                v-model="item.description"
                placeholder="Item description..."
                rows="1"
                class="px-2 py-1.5 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded focus:outline-none focus:border-primary-500/40 transition-colors resize-none"
              />

              <!-- Unit Price -->
              <input
                v-model.number="item.unitPrice"
                type="number"
                min="0"
                step="0.01"
                placeholder="0.00"
                class="px-2 py-1.5 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded text-center focus:outline-none focus:border-primary-500/40 transition-colors"
              />

              <!-- Discount % -->
              <input
                v-model.number="item.discount"
                type="number"
                min="0"
                max="100"
                step="0.01"
                placeholder="0.00"
                class="px-2 py-1.5 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded text-center focus:outline-none focus:border-primary-500/40 transition-colors"
              />

              <!-- Total (calculated, read-only) -->
              <div class="px-2 py-1.5 text-[0.82rem] text-text-primary text-center font-medium bg-white/[0.02] rounded border border-border/30">
                {{ calculateLineAmount(index).toFixed(2) }}
              </div>

              <!-- Taxed (checkbox) -->
              <div class="flex justify-center">
                <AppCheckbox v-model="item.taxed" />
              </div>

              <!-- Delete Button -->
              <button
                v-if="form.lineItems.length > 1"
                type="button"
                @click="removeLineItem(index)"
                class="text-status-red hover:bg-status-red/10 rounded p-1 transition-colors text-center"
                title="Delete row"
              >
                ✕
              </button>
            </div>
          </div>
        </div>

        <!-- Add Item Button -->
        <button
          type="button"
          @click="openProductModal"
          class="flex items-center gap-2 text-[0.82rem] font-semibold text-primary-500 hover:text-primary-600 transition-colors mt-3"
        >
          <span class="text-lg">+</span> Add a Predefined Product
        </button>

        <!-- Totals Section -->
        <div class="border-t border-border pt-4 space-y-2">
          <div class="grid grid-cols-[60px_2fr_100px_100px_100px_100px_40px] gap-2 text-[0.82rem] bg-white/[0.02] p-3 rounded">
            <div></div>
            <div></div>
            <div></div>
            <div class="text-text-primary font-semibold text-right pr-2">Sub Total</div>
            <div class="text-text-primary font-bold text-right">${{ calculateTotal().toFixed(2) }} {{ form.currency }}</div>
            <div></div>
            <div></div>
          </div>
          <div class="grid grid-cols-[60px_2fr_100px_100px_100px_100px_40px] gap-2 text-[0.82rem] bg-white/[0.02] p-3 rounded">
            <div></div>
            <div></div>
            <div></div>
            <div class="text-text-primary font-semibold text-right pr-2">Total Due</div>
            <div class="text-text-primary font-bold text-right">${{ calculateTotal().toFixed(2) }} {{ form.currency }}</div>
            <div></div>
            <div></div>
          </div>
        </div>
      </div>

      <!-- Notes Card -->
      <div class="bg-surface-card border border-border rounded-2xl p-6">
        <h2 class="text-[0.95rem] font-semibold text-text-primary mb-6">Notes</h2>

        <!-- Notes Grid -->
        <div class="grid grid-cols-[200px_1fr] gap-6 mb-6">
          <!-- Proposal Text -->
          <div class="text-right text-[0.82rem] text-text-secondary">
            <div class="font-medium text-text-primary">Proposal Text</div>
            <div class="text-[0.75rem] mt-1">(Displayed at the Top of the Quote)</div>
          </div>
          <textarea
            v-model="form.proposalText"
            rows="4"
            placeholder="Enter proposal text..."
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[4px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors resize-none"
          />
        </div>

        <!-- Customer Notes -->
        <div class="grid grid-cols-[200px_1fr] gap-6 mb-6">
          <div class="text-right text-[0.82rem] text-text-secondary">
            <div class="font-medium text-text-primary">Customer Notes</div>
            <div class="text-[0.75rem] mt-1">(Displayed as a Footer to the Quote)</div>
          </div>
          <textarea
            v-model="form.customNotes"
            rows="4"
            placeholder="Enter customer notes..."
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[4px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors resize-none"
          />
        </div>

        <!-- Admin Only Notes -->
        <div class="grid grid-cols-[200px_1fr] gap-6 border-b border-border pb-6 mb-6">
          <div class="text-right text-[0.82rem] text-text-secondary">
            <div class="font-medium text-text-primary">Admin Only Notes</div>
            <div class="text-[0.75rem] mt-1">(Private Notes)</div>
          </div>
          <textarea
            v-model="form.termsAndConditions"
            rows="4"
            placeholder="Enter admin notes..."
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[4px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors resize-none"
          />
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center gap-2 flex-wrap">
          <button
            type="submit"
            class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded transition-colors"
          >
            Save Changes
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Duplicate
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Printable Version
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            View PDF
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Download PDF
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Email as PDF
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded hover:bg-white/[0.05] transition-colors"
          >
            Convert to Invoice
          </button>
          <button
            type="button"
            class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-red hover:bg-status-red/90 rounded transition-colors ml-auto"
          >
            Delete
          </button>
        </div>
      </div>

    </form>

    <!-- Load Product Modal -->
    <div v-if="showProductModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div class="bg-surface-card border border-border rounded-2xl p-6 max-w-sm w-full mx-4">
        <h3 class="text-[1rem] font-semibold text-text-primary mb-4">Load Product</h3>

        <div class="space-y-4">
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Product/Service</label>
            <AppSelect
              v-model="selectedProductId"
              :options="productOptions"
              placeholder="Choose a product..."
            />
          </div>
        </div>

        <div class="flex items-center justify-end gap-3 mt-6">
          <button
            @click="showProductModal = false"
            class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[6px] hover:bg-white/[0.05] transition-colors"
          >
            Cancel
          </button>
          <button
            @click="addPredefinedProduct"
            :disabled="!selectedProductId"
            class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-[6px] transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Select
          </button>
        </div>
      </div>
    </div>

  </div>
</template>
