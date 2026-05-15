<script setup lang="ts">
/**
 * Full-page form for creating a new client account.
 *
 * All sections are displayed on a single scrollable page in a two-column layout.
 * Navigates back to the clients list on success.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import type { AdminCreateClientPayload } from '../../../types/models'
import { useClientsStore } from '../stores/clientsStore'
import { CLIENT_STATUS_OPTIONS, LANGUAGE_OPTIONS } from '../../../utils/constants'
import { useGeoOptions } from '../../../composables/useGeoOptions'
import AppSelect from '../../../components/AppSelect.vue'
import ToggleSwitch from '../../../components/ToggleSwitch.vue'

const router = useRouter()
const store = useClientsStore()

/** True while the save request is in flight. */
const saving = ref(false)

/** Error message shown when client creation fails. */
const saveError = ref<string | null>(null)

// --- Profile fields ---

/** Whether to create a new user or associate an existing one. */
const createNewUser = ref(true)

/** Email for new user creation. */
const email = ref('')

/** Password for new user creation. */
const password = ref('')

/** Search term for finding existing users. */
const userSearchTerm = ref('')

/** Results from user search. */
const userSearchResults = ref<{ id: string; email: string; firstName: string; lastName: string }[]>([])

/** True while user search is in flight. */
const userSearchLoading = ref(false)

/** Selected existing user, null until one is picked. */
const selectedUser = ref<{ id: string; email: string; firstName: string; lastName: string } | null>(null)

/** Client first name. */
const firstName = ref('')

/** Client last name. */
const lastName = ref('')

/** Optional company name. */
const companyName = ref('')

/** Optional phone number (without country code). */
const phone = ref('')

/** Selected phone country ISO2 code. */
const phoneCountry = ref('US')

const { countryOptions, phoneCodeOptions, resolvePhoneCode } = useGeoOptions()

/** Resolves the selected country's phone code for the payload. */
const selectedPhoneCode = computed(() => resolvePhoneCode(phoneCountry.value))

/** Preferred language code. */
const language = ref('')

/** Client status. */
const status = ref('Active')

// --- Address & Billing fields ---

/** Street address line 1. */
const street = ref('')

/** Street address line 2. */
const address2 = ref('')

/** City. */
const city = ref('')

/** State or province. */
const state = ref('')

/** Postal code. */
const postCode = ref('')

/** Selected country code. */
const country = ref('')

/** Selected currency code. */
const currency = ref('')

/** Payment method. */
const paymentMethod = ref('')

/** Billing contact. */
const billingContact = ref('')

// --- Notification toggles ---

/** General email notifications. */
const notifyGeneral = ref(true)

/** Invoice email notifications. */
const notifyInvoice = ref(true)

/** Support email notifications. */
const notifySupport = ref(true)

/** Product email notifications. */
const notifyProduct = ref(true)

/** Domain email notifications. */
const notifyDomain = ref(true)

/** Affiliate email notifications. */
const notifyAffiliate = ref(true)

// --- Settings toggles ---

/** Whether late fees apply. */
const lateFees = ref(true)

/** Whether overdue notices are sent. */
const overdueNotices = ref(true)

/** Whether the client is tax exempt. */
const taxExempt = ref(false)

/** Whether invoices are separated per product. */
const separateInvoices = ref(false)

/** Whether CC processing is disabled. */
const disableCcProcessing = ref(false)

/** Whether marketing opt-in is enabled. */
const marketingOptIn = ref(false)

/** Whether status updates are tracked. */
const statusUpdate = ref(true)

/** Whether SSO is allowed. */
const allowSso = ref(true)

/** Internal admin notes. */
const adminNotes = ref('')

// --- User search debounce ---

/** Timer handle for debounced user search. */
let searchTimer: ReturnType<typeof setTimeout> | null = null

watch(userSearchTerm, (term) => {
  if (searchTimer) clearTimeout(searchTimer)
  if (!term.trim()) {
    userSearchResults.value = []
    return
  }
  searchTimer = setTimeout(async () => {
    userSearchLoading.value = true
    try {
      const results = await store.searchUsers(term.trim())
      userSearchResults.value = results.slice(0, 5)
    } catch {
      userSearchResults.value = []
    } finally {
      userSearchLoading.value = false
    }
  }, 300)
})

/**
 * Selects an existing user from search results.
 *
 * @param user - The user to associate with the new client.
 */
