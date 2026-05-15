<script setup lang="ts">
/**
 * Client profile editing page — displays and allows editing of all client fields.
 *
 * Rendered inside {@link ClientLayout} which handles data fetching and the header.
 * All sections are on a single scrollable page in a two-column layout.
 */
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useClientsStore } from '../stores/clientsStore'
import { CLIENT_STATUS_OPTIONS, CONTACT_TYPE_STYLES } from '../../../utils/constants'
import { useGeoOptions } from '../../../composables/useGeoOptions'
import AppSelect from '../../../components/AppSelect.vue'
import ToggleSwitch from '../../../components/ToggleSwitch.vue'

const route = useRoute()
const store = useClientsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** True while save is in flight. */
const saving = ref(false)

/** Success message after save. */
const saveSuccess = ref(false)

/** Error message after save. */
const saveError = ref<string | null>(null)

// --- Profile fields ---

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

/** Client status. */
const status = ref('Active')

/** Client email (read-only, from Identity). */
const email = ref('')

/** Client ID (read-only). */
const id = ref(0)

/** Joined date (read-only). */
const createdAt = ref('')

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

// --- Geo-atlas data ---

const { geoCountries, countryOptions, phoneCodeOptions, resolvePhoneCode, parsePhone } = useGeoOptions()

/** Resolves the selected country's phone code for the payload. */
const selectedPhoneCode = computed(() => resolvePhoneCode(phoneCountry.value))

/** Currency options for the AppSelect dropdown. */
const currencyOptions = computed(() =>
  [{ value: '', label: 'Select currency' }, ...store.currencies.map(c => ({ value: c.code, label: `${c.name} (${c.symbol})` }))]
)

/** Status options for the dropdown. */
const statusOptions = CLIENT_STATUS_OPTIONS

/** Contact type badge styles. */
const contactTypeStyles = CONTACT_TYPE_STYLES

// --- Validation ---

/** Field-level validation errors. */
const errors = ref({ firstName: '', lastName: '' })

/** True when any validation error is present. */
const hasErrors = computed(() => Object.values(errors.value).some(Boolean))

/**
 * Validates required fields.
 *
 * @returns True when all required fields are filled.
 */
function validate(): boolean {
  errors.value = { firstName: '', lastName: '' }
  if (!firstName.value.trim()) errors.value.firstName = 'First name is required'
  if (!lastName.value.trim()) errors.value.lastName = 'Last name is required'
  return !hasErrors.value
}

/**
 * Populates all form refs from the store's current client.
 */
function populateForm(): void {
  const c = store.current
  if (!c) return

  id.value = c.id
  email.value = c.email ?? ''
  firstName.value = c.firstName
  lastName.value = c.lastName
  companyName.value = c.companyName ?? ''
  status.value = c.status
  createdAt.value = c.createdAt

  // Parse phone — try to extract country code and number
  if (c.phone) {
    const parsed = parsePhone(c.phone)
    phoneCountry.value = parsed.countryIso2
    phone.value = parsed.number
  } else {
    phone.value = ''
  }

  street.value = c.street ?? ''
  address2.value = c.address2 ?? ''
  city.value = c.city ?? ''
  state.value = c.state ?? ''
  postCode.value = c.postCode ?? ''
  country.value = c.country ?? ''
  currency.value = c.currency ?? ''
  paymentMethod.value = c.paymentMethod ?? ''
  billingContact.value = c.billingContact ?? ''
  adminNotes.value = c.adminNotes ?? ''

  notifyGeneral.value = c.notifyGeneral
  notifyInvoice.value = c.notifyInvoice
  notifySupport.value = c.notifySupport
  notifyProduct.value = c.notifyProduct
  notifyDomain.value = c.notifyDomain
  notifyAffiliate.value = c.notifyAffiliate

  lateFees.value = c.lateFees
  overdueNotices.value = c.overdueNotices
  taxExempt.value = c.taxExempt
  separateInvoices.value = c.separateInvoices
  disableCcProcessing.value = c.disableCcProcessing
  marketingOptIn.value = c.marketingOptIn
  statusUpdate.value = c.statusUpdate
  allowSso.value = c.allowSso
}

/** Resets form to the last saved state. */
function handleCancel(): void {
  populateForm()
  saveError.value = null
  saveSuccess.value = false
}

/** Submits all changes to the API. */
async function handleSave(): Promise<void> {
  if (!validate()) return

  saveError.value = null
  saveSuccess.value = false
  saving.value = true

  try {
    await store.updateClient(clientId.value, {
      email: email.value.trim() || null,
      firstName: firstName.value.trim(),
      lastName: lastName.value.trim(),
      companyName: companyName.value.trim() || null,
      phone: phone.value.trim() ? `${selectedPhoneCode.value} ${phone.value.trim()}` : null,
      street: street.value.trim() || null,
      address2: address2.value.trim() || null,
      city: city.value.trim() || null,
      state: state.value.trim() || null,
      postCode: postCode.value.trim() || null,
      country: country.value || null,
      currency: currency.value || null,
      paymentMethod: paymentMethod.value.trim() || null,
      billingContact: billingContact.value.trim() || null,
      adminNotes: adminNotes.value.trim() || null,
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
    })
    saveSuccess.value = true
    setTimeout(() => { saveSuccess.value = false }, 3000)
  } catch {
    saveError.value = 'Failed to save changes.'
  } finally {
    saving.value = false
  }
}

