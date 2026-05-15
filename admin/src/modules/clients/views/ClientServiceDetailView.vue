<script setup lang="ts">
/**
 * Editable service detail page -- displays and allows editing all service fields.
 * Supports lifecycle actions: Suspend, Unsuspend, Terminate.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '../../../composables/useApi'
import { SERVICE_STATUS_STYLES, SERVICE_STATUS_OPTIONS } from '../../../utils/constants'
import { formatDate, toDateInputValue } from '../../../utils/format'
import AppSelect from '../../../components/AppSelect.vue'
import ToggleSwitch from '../../../components/ToggleSwitch.vue'
import type { ServiceDetail } from '../../../types/models'

const route = useRoute()
const router = useRouter()
const { request } = useApi()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Service ID from route params. */
const serviceId = computed(() => route.params.serviceId as string)

/** Fetched service detail, null until loaded. */
const service = ref<ServiceDetail | null>(null)

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

/** True while a module command (suspend/unsuspend/terminate) is in flight. */
const commandLoading = ref(false)

// --- Editable fields ---

/** Service domain. */
const domain = ref('')

/** Dedicated IP address. */
const dedicatedIp = ref('')

/** Control panel username. */
const username = ref('')

/** Control panel password. */
const password = ref('')

/** Recurring charge amount. */
const recurringAmount = ref(0)

/** Next due date (ISO date string for input[type=date]). */
const nextDueDate = ref('')

/** Billing cycle. */
const billingCycle = ref('monthly')

/** Payment method. */
const paymentMethod = ref('')

/** Override auto-suspend toggle. */
const overrideAutoSuspend = ref(false)

/** Suspend-until date (ISO date string for input[type=date]). */
const suspendUntil = ref('')

/** Auto-terminate at end of cycle toggle. */
const autoTerminateEndOfCycle = ref(false)

/** Auto-terminate reason. */
const autoTerminateReason = ref('')

/** Internal admin notes. */
const adminNotes = ref('')

/** Provisioning reference / server. */
const provisioningRef = ref('')

/** First payment amount. */
const firstPaymentAmount = ref(0)

/** Promotion code. */
const promotionCode = ref('')

/** Termination date (ISO date string). */
const terminatedAt = ref('')

/** Registration date (ISO date string). */
const createdAt = ref('')

/** Status (editable dropdown). */
const status = ref('Active')

/** Subscription ID. */
const subscriptionId = ref('')

/** Status options for dropdown. */
const statusOptions = SERVICE_STATUS_OPTIONS

/** Status badge style map. */
const statusStyles = SERVICE_STATUS_STYLES

/** Billing cycle options for the AppSelect dropdown. */
const billingCycleOptions = [
  { value: 'monthly', label: 'Monthly' },
  { value: 'annual', label: 'Annual' },
]

/**
 * Populates all form refs from the fetched service detail.
 */
function populateForm(): void {
  const s = service.value
  if (!s) return

  domain.value = s.domain ?? ''
  dedicatedIp.value = s.dedicatedIp ?? ''
  username.value = s.username ?? ''
  password.value = s.password ?? ''
  recurringAmount.value = s.recurringAmount
  firstPaymentAmount.value = s.firstPaymentAmount
  nextDueDate.value = toDateInputValue(s.nextDueDate)
  billingCycle.value = s.billingCycle || 'monthly'
  paymentMethod.value = s.paymentMethod ?? ''
  provisioningRef.value = s.provisioningRef ?? ''
  promotionCode.value = s.promotionCode ?? ''
  subscriptionId.value = s.subscriptionId ?? ''
  terminatedAt.value = toDateInputValue(s.terminatedAt)
  createdAt.value = toDateInputValue(s.createdAt)
  status.value = s.status || 'Active'
  overrideAutoSuspend.value = s.overrideAutoSuspend
  suspendUntil.value = toDateInputValue(s.suspendUntil)
  autoTerminateEndOfCycle.value = s.autoTerminateEndOfCycle
  autoTerminateReason.value = s.autoTerminateReason ?? ''
  adminNotes.value = s.adminNotes ?? ''
}

/**
 * Fetches the service detail from the API and populates the form.
 *
 * @returns Promise that resolves when the service is loaded.
 */