function selectUser(user: { id: string; email: string; firstName: string; lastName: string }): void {
  selectedUser.value = user
  userSearchTerm.value = ''
  userSearchResults.value = []
}

/**
 * Clears the selected existing user.
 */
function clearSelectedUser(): void {
  selectedUser.value = null
}

// --- Country/currency options ---

/** Currency options for the AppSelect dropdown. */
const currencyOptions = computed(() =>
  [{ value: '', label: 'Select currency' }, ...store.currencies.map(c => ({ value: c.code, label: `${c.name} (${c.symbol})` }))]
)

/** Language options for the dropdown. */
const languageOptions = LANGUAGE_OPTIONS

/** Status options for the dropdown. */
const statusOptions = CLIENT_STATUS_OPTIONS

// --- Validation ---

/** Field-level validation errors. */
const errors = ref({
  email: '',
  password: '',
  existingUser: '',
  firstName: '',
  lastName: '',
})

/** True when any validation error is present. */
const hasErrors = computed(() => Object.values(errors.value).some(Boolean))

/**
 * Validates required fields and sets error messages.
 *
 * @returns True when all required fields are filled.
 */
function validate(): boolean {
  errors.value = { email: '', password: '', existingUser: '', firstName: '', lastName: '' }

  if (createNewUser.value) {
    if (!email.value.trim()) errors.value.email = 'Email is required'
    if (!password.value || password.value.length < 8) errors.value.password = 'Password must be at least 8 characters'
  } else {
    if (!selectedUser.value) errors.value.existingUser = 'Please select a user'
  }

  if (!firstName.value.trim()) errors.value.firstName = 'First name is required'
  if (!lastName.value.trim()) errors.value.lastName = 'Last name is required'

  return !hasErrors.value
}

