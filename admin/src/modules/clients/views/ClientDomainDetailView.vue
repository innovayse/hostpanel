<script setup lang="ts">
/**
 * Editable domain detail page -- displays and allows editing all domain fields.
 * Supports registrar commands, management tool toggles, DNS records, email forwarding, and reminders.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '../../../composables/useApi'
import { DOMAIN_STATUS_OPTIONS, PAYMENT_METHOD_OPTIONS } from '../../../utils/constants'
import { formatDate, toDateInputValue } from '../../../utils/format'
import AppSelect from '../../../components/AppSelect.vue'
import ToggleSwitch from '../../../components/ToggleSwitch.vue'
import DnsRecordsTable from '../components/DnsRecordsTable.vue'
import EmailForwardingTable from '../components/EmailForwardingTable.vue'
import DomainRemindersTable from '../components/DomainRemindersTable.vue'
import DomainContactModal from '../components/DomainContactModal.vue'
import type { DomainDetail } from '../../../types/models'

const route = useRoute()
const router = useRouter()
const { request } = useApi()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Domain ID from route params. */
const domainId = computed(() => route.params.domainId as string)

/** Fetched domain detail, null until loaded. */
const domain = ref<DomainDetail | null>(null)

/** True while the initial load is in flight. */
const loading = ref(false)

/** Error message from the initial load. */
const error = ref<string | null>(null)

/** True while save is in flight. */
const saving = ref(false)

/** Success message after save. */
const saveSuccess = ref(false)

/** Error message after save. */
const saveError = ref<string | null>(null)

/** True while a registrar command is in flight. */
const commandLoading = ref(false)

/** Whether the contact modification modal is visible. */
const showContactModal = ref(false)

/** True while contact modification is being saved. */
const contactSaving = ref(false)

// --- Editable fields ---

/** First payment amount. */
const firstPaymentAmount = ref(0)

/** Recurring renewal amount. */
const recurringAmount = ref(0)

/** Payment method. */
const paymentMethod = ref('')

/** Promotion code. */
const promotionCode = ref('')

/** Subscription ID. */
const subscriptionId = ref('')

/** Internal admin notes. */
const adminNotes = ref('')

/** Expiry date (ISO date string for input[type=date]). */
const expiresAt = ref('')

/** Next due date (ISO date string for input[type=date]). */
const nextDueDate = ref('')

/** Registration period in years. */
const registrationPeriod = ref(1)

/** Domain status. */
const status = ref('Active')

/** Nameserver 1. */
const ns1 = ref('')

/** Nameserver 2. */
const ns2 = ref('')

/** Nameserver 3. */
const ns3 = ref('')

/** Nameserver 4. */
const ns4 = ref('')

/** Nameserver 5. */
const ns5 = ref('')

/**
 * Populates all form refs from the fetched domain detail.
 */
function populateForm(): void {
  const d = domain.value
  if (!d) return

  firstPaymentAmount.value = d.firstPaymentAmount
  recurringAmount.value = d.recurringAmount
  paymentMethod.value = d.paymentMethod ?? ''
  promotionCode.value = d.promotionCode ?? ''
  subscriptionId.value = d.subscriptionId ?? ''
  adminNotes.value = d.adminNotes ?? ''
  expiresAt.value = toDateInputValue(d.expiresAt)
  nextDueDate.value = toDateInputValue(d.nextDueDate)
  registrationPeriod.value = d.registrationPeriod
  status.value = d.status || 'Active'

  const ns = d.nameservers ?? []
  ns1.value = ns[0]?.host ?? ''
  ns2.value = ns[1]?.host ?? ''
  ns3.value = ns[2]?.host ?? ''
  ns4.value = ns[3]?.host ?? ''
  ns5.value = ns[4]?.host ?? ''
}

/**
 * Fetches the domain detail from the API and populates the form.
 *
 * @returns Promise that resolves when the domain is loaded.
 */
async function fetchDomain(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    domain.value = await request<DomainDetail>(`/domains/${domainId.value}`)
    populateForm()
  } catch {
    error.value = 'Failed to load domain details.'
  } finally {
    loading.value = false
  }
}

/** Resets form to the last saved state. */
function handleCancel(): void {
  populateForm()
  saveError.value = null
  saveSuccess.value = false
}

/**
 * Submits all editable field changes to the API.
 *
 * @returns Promise that resolves when the save completes.
 */
