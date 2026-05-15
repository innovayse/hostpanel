<script setup lang="ts">
/**
 * Modal for adding or editing a client contact.
 * Shows a two-column form with profile, address, and notification fields.
 */
import { ref, computed, onMounted } from 'vue'
import type { Contact } from '../../../types/models'
import { useGeoOptions } from '../../../composables/useGeoOptions'
import AppSelect from '../../../components/AppSelect.vue'
import ToggleSwitch from '../../../components/ToggleSwitch.vue'

const props = defineProps<{
  /** Contact to edit, or null for create mode. */
  contact: Contact | null
  /** True while save is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted with the form payload when saved. */
  save: [payload: Record<string, unknown>]
  /** Emitted when delete is clicked in edit mode. */
  delete: []
  /** Emitted when the modal is closed without saving. */
  close: []
}>()

/** Whether we are editing an existing contact. */
const isEditing = computed(() => props.contact !== null)

// --- Profile fields ---

/** Contact first name. */
const firstName = ref('')

/** Contact last name. */
const lastName = ref('')

/** Optional company name. */
const companyName = ref('')

/** Contact email address. */
const email = ref('')

/** Phone number without country code. */
const phone = ref('')

/** Selected phone country ISO2 code. */
const phoneCountry = ref('US')

/** Contact type (Billing, Technical, General). */
const contactType = ref('General')

// --- Address fields ---

/** Street address line 1. */
const street = ref('')

/** Street address line 2. */
const address2 = ref('')

/** City. */
const city = ref('')

/** State or region. */
const state = ref('')

/** Postal code. */
const postCode = ref('')

/** Country ISO2 code. */
const country = ref('')

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

// --- Validation ---

/** Field-level validation errors. */
const errors = ref({ firstName: '', lastName: '', email: '' })

/** True when any validation error is present. */
const hasErrors = computed(() => Object.values(errors.value).some(Boolean))

// --- Geo-atlas data ---

const { geoCountries, countryOptions, phoneCodeOptions, resolvePhoneCode, parsePhone } = useGeoOptions()

/** Resolves the selected country's phone code for the payload. */
const selectedPhoneCode = computed(() => resolvePhoneCode(phoneCountry.value))

/** Contact type options. */
const contactTypeOptions = [
  { value: 'Billing', label: 'Billing' },
  { value: 'Technical', label: 'Technical' },
  { value: 'General', label: 'General' },
]

/**
 * Validates required fields.
 *
 * @returns True when all required fields are filled.
 */
function validate(): boolean {
  errors.value = { firstName: '', lastName: '', email: '' }
  if (!firstName.value.trim()) errors.value.firstName = 'First name is required'
  if (!lastName.value.trim()) errors.value.lastName = 'Last name is required'
  if (!email.value.trim()) errors.value.email = 'Email is required'
  return !hasErrors.value
}

/**
 * Validates and emits the save event with form payload.
 */
function handleSave(): void {
  if (!validate()) return

  emit('save', {
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    companyName: companyName.value.trim() || null,
    email: email.value.trim(),
    phone: phone.value.trim() ? `${selectedPhoneCode.value} ${phone.value.trim()}` : null,
    type: contactType.value,
    street: street.value.trim() || null,
    address2: address2.value.trim() || null,
    city: city.value.trim() || null,
    state: state.value.trim() || null,
    postCode: postCode.value.trim() || null,
    country: country.value || null,
    notifyGeneral: notifyGeneral.value,
    notifyInvoice: notifyInvoice.value,
    notifySupport: notifySupport.value,
    notifyProduct: notifyProduct.value,
    notifyDomain: notifyDomain.value,
    notifyAffiliate: notifyAffiliate.value,
  })
}

/**
 * Confirms and emits the delete event.
 */
function handleDelete(): void {
  if (confirm('Permanently delete this contact? This cannot be undone.')) {
    emit('delete')
  }
}

/**
 * Populates form fields from the contact prop on mount.
 */
function populateFromContact(): void {
  const c = props.contact
  if (!c) return

  firstName.value = c.firstName
  lastName.value = c.lastName
  companyName.value = c.companyName ?? ''
  email.value = c.email
  contactType.value = c.type

  // Parse phone — try to extract country code and number
  if (c.phone) {
    const parsed = parsePhone(c.phone)
    phoneCountry.value = parsed.countryIso2
    phone.value = parsed.number
  }

  street.value = c.street ?? ''
  address2.value = c.address2 ?? ''
  city.value = c.city ?? ''
  state.value = c.state ?? ''
  postCode.value = c.postCode ?? ''
  country.value = c.country ?? ''

  notifyGeneral.value = c.notifyGeneral
  notifyInvoice.value = c.notifyInvoice
  notifySupport.value = c.notifySupport
  notifyProduct.value = c.notifyProduct
  notifyDomain.value = c.notifyDomain
  notifyAffiliate.value = c.notifyAffiliate
}