/** Builds the payload and submits to the API. */
async function handleSubmit(): Promise<void> {
  if (!validate()) return

  const payload: AdminCreateClientPayload = {
    createNewUser: createNewUser.value,
    existingUserId: !createNewUser.value ? selectedUser.value?.id : undefined,
    email: createNewUser.value ? email.value.trim() : undefined,
    password: createNewUser.value ? password.value : undefined,
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    companyName: companyName.value.trim() || undefined,
    phone: phone.value.trim() ? `${selectedPhoneCode.value} ${phone.value.trim()}` : undefined,
    street: street.value.trim() || undefined,
    address2: address2.value.trim() || undefined,
    city: city.value.trim() || undefined,
    state: state.value.trim() || undefined,
    postCode: postCode.value.trim() || undefined,
    country: country.value || undefined,
    currency: currency.value || undefined,
    language: language.value || undefined,
    paymentMethod: paymentMethod.value.trim() || undefined,
    billingContact: billingContact.value.trim() || undefined,
    adminNotes: adminNotes.value.trim() || undefined,
    status: status.value,
    notifyGeneral: notifyGeneral.value,
    notifyInvoice: notifyInvoice.value,
    notifySupport: notifySupport.value,
    notifyProduct: notifyProduct.value,
    notifyDomain: notifyDomain.value,
    notifyAffiliate: notifyAffiliate.value,
    lateFees: lateFees.value,
    overdueNotices: overdueNotices.value,
    taxExempt: taxExempt.value,
    separateInvoices: separateInvoices.value,
    disableCcProcessing: disableCcProcessing.value,
    marketingOptIn: marketingOptIn.value,
    statusUpdate: statusUpdate.value,
    allowSso: allowSso.value,
  }

  saving.value = true
  try {
    await store.createClient(payload)
    router.push('/clients')
  } catch {
    saveError.value = 'Failed to create client.'
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  store.fetchCurrencies()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header with sticky actions -->
    <div class="flex items-center justify-between mb-6">
      <div class="flex items-center gap-3">
        <button
          class="w-8 h-8 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
          @click="router.push('/clients')"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="15 18 9 12 15 6"/>
          </svg>
        </button>
        <div>
          <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Add New Client</h1>
          <p class="text-[0.78rem] text-text-secondary mt-1">Create a new client account</p>
        </div>
      </div>
      <div class="flex items-center gap-2.5">
        <button
          type="button"
          class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
          @click="router.push('/clients')"
        >
          Cancel
        </button>
        <button
          type="button"
          :disabled="saving"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="handleSubmit"
        >
          {{ saving ? 'Creating...' : 'Create Client' }}
        </button>
      </div>
    </div>

    <div v-if="saveError" class="mb-4 px-4 py-2.5 text-[0.82rem] text-status-red bg-status-red/10 border border-status-red/20 rounded-xl">
      {{ saveError }}
    </div>

    <form @submit.prevent="handleSubmit">

      <!-- Two-column layout on large screens -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">

        <!-- LEFT COLUMN -->
        <div class="flex flex-col gap-5">

          <!-- Account Owner -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Account Owner</h2>

            <div class="flex flex-col gap-2 mb-4">
              <label class="flex items-center gap-2.5 cursor-pointer">
                <input type="radio" :checked="createNewUser" class="w-4 h-4 accent-primary-500" @change="createNewUser = true; selectedUser = null" />
                <span class="text-[0.82rem] text-text-secondary">Create a new user</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <input type="radio" :checked="!createNewUser" class="w-4 h-4 accent-primary-500" @change="createNewUser = false; email = ''; password = ''" />
                <span class="text-[0.82rem] text-text-secondary">Associate with an existing user</span>
              </label>
            </div>

            <!-- New user fields -->
            <div v-if="createNewUser" class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Email <span class="text-status-red">*</span></label>
                <input v-model="email" type="email" placeholder="client@example.com"
                  :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                    errors.email ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                  @input="errors.email = ''" />
                <p v-if="errors.email" class="text-[0.68rem] text-status-red mt-1">{{ errors.email }}</p>
              </div>
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Password <span class="text-status-red">*</span></label>
                <input v-model="password" type="password" placeholder="Min. 8 characters"
                  :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                    errors.password ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                  @input="errors.password = ''" />
                <p v-if="errors.password" class="text-[0.68rem] text-status-red mt-1">{{ errors.password }}</p>
              </div>
            </div>

            <!-- Existing user search -->
            <div v-else>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Find Existing User <span class="text-status-red">*</span></label>
              <div v-if="selectedUser" class="flex items-center gap-2.5 bg-primary-500/10 border border-primary-500/20 rounded-[10px] px-3 py-2.5">
                <div class="flex items-center justify-center w-7 h-7 rounded-full bg-primary-500/20 text-primary-400 text-[0.65rem] font-bold shrink-0">
                  {{ selectedUser.firstName.charAt(0) }}{{ selectedUser.lastName.charAt(0) }}
                </div>
                <div class="flex-1 min-w-0">
                  <p class="text-[0.82rem] text-text-primary font-medium truncate">{{ selectedUser.firstName }} {{ selectedUser.lastName }}</p>
                  <p class="text-[0.68rem] text-text-muted truncate">{{ selectedUser.email }}</p>
                </div>
                <button type="button" class="w-6 h-6 flex items-center justify-center rounded-md text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors" @click="clearSelectedUser">
                  <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
                </button>
              </div>
              <template v-else>
                <div class="relative">
                  <input v-model="userSearchTerm" type="text" placeholder="Search by name or email..."
                    :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                      errors.existingUser ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                    @input="errors.existingUser = ''" />
                  <span v-if="userSearchLoading" class="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
                </div>
                <p v-if="errors.existingUser" class="text-[0.68rem] text-status-red mt-1">{{ errors.existingUser }}</p>
                <div v-if="userSearchResults.length > 0" class="mt-1.5 bg-surface-card border border-border rounded-[10px] overflow-hidden shadow-lg">
                  <button v-for="user in userSearchResults" :key="user.id" type="button"
                    class="w-full flex items-center gap-2.5 px-3 py-2.5 text-left hover:bg-white/[0.05] transition-colors border-b border-border last:border-0"
                    @click="selectUser(user)">
                    <div class="flex items-center justify-center w-7 h-7 rounded-full bg-primary-500/10 text-primary-400 text-[0.65rem] font-bold shrink-0">
                      {{ user.firstName.charAt(0) }}{{ user.lastName.charAt(0) }}
                    </div>
                    <div class="min-w-0">
                      <p class="text-[0.82rem] text-text-primary font-medium truncate">{{ user.firstName }} {{ user.lastName }}</p>
                      <p class="text-[0.68rem] text-text-muted truncate">{{ user.email }}</p>
                    </div>
                  </button>
                </div>
              </template>
            </div>
          </div>

          <!-- Profile Details -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Profile Details</h2>

            <div class="flex flex-col gap-3">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">First Name <span class="text-status-red">*</span></label>
                  <input v-model="firstName" type="text" placeholder="John"
                    :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                      errors.firstName ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                    @input="errors.firstName = ''" />
                  <p v-if="errors.firstName" class="text-[0.68rem] text-status-red mt-1">{{ errors.firstName }}</p>
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Last Name <span class="text-status-red">*</span></label>
                  <input v-model="lastName" type="text" placeholder="Doe"
                    :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                      errors.lastName ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                    @input="errors.lastName = ''" />
                  <p v-if="errors.lastName" class="text-[0.68rem] text-status-red mt-1">{{ errors.lastName }}</p>
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Company Name</label>
                  <input v-model="companyName" type="text" placeholder="Acme Inc."
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Phone</label>
                  <div class="flex gap-1.5">
                    <div class="shrink-0 w-[7rem]">
                      <AppSelect v-model="phoneCountry" :options="phoneCodeOptions" searchable dropdown-width="18rem" />
                    </div>
                    <input v-model="phone" type="text" placeholder="Phone number"
                      class="flex-1 min-w-0 bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Language</label>
                  <AppSelect v-model="language" :options="languageOptions" placeholder="Default" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
                  <AppSelect v-model="status" :options="statusOptions" />
                </div>
              </div>
            </div>
          </div>

          <!-- Address & Billing -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Address & Billing</h2>

            <div class="flex flex-col gap-3">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 1</label>
                  <input v-model="street" type="text" placeholder="123 Main Street"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 2</label>
                  <input v-model="address2" type="text" placeholder="Suite 100"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">City</label>
                  <input v-model="city" type="text" placeholder="New York"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">State / Region</label>
                  <input v-model="state" type="text" placeholder="NY"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Postcode</label>
                  <input v-model="postCode" type="text" placeholder="10001"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Country</label>
                  <AppSelect v-model="country" :options="countryOptions" placeholder="Select country" />
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Currency</label>
                  <AppSelect v-model="currency" :options="currencyOptions" placeholder="Select currency" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Payment Method</label>
                  <input v-model="paymentMethod" type="text" placeholder="Credit Card, Bank Transfer..."
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
              </div>

              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Billing Contact</label>
                <input v-model="billingContact" type="text" placeholder="John Doe"
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
              </div>
            </div>
          </div>

        </div>

        <!-- RIGHT COLUMN -->
        <div class="flex flex-col gap-5">

          <!-- Email Notifications -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Email Notifications</h2>

            <div class="flex flex-col gap-3">
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="notifyGeneral" />
                <span class="text-[0.82rem] text-text-secondary">General Emails</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="notifyInvoice" />
                <span class="text-[0.82rem] text-text-secondary">Invoice Emails</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="notifySupport" />
                <span class="text-[0.82rem] text-text-secondary">Support Emails</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="notifyProduct" />
                <span class="text-[0.82rem] text-text-secondary">Product Emails</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="notifyDomain" />
                <span class="text-[0.82rem] text-text-secondary">Domain Emails</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="notifyAffiliate" />
                <span class="text-[0.82rem] text-text-secondary">Affiliate Emails</span>
              </label>
            </div>
          </div>

          <!-- Settings -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Settings</h2>

            <div class="flex flex-col gap-3">
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="lateFees" />
                <span class="text-[0.82rem] text-text-secondary">Late Fees</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="overdueNotices" />
                <span class="text-[0.82rem] text-text-secondary">Overdue Notices</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="taxExempt" />
                <span class="text-[0.82rem] text-text-secondary">Tax Exempt</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="separateInvoices" />
                <span class="text-[0.82rem] text-text-secondary">Separate Invoices</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="disableCcProcessing" />
                <span class="text-[0.82rem] text-text-secondary">Disable CC Processing</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="marketingOptIn" />
                <span class="text-[0.82rem] text-text-secondary">Marketing Emails Opt-in</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="statusUpdate" />
                <span class="text-[0.82rem] text-text-secondary">Status Update</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer">
                <ToggleSwitch v-model="allowSso" />
                <span class="text-[0.82rem] text-text-secondary">Allow Single Sign-On</span>
              </label>
            </div>
          </div>

          <!-- Admin Notes -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Admin Notes</h2>
            <textarea v-model="adminNotes" rows="4" placeholder="Internal notes visible only to admins..."
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none" />
          </div>

        </div>

      </div>

    </form>

  </div>
</template>