async function handleSave(): Promise<void> {
  saveError.value = null
  saveSuccess.value = false
  saving.value = true

  try {
    await request(`/domains/${domainId.value}`, {
      method: 'PUT',
      body: JSON.stringify({
        firstPaymentAmount: firstPaymentAmount.value,
        recurringAmount: recurringAmount.value,
        paymentMethod: paymentMethod.value.trim() || null,
        promotionCode: promotionCode.value.trim() || null,
        subscriptionId: subscriptionId.value.trim() || null,
        adminNotes: adminNotes.value.trim() || null,
        expiresAt: expiresAt.value || null,
        nextDueDate: nextDueDate.value || null,
        registrationPeriod: registrationPeriod.value,
        status: status.value,
        nameservers: [ns1.value, ns2.value, ns3.value, ns4.value, ns5.value].filter(Boolean),
      }),
    })
    saveSuccess.value = true
    setTimeout(() => { saveSuccess.value = false }, 3000)
    await fetchDomain()
  } catch {
    saveError.value = 'Failed to save changes.'
  } finally {
    saving.value = false
  }
}

/**
 * Sends a register command to the registrar API.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleRegister(): Promise<void> {
  if (!confirm('Register this domain with the registrar?')) return
  commandLoading.value = true
  try {
    await request(`/domains/${domainId.value}/register`, { method: 'POST' })
    await fetchDomain()
  } catch {
    saveError.value = 'Failed to register domain.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Prompts for years and sends a renew command to the registrar API.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleRenew(): Promise<void> {
  const input = prompt('Enter number of years to renew:', '1')
  if (!input) return
  const years = parseInt(input, 10)
  if (isNaN(years) || years < 1) return

  commandLoading.value = true
  try {
    await request(`/domains/${domainId.value}/renew`, {
      method: 'POST',
      body: JSON.stringify({ years }),
    })
    await fetchDomain()
  } catch {
    saveError.value = 'Failed to renew domain.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Requests the EPP authorization code from the registrar and shows it.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleGetEppCode(): Promise<void> {
  commandLoading.value = true
  try {
    const result = await request<{ eppCode: string }>(`/domains/${domainId.value}/initiate-outgoing-transfer`, { method: 'POST' })
    alert(`EPP Code: ${result.eppCode}`)
  } catch {
    saveError.value = 'Failed to retrieve EPP code.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Toggles WHOIS ID protection via the registrar API.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleToggleIdProtection(): Promise<void> {
  commandLoading.value = true
  try {
    await request(`/domains/${domainId.value}/whois-privacy`, {
      method: 'PUT',
      body: JSON.stringify({ enabled: !domain.value?.whoisPrivacy }),
    })
    await fetchDomain()
  } catch {
    saveError.value = 'Failed to toggle ID protection.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Saves contact details from the modal to the registrar API.
 *
 * @param contact - Contact payload from DomainContactModal.
 * @returns Promise that resolves when the action completes.
 */
async function handleModifyContact(contact: Record<string, string>): Promise<void> {
  contactSaving.value = true
  try {
    await request(`/domains/${domainId.value}/modify-contact`, {
      method: 'POST',
      body: JSON.stringify(contact),
    })
    await fetchDomain()
    showContactModal.value = false
  } catch {
    saveError.value = 'Failed to modify contact details.'
  } finally {
    contactSaving.value = false
  }
}

/**
 * Toggles DNS management for the domain.
 *
 * @param val - The desired toggle state.
 * @returns Promise that resolves when the action completes.
 */