onMounted(() => {
  if (props.contact) populateFromContact()
})
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center">
    <!-- Backdrop -->
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')" />

    <!-- Modal -->
    <div class="relative bg-surface-card border border-border rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">

      <!-- Header -->
      <div class="px-6 pt-5 pb-4 border-b border-border flex items-center justify-between">
        <h2 class="font-display font-bold text-[1.1rem] text-text-primary">
          {{ isEditing ? 'Edit Contact' : 'Add Contact' }}
        </h2>
        <button
          class="w-8 h-8 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
          @click="emit('close')"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      <!-- Body -->
      <form @submit.prevent="handleSave">
        <div class="px-6 py-5 grid grid-cols-1 md:grid-cols-2 gap-5">

          <!-- LEFT COLUMN: Profile -->
          <div class="flex flex-col gap-3">
            <h3 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Profile</h3>

            <!-- First Name -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">First Name <span class="text-status-red">*</span></label>
              <input
                v-model="firstName"
                type="text"
                :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                  errors.firstName ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                @input="errors.firstName = ''"
              />
              <p v-if="errors.firstName" class="text-[0.68rem] text-status-red mt-1">{{ errors.firstName }}</p>
            </div>

            <!-- Last Name -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Last Name <span class="text-status-red">*</span></label>
              <input
                v-model="lastName"
                type="text"
                :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                  errors.lastName ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                @input="errors.lastName = ''"
              />
              <p v-if="errors.lastName" class="text-[0.68rem] text-status-red mt-1">{{ errors.lastName }}</p>
            </div>

            <!-- Company Name -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Company Name</label>
              <input
                v-model="companyName"
                type="text"
                placeholder="Company"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Email -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Email <span class="text-status-red">*</span></label>
              <input
                v-model="email"
                type="email"
                placeholder="contact@example.com"
                :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                  errors.email ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                @input="errors.email = ''"
              />
              <p v-if="errors.email" class="text-[0.68rem] text-status-red mt-1">{{ errors.email }}</p>
            </div>

            <!-- Phone -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Phone</label>
              <div class="flex gap-1.5">
                <div class="shrink-0 w-[7rem]">
                  <AppSelect v-model="phoneCountry" :options="phoneCodeOptions" searchable dropdown-width="18rem" />
                </div>
                <input
                  v-model="phone"
                  type="text"
                  placeholder="Phone number"
                  class="flex-1 min-w-0 bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                />
              </div>
            </div>

            <!-- Contact Type -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Contact Type</label>
              <AppSelect v-model="contactType" :options="contactTypeOptions" />
            </div>
          </div>

          <!-- RIGHT COLUMN: Address + Notifications -->
          <div class="flex flex-col gap-3">
            <h3 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Address</h3>

            <!-- Address 1 -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 1</label>
              <input
                v-model="street"
                type="text"
                placeholder="Street address"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Address 2 -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 2</label>
              <input
                v-model="address2"
                type="text"
                placeholder="Suite, apt, etc."
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- City + State -->
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">City</label>
                <input
                  v-model="city"
                  type="text"
                  placeholder="City"
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                />
              </div>
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">State / Region</label>
                <input
                  v-model="state"
                  type="text"
                  placeholder="State"
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                />
              </div>
            </div>

            <!-- Postcode + Country -->
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Postcode</label>
                <input
                  v-model="postCode"
                  type="text"
                  placeholder="Postcode"
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                />
              </div>
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Country</label>
                <AppSelect v-model="country" :options="countryOptions" searchable dropdown-width="18rem" placeholder="Select country" />
              </div>
            </div>

            <!-- Notification Toggles -->
            <h3 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-3 mb-1">Email Notifications</h3>

            <div class="flex flex-col gap-2.5">
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
        </div>

        <!-- Footer -->
        <div class="flex items-center justify-between px-6 py-4 border-t border-border">
          <div>
            <button
              v-if="isEditing"
              type="button"
              class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-red/80 rounded-[9px] hover:bg-status-red transition-colors"
              @click="handleDelete"
            >
              Delete Contact
            </button>
          </div>

          <div class="flex items-center gap-2">
            <button
              type="button"
              class="px-4 py-2 text-[0.82rem] font-medium text-text-secondary bg-white/[0.05] border border-border rounded-[9px] hover:text-text-primary hover:border-border/80 transition-colors"
              @click="emit('close')"
            >
              Cancel
            </button>
            <button
              type="submit"
              :disabled="saving"
              class="gradient-brand px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90 disabled:opacity-50"
            >
              {{ saving ? 'Saving...' : (isEditing ? 'Save Changes' : 'Add Contact') }}
            </button>
          </div>
        </div>
      </form>
    </div>
  </div>
</template>