async function fetchService(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    service.value = await request<ServiceDetail>(`/services/${serviceId.value}`)
    populateForm()
  } catch {
    error.value = 'Failed to load service details.'
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
    await request(`/services/${serviceId.value}`, {
      method: 'PUT',
      body: JSON.stringify({
        domain: domain.value.trim() || null,
        dedicatedIp: dedicatedIp.value.trim() || null,
        username: username.value.trim() || null,
        password: password.value.trim() || null,
        provisioningRef: provisioningRef.value.trim() || null,
        recurringAmount: recurringAmount.value,
        firstPaymentAmount: firstPaymentAmount.value,
        nextRenewalAt: nextDueDate.value || null,
        createdAt: createdAt.value || null,
        terminatedAt: terminatedAt.value || null,
        billingCycle: billingCycle.value,
        paymentMethod: paymentMethod.value.trim() || null,
        promotionCode: promotionCode.value.trim() || null,
        subscriptionId: subscriptionId.value.trim() || null,
        overrideAutoSuspend: overrideAutoSuspend.value,
        suspendUntil: overrideAutoSuspend.value && suspendUntil.value ? suspendUntil.value : null,
        autoTerminateEndOfCycle: autoTerminateEndOfCycle.value,
        autoTerminateReason: autoTerminateEndOfCycle.value ? autoTerminateReason.value.trim() || null : null,
        adminNotes: adminNotes.value.trim() || null,
        status: status.value,
      }),
    })
    saveSuccess.value = true
    setTimeout(() => { saveSuccess.value = false }, 3000)
    await fetchService()
  } catch {
    saveError.value = 'Failed to save changes.'
  } finally {
    saving.value = false
  }
}