// Populate form when store data loads
watch(() => store.current, () => populateForm(), { immediate: true })
</script>

<template>
  <div v-if="store.current" class="p-4 sm:p-6 lg:p-8 w-full">

    <form @submit.prevent="handleSave">

      <!-- Action bar -->
      <div class="flex items-center justify-end gap-2.5 mb-5">
        <!-- Success/Error feedback -->
        <div v-if="saveSuccess" class="flex-1 px-4 py-2.5 text-[0.82rem] text-status-green bg-status-green/10 border border-status-green/20 rounded-xl">
          Changes saved successfully.
        </div>
        <div v-if="saveError" class="flex-1 px-4 py-2.5 text-[0.82rem] text-status-red bg-status-red/10 border border-status-red/20 rounded-xl">
          {{ saveError }}
        </div>

        <button
          type="button"
          class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
          @click="handleCancel"
        >
          Cancel
        </button>
        <button
          type="submit"
          :disabled="saving"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
        >
          {{ saving ? 'Saving...' : 'Save Changes' }}
        </button>
      </div>

      <!-- Two-column layout -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">

        <!-- LEFT COLUMN -->
        <div class="flex flex-col gap-5">

          <!-- Profile -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Profile</h2>

            <div class="flex flex-col gap-3">
              <!-- Read-only: Client ID + Joined -->
              <div class="flex items-center gap-4 text-[0.78rem] text-text-muted mb-1">
                <span>Client ID: <span class="font-mono text-text-secondary">{{ id }}</span></span>
                <span>Joined: <span class="text-text-secondary">{{ new Date(createdAt).toLocaleDateString() }}</span></span>
              </div>

              <!-- Email -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Email</label>
                <input v-model="email" type="email" placeholder="client@example.com"
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">First Name <span class="text-status-red">*</span></label>
                  <input v-model="firstName" type="text"
                    :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                      errors.firstName ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                    @input="errors.firstName = ''" />
                  <p v-if="errors.firstName" class="text-[0.68rem] text-status-red mt-1">{{ errors.firstName }}</p>
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Last Name <span class="text-status-red">*</span></label>
                  <input v-model="lastName" type="text"
                    :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                      errors.lastName ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                    @input="errors.lastName = ''" />
                  <p v-if="errors.lastName" class="text-[0.68rem] text-status-red mt-1">{{ errors.lastName }}</p>
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Company Name</label>
                  <input v-model="companyName" type="text" placeholder="Company"
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

              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
                <AppSelect v-model="status" :options="statusOptions" />
              </div>
            </div>
          </div>

          <!-- Billing Address -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Billing Address</h2>

            <div class="flex flex-col gap-3">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 1</label>
                  <input v-model="street" type="text" placeholder="Street address"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 2</label>
                  <input v-model="address2" type="text" placeholder="Suite, apt, etc."
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">City</label>
                  <input v-model="city" type="text" placeholder="City"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">State / Region</label>
                  <input v-model="state" type="text" placeholder="State"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Postcode</label>
                  <input v-model="postCode" type="text" placeholder="Postcode"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Country</label>
                  <AppSelect v-model="country" :options="countryOptions" searchable dropdown-width="18rem" placeholder="Select country" />
                </div>
              </div>
            </div>
          </div>

          <!-- Billing Preferences -->
          <div class="bg-surface-card border border-border rounded-2xl p-5">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Billing Preferences</h2>

            <div class="flex flex-col gap-3">
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
                <input v-model="billingContact" type="text" placeholder="Billing contact name"
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
          <div class="bg-surface-card border border-border rounded-2xl p-5 flex-1 flex flex-col">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Admin Notes</h2>
            <textarea v-model="adminNotes" placeholder="Internal notes visible only to admins..."
              class="w-full flex-1 min-h-[8rem] bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none" />
          </div>

        </div>

      </div>

      <!-- Contacts section (read-only, below both columns) -->
      <div class="mt-5">
        <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
          <div class="px-5 py-3.5 border-b border-border">
            <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted">
              Contacts ({{ store.current.contacts.length }})
            </h2>
          </div>

          <template v-if="store.current.contacts.length > 0">
            <div class="hidden sm:grid grid-cols-[2fr_2fr_1.5fr_1fr] gap-4 px-5 py-2.5 border-b border-border bg-white/[0.02]">
              <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Name</span>
              <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Email</span>
              <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Phone</span>
              <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Type</span>
            </div>

            <div
              v-for="contact in store.current.contacts"
              :key="contact.id"
              class="grid grid-cols-1 sm:grid-cols-[2fr_2fr_1.5fr_1fr] gap-2 sm:gap-4 px-5 py-3 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
            >
              <span class="text-[0.82rem] text-text-primary font-medium">{{ contact.firstName }} {{ contact.lastName }}</span>
              <span class="text-[0.82rem] text-text-secondary">{{ contact.email }}</span>
              <span class="text-[0.82rem] text-text-muted">{{ contact.phone || '\u2014' }}</span>
              <span
                class="inline-flex w-fit px-2 py-0.5 rounded-full text-[0.65rem] font-semibold border"
                :class="contactTypeStyles[contact.type] ?? contactTypeStyles.General"
              >
                {{ contact.type }}
              </span>
            </div>
          </template>

          <div v-else class="px-5 py-8 text-center">
            <p class="text-[0.82rem] text-text-muted">No contacts added yet</p>
          </div>
        </div>
      </div>

    </form>
  </div>
</template>