async function handleToggleDnsManagement(val: boolean): Promise<void> {
  commandLoading.value = true
  try {
    await request(`/domains/${domainId.value}/dns-management`, {
      method: 'PUT',
      body: JSON.stringify({ enabled: val }),
    })
    await fetchDomain()
  } catch {
    saveError.value = 'Failed to toggle DNS management.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Toggles email forwarding for the domain.
 *
 * @param val - The desired toggle state.
 * @returns Promise that resolves when the action completes.
 */
async function handleToggleEmailForwarding(val: boolean): Promise<void> {
  commandLoading.value = true
  try {
    await request(`/domains/${domainId.value}/email-forwarding-toggle`, {
      method: 'PUT',
      body: JSON.stringify({ enabled: val }),
    })
    await fetchDomain()
  } catch {
    saveError.value = 'Failed to toggle email forwarding.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Toggles auto-renew for the domain.
 *
 * @param val - The desired toggle state.
 * @returns Promise that resolves when the action completes.
 */
async function handleToggleAutoRenew(val: boolean): Promise<void> {
  commandLoading.value = true
  try {
    await request(`/domains/${domainId.value}/auto-renew`, {
      method: 'PUT',
      body: JSON.stringify({ enabled: val }),
    })
    await fetchDomain()
  } catch {
    saveError.value = 'Failed to toggle auto-renew.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Toggles registrar lock for the domain.
 *
 * @param val - The desired toggle state.
 * @returns Promise that resolves when the action completes.
 */
async function handleToggleLock(val: boolean): Promise<void> {
  commandLoading.value = true
  try {
    await request(`/domains/${domainId.value}/lock`, {
      method: 'PUT',
      body: JSON.stringify({ enabled: val }),
    })
    await fetchDomain()
  } catch {
    saveError.value = 'Failed to toggle registrar lock.'
  } finally {
    commandLoading.value = false
  }
}

// Populate form when domain data changes
watch(domain, () => populateForm())

onMounted(() => fetchDomain())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Loading -->
    <div v-if="loading && !domain" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading domain...
    </div>

    <!-- Error -->
    <div v-else-if="error && !domain" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ error }}
    </div>

    <template v-else-if="domain">

      <form @submit.prevent="handleSave">

        <!-- Action bar -->
        <div class="flex items-center justify-between gap-2.5 mb-5">
          <!-- Back to domains list -->
          <div class="flex items-center gap-2.5">
            <button
              type="button"
              class="px-3 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors flex items-center gap-1.5"
              @click="router.push(`/clients/${clientId}/domains`)"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="15 18 9 12 15 6"/>
              </svg>
              Back
            </button>
            <span class="text-[0.875rem] font-semibold text-text-primary">
              Domain &mdash; {{ domain.name }}
            </span>
          </div>

          <div class="flex items-center gap-2.5">
            <!-- Success/Error feedback -->
            <div v-if="saveSuccess" class="px-4 py-2.5 text-[0.82rem] text-status-green bg-status-green/10 border border-status-green/20 rounded-xl">
              Changes saved successfully.
            </div>
            <div v-if="saveError" class="px-4 py-2.5 text-[0.82rem] text-status-red bg-status-red/10 border border-status-red/20 rounded-xl">
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
        </div>

        <!-- Two-column layout -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">

          <!-- LEFT COLUMN -->
          <div class="flex flex-col gap-5">

            <!-- Domain Info -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Domain Info</h2>

              <div class="flex flex-col gap-3">
                <!-- Read-only info -->
                <div class="flex items-center gap-4 text-[0.78rem] text-text-muted mb-1">
                  <span>Order #: <span class="font-mono text-text-secondary">{{ domain.orderId ?? '&mdash;' }}</span></span>
                  <span>Order Type: <span class="text-text-secondary">{{ domain.orderType }}</span></span>
                </div>

                <div class="flex items-center gap-4 text-[0.78rem] text-text-muted mb-1">
                  <span>Domain: <span class="text-text-secondary">{{ domain.name }}</span></span>
                  <span>Registrar: <span class="text-text-secondary">{{ domain.registrar ?? '&mdash;' }}</span></span>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">First Payment Amount</label>
                    <input v-model.number="firstPaymentAmount" type="number" step="0.01" min="0"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Recurring Amount</label>
                    <input v-model.number="recurringAmount" type="number" step="0.01" min="0"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Promotion Code</label>
                    <input v-model="promotionCode" type="text" placeholder="PROMO2026"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Subscription ID</label>
                    <input v-model="subscriptionId" type="text" placeholder="sub_1234567890"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>
              </div>
            </div>

            <!-- Nameservers -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Nameservers</h2>

              <div class="flex flex-col gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Nameserver 1</label>
                  <input v-model="ns1" type="text" placeholder="ns1.example.com"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Nameserver 2</label>
                  <input v-model="ns2" type="text" placeholder="ns2.example.com"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Nameserver 3</label>
                  <input v-model="ns3" type="text" placeholder="ns3.example.com"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Nameserver 4</label>
                  <input v-model="ns4" type="text" placeholder="ns4.example.com"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Nameserver 5</label>
                  <input v-model="ns5" type="text" placeholder="ns5.example.com"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
              </div>
            </div>

            <!-- Registrar Commands -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Registrar Commands</h2>

              <div class="flex items-center gap-2.5 flex-wrap">
                <button
                  type="button"
                  :disabled="commandLoading || domain.status === 'Active'"
                  class="px-4 py-2 text-[0.84rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-[10px] hover:bg-primary-500/20 transition-colors disabled:opacity-50"
                  @click="handleRegister"
                >
                  {{ commandLoading ? 'Processing...' : 'Register' }}
                </button>
                <button
                  type="button"
                  :disabled="commandLoading"
                  class="px-4 py-2 text-[0.84rem] font-medium text-status-green bg-status-green/10 border border-status-green/20 rounded-[10px] hover:bg-status-green/20 transition-colors disabled:opacity-50"
                  @click="handleRenew"
                >
                  {{ commandLoading ? 'Processing...' : 'Renew' }}
                </button>
                <button
                  type="button"
                  :disabled="commandLoading"
                  class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.08] transition-colors disabled:opacity-50"
                  @click="showContactModal = true"
                >
                  Modify Contact Details
                </button>
                <button
                  type="button"
                  :disabled="commandLoading"
                  class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.08] transition-colors disabled:opacity-50"
                  @click="handleGetEppCode"
                >
                  {{ commandLoading ? 'Processing...' : 'Get EPP Code' }}
                </button>
                <button
                  type="button"
                  :disabled="commandLoading"
                  class="px-4 py-2 text-[0.84rem] font-medium rounded-[10px] transition-colors disabled:opacity-50"
                  :class="domain.whoisPrivacy
                    ? 'text-status-yellow bg-status-yellow/10 border border-status-yellow/20 hover:bg-status-yellow/20'
                    : 'text-status-green bg-status-green/10 border border-status-green/20 hover:bg-status-green/20'"
                  @click="handleToggleIdProtection"
                >
                  {{ commandLoading ? 'Processing...' : (domain.whoisPrivacy ? 'Disable ID Protection' : 'Enable ID Protection') }}
                </button>
              </div>
            </div>

          </div>

          <!-- RIGHT COLUMN -->
          <div class="flex flex-col gap-5">

            <!-- Dates & Billing -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Dates &amp; Billing</h2>

              <div class="flex flex-col gap-3">
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Registration Period</label>
                    <div class="flex items-center gap-2">
                      <input v-model.number="registrationPeriod" type="number" min="1" max="10" step="1"
                        class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                      <span class="text-[0.82rem] text-text-muted shrink-0">Years</span>
                    </div>
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Registration Date</label>
                    <div class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-muted">
                      {{ formatDate(domain.registeredAt) }}
                    </div>
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Expiry Date</label>
                    <input v-model="expiresAt" type="date"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Next Due Date</label>
                    <input v-model="nextDueDate" type="date"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Payment Method</label>
                    <AppSelect v-model="paymentMethod" :options="PAYMENT_METHOD_OPTIONS" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
                    <AppSelect v-model="status" :options="DOMAIN_STATUS_OPTIONS" />
                  </div>
                </div>
              </div>
            </div>

            <!-- Management Tools -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Management Tools</h2>

              <div class="flex flex-col gap-3">
                <label class="flex items-center gap-2.5 cursor-pointer">
                  <ToggleSwitch :model-value="domain.dnsManagement" @update:model-value="handleToggleDnsManagement" />
                  <span class="text-[0.82rem] text-text-secondary">DNS Management</span>
                </label>

                <label class="flex items-center gap-2.5 cursor-pointer">
                  <ToggleSwitch :model-value="domain.emailForwarding" @update:model-value="handleToggleEmailForwarding" />
                  <span class="text-[0.82rem] text-text-secondary">Email Forwarding</span>
                </label>

                <label class="flex items-center gap-2.5 cursor-pointer">
                  <ToggleSwitch :model-value="domain.whoisPrivacy" @update:model-value="() => handleToggleIdProtection()" />
                  <span class="text-[0.82rem] text-text-secondary">ID Protection</span>
                </label>

                <label class="flex items-center gap-2.5 cursor-pointer">
                  <ToggleSwitch :model-value="domain.autoRenew" @update:model-value="handleToggleAutoRenew" />
                  <span class="text-[0.82rem] text-text-secondary">Auto Renew</span>
                </label>

                <label class="flex items-center gap-2.5 cursor-pointer">
                  <ToggleSwitch :model-value="domain.isLocked" @update:model-value="handleToggleLock" />
                  <span class="text-[0.82rem] text-text-secondary">Registrar Lock</span>
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

      </form>

      <!-- Full-width sections below the two-column grid -->
      <div class="flex flex-col gap-5 mt-5">
        <EmailForwardingTable
          v-if="domain.emailForwarding"
          :domain-id="Number(domainId)"
          :rules="domain.emailForwardingRules"
          @refresh="fetchDomain"
        />

        <DnsRecordsTable
          v-if="domain.dnsManagement"
          :domain-id="Number(domainId)"
          :records="domain.dnsRecords"
          @refresh="fetchDomain"
        />

        <DomainRemindersTable :reminders="domain.reminders" />
      </div>

      <!-- Contact Modal -->
      <DomainContactModal
        v-if="showContactModal"
        :saving="contactSaving"
        @save="handleModifyContact"
        @close="showContactModal = false"
      />

    </template>
  </div>
</template>