/**
 * Suspends the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleSuspend(): Promise<void> {
  if (!confirm('Are you sure you want to suspend this service?')) return
  commandLoading.value = true
  try {
    await request(`/services/${serviceId.value}/suspend`, { method: 'POST' })
    await fetchService()
  } catch {
    saveError.value = 'Failed to suspend service.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Unsuspends the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleUnsuspend(): Promise<void> {
  if (!confirm('Unsuspend this service and restore active status?')) return
  commandLoading.value = true
  try {
    await request(`/services/${serviceId.value}/unsuspend`, { method: 'POST' })
    await fetchService()
  } catch {
    saveError.value = 'Failed to unsuspend service.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Terminates the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleTerminate(): Promise<void> {
  if (!confirm('Are you sure you want to terminate this service? This action cannot be undone.')) return
  commandLoading.value = true
  try {
    await request(`/services/${serviceId.value}/terminate`, { method: 'POST' })
    await fetchService()
  } catch {
    saveError.value = 'Failed to terminate service.'
  } finally {
    commandLoading.value = false
  }
}

// Populate form when service data changes
watch(service, () => populateForm())

onMounted(() => fetchService())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Loading -->
    <div v-if="loading && !service" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading service...
    </div>

    <!-- Error -->
    <div v-else-if="error && !service" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ error }}
    </div>

    <template v-else-if="service">

      <form @submit.prevent="handleSave">

        <!-- Action bar -->
        <div class="flex items-center justify-between gap-2.5 mb-5">
          <!-- Back to services list -->
          <div class="flex items-center gap-2.5">
            <button
              type="button"
              class="px-3 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors flex items-center gap-1.5"
              @click="router.push(`/clients/${clientId}/services`)"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="15 18 9 12 15 6"/>
              </svg>
              Back
            </button>
            <span class="text-[0.875rem] font-semibold text-text-primary">
              Service #{{ service.id }} &mdash; {{ service.productName }}
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

            <!-- Service Info -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Service Info</h2>

              <div class="flex flex-col gap-3">
                <!-- Order # (read-only) + Product (read-only) -->
                <div class="flex items-center gap-4 text-[0.78rem] text-text-muted mb-1">
                  <span>Order #: <span class="font-mono text-text-secondary">{{ service.id }}</span></span>
                  <span>Product: <span class="text-text-secondary">{{ service.productName }}</span></span>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Server (Provisioning Ref)</label>
                    <input v-model="provisioningRef" type="text" placeholder="cpanel-server-01"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Domain</label>
                    <input v-model="domain" type="text" placeholder="example.com"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Dedicated IP</label>
                    <input v-model="dedicatedIp" type="text" placeholder="192.168.1.1"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Username</label>
                    <input v-model="username" type="text" placeholder="cpanel_user"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Password</label>
                    <input v-model="password" type="password" placeholder="********"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
                    <AppSelect v-model="status" :options="statusOptions" />
                  </div>
                </div>
              </div>
            </div>

            <!-- Module Commands -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Module Commands</h2>

              <!-- Terminated state -->
              <div v-if="service.status === 'Terminated'" class="text-[0.82rem] text-text-muted">
                Service Terminated
              </div>

              <!-- Action buttons -->
              <div v-else class="flex items-center gap-2.5">
                <!-- Active: Suspend + Terminate -->
                <template v-if="service.status === 'Active'">
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-yellow bg-status-yellow/10 border border-status-yellow/20 rounded-[10px] hover:bg-status-yellow/20 transition-colors disabled:opacity-50"
                    @click="handleSuspend"
                  >
                    {{ commandLoading ? 'Processing...' : 'Suspend' }}
                  </button>
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-[10px] hover:bg-status-red/20 transition-colors disabled:opacity-50"
                    @click="handleTerminate"
                  >
                    {{ commandLoading ? 'Processing...' : 'Terminate' }}
                  </button>
                </template>

                <!-- Suspended: Unsuspend + Terminate -->
                <template v-else-if="service.status === 'Suspended'">
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-green bg-status-green/10 border border-status-green/20 rounded-[10px] hover:bg-status-green/20 transition-colors disabled:opacity-50"
                    @click="handleUnsuspend"
                  >
                    {{ commandLoading ? 'Processing...' : 'Unsuspend' }}
                  </button>
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-[10px] hover:bg-status-red/20 transition-colors disabled:opacity-50"
                    @click="handleTerminate"
                  >
                    {{ commandLoading ? 'Processing...' : 'Terminate' }}
                  </button>
                </template>

                <!-- Pending: Terminate only -->
                <template v-else-if="service.status === 'Pending'">
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-[10px] hover:bg-status-red/20 transition-colors disabled:opacity-50"
                    @click="handleTerminate"
                  >
                    {{ commandLoading ? 'Processing...' : 'Terminate' }}
                  </button>
                </template>
              </div>
            </div>

          </div>

          <!-- RIGHT COLUMN -->
          <div class="flex flex-col gap-5">

            <!-- Billing -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Billing</h2>

              <div class="flex flex-col gap-3">
                <!-- Quantity (read-only) -->
                <div class="text-[0.78rem] text-text-muted mb-1">
                  Quantity: <span class="text-text-secondary">{{ service.quantity }}</span>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Registration Date</label>
                    <input v-model="createdAt" type="date"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">First Payment Amount</label>
                    <input v-model.number="firstPaymentAmount" type="number" step="0.01" min="0"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Recurring Amount</label>
                    <input v-model.number="recurringAmount" type="number" step="0.01" min="0"
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
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Termination Date</label>
                    <input v-model="terminatedAt" type="date"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Billing Cycle</label>
                    <AppSelect v-model="billingCycle" :options="billingCycleOptions" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Payment Method</label>
                    <input v-model="paymentMethod" type="text" placeholder="Stripe, PayPal..."
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Promotion Code</label>
                    <input v-model="promotionCode" type="text" placeholder="PROMO2026"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Subscription ID</label>
                  <input v-model="subscriptionId" type="text" placeholder="sub_1234567890"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
              </div>
            </div>

            <!-- Auto-Management -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Auto-Management</h2>

              <div class="flex flex-col gap-3">
                <!-- Override Auto-Suspend -->
                <label class="flex items-center gap-2.5 cursor-pointer">
                  <ToggleSwitch v-model="overrideAutoSuspend" />
                  <span class="text-[0.82rem] text-text-secondary">Override Auto-Suspend</span>
                </label>

                <!-- Suspend Until date (shown when override is on) -->
                <div v-if="overrideAutoSuspend">
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Suspend Until</label>
                  <input v-model="suspendUntil" type="date"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>

                <!-- Auto-Terminate End of Cycle -->
                <label class="flex items-center gap-2.5 cursor-pointer">
                  <ToggleSwitch v-model="autoTerminateEndOfCycle" />
                  <span class="text-[0.82rem] text-text-secondary">Auto-Terminate End of Cycle</span>
                </label>

                <!-- Auto-terminate reason (shown when toggle is on) -->
                <div v-if="autoTerminateEndOfCycle">
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Termination Reason</label>
                  <input v-model="autoTerminateReason" type="text" placeholder="Reason for auto-termination"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
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

    </template>
  </div>
</template>
