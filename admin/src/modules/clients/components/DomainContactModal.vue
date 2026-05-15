<script setup lang="ts">
/**
 * Modal for modifying the WHOIS registrant contact details for a domain.
 * Displays a two-column form with standard contact/address fields.
 */
import { ref } from 'vue'

defineProps<{
  /** True while the save operation is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted with the contact form payload when saved. */
  save: [payload: Record<string, string>]
  /** Emitted when the modal is closed without saving. */
  close: []
}>()

// --- Contact fields ---

/** Registrant first name. */
const firstName = ref('')

/** Registrant last name. */
const lastName = ref('')

/** Organization name. */
const organization = ref('')

/** Registrant email address. */
const email = ref('')

/** Registrant phone number. */
const phone = ref('')

/** Street address line 1. */
const address1 = ref('')

/** Street address line 2. */
const address2 = ref('')

/** City. */
const city = ref('')

/** State or province. */
const state = ref('')

/** Postal code. */
const postalCode = ref('')

/** Country. */
const country = ref('')

/**
 * Emits the save event with all contact field values.
 */
function handleSave(): void {
  emit('save', {
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    organization: organization.value.trim(),
    email: email.value.trim(),
    phone: phone.value.trim(),
    address1: address1.value.trim(),
    address2: address2.value.trim(),
    city: city.value.trim(),
    state: state.value.trim(),
    postalCode: postalCode.value.trim(),
    country: country.value.trim(),
  })
}
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
          Modify Contact Details
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
        <div class="px-6 py-5">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">

            <!-- First Name -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">First Name</label>
              <input
                v-model="firstName"
                type="text"
                placeholder="John"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Last Name -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Last Name</label>
              <input
                v-model="lastName"
                type="text"
                placeholder="Doe"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Organization -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Organization</label>
              <input
                v-model="organization"
                type="text"
                placeholder="Acme Inc."
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Email -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Email</label>
              <input
                v-model="email"
                type="email"
                placeholder="john@example.com"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Phone -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Phone</label>
              <input
                v-model="phone"
                type="text"
                placeholder="+1.5551234567"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Address 1 -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 1</label>
              <input
                v-model="address1"
                type="text"
                placeholder="123 Main St"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Address 2 -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Address 2</label>
              <input
                v-model="address2"
                type="text"
                placeholder="Suite 100"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- City -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">City</label>
              <input
                v-model="city"
                type="text"
                placeholder="New York"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- State -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">State</label>
              <input
                v-model="state"
                type="text"
                placeholder="NY"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Postal Code -->
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Postal Code</label>
              <input
                v-model="postalCode"
                type="text"
                placeholder="10001"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

            <!-- Country -->
            <div class="sm:col-span-2">
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Country</label>
              <input
                v-model="country"
                type="text"
                placeholder="US"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>

          </div>
        </div>

        <!-- Footer -->
        <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-border">
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
            {{ saving ? 'Saving...' : 'Save Changes' }}
          </button>
        </div>
      </form>
    </div>
  </div>
</template>
