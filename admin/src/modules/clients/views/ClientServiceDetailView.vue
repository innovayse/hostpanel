<script setup lang="ts">
/**
 * Editable service detail page -- displays and allows editing all service fields.
 * Supports lifecycle actions: Suspend, Unsuspend, Terminate.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '../../../composables/useApi'
import { SERVICE_STATUS_STYLES, SERVICE_STATUS_OPTIONS, BILLING_CYCLE_OPTIONS } from '../../../utils/constants'
import { formatDate, toDateInputValue } from '../../../utils/format'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppSelect from '../../../components/AppSelect.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'
import ToggleSwitch from '../../../components/ToggleSwitch.vue'
import type { ServiceDetail, ServiceListItem, PagedResult } from '../../../types/models'

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

/** Quantity of units purchased. */
const quantity = ref(1)

/** FK to the assigned server. */
const serverId = ref<number | null>(null)

/** FK to the ordered product (for changing product). */
const productIdRef = ref<number | null>(null)

/** All services for the current client (for service switcher). */
const clientServices = ref<ServiceListItem[]>([])

/** Available servers for dropdown. */
const serverList = ref<Array<{ id: number; name: string; hostname: string; maxAccounts: number | null; accountsCount: number | null }>>([])

/** Available products for dropdown. */
const productList = ref<Array<{ id: number; name: string }>>([])

/** Whether the change password modal is visible. */
const showChangePasswordModal = ref(false)

/** New password for change password modal. */
const newPassword = ref('')

/** Whether the change package modal is visible. */
const showChangePackageModal = ref(false)

/** New package name for change package modal. */
const newPackage = ref('')

/** Whether the suspend confirmation modal is visible. */
const showSuspendModal = ref(false)

/** Whether the terminate confirmation modal is visible. */
const showTerminateModal = ref(false)

/** Whether SSO URL is loading. */
const ssoLoading = ref(false)

/** Status options for dropdown. */
const statusOptions = SERVICE_STATUS_OPTIONS

/** Status badge style map. */
const statusStyles = SERVICE_STATUS_STYLES

/** Billing cycle options for the AppSelect dropdown. */
const billingCycleOptions = BILLING_CYCLE_OPTIONS

/** Server options for AppSelect dropdown. */
const serverOptions = computed(() => [
  { value: 0, label: 'None' },
  ...serverList.value.map(s => ({
    value: s.id,
    label: `${s.name} — ${s.hostname} (${s.accountsCount ?? 0}/${s.maxAccounts ?? '∞'})`,
  })),
])

/** Product options for AppSelect dropdown. */
const productOptions = computed(() =>
  productList.value.map(p => ({ value: p.id, label: p.name }))
)

/** Service switcher options for AppSelect dropdown. */
const serviceSwitcherOptions = computed(() =>
  clientServices.value.map(s => ({
    value: s.id,
    label: `#${s.id} — ${s.productName}`,
  }))
)

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
  quantity.value = s.quantity
  serverId.value = s.serverId ?? null
  productIdRef.value = s.productId
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

/**
 * Fetches all services for the current client (service switcher).
 *
 * @returns Promise that resolves when services are loaded.
 */
async function fetchClientServices(): Promise<void> {
  try {
    const result = await request<PagedResult<ServiceListItem>>(
      `/services?clientId=${clientId.value}&pageSize=100`)
    clientServices.value = result.items
  } catch { /* silent */ }
}

/**
 * Fetches all servers for the server dropdown.
 *
 * @returns Promise that resolves when servers are loaded.
 */
async function fetchServers(): Promise<void> {
  try {
    serverList.value = await request<typeof serverList.value>('/admin/servers')
  } catch { /* silent */ }
}

/**
 * Fetches all products for the product dropdown.
 *
 * @returns Promise that resolves when products are loaded.
 */
async function fetchProducts(): Promise<void> {
  try {
    const result = await request<Array<{ id: number; name: string }>>('/products?activeOnly=false')
    productList.value = result
  } catch { /* silent */ }
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
        quantity: quantity.value,
        serverId: serverId.value || null,
        productId: productIdRef.value,
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
 * Opens a cPanel SSO URL in a new browser tab.
 *
 * @returns Promise that resolves when the SSO URL is retrieved.
 */
async function handleLoginToPanel(): Promise<void> {
  ssoLoading.value = true
  try {
    const result = await request<{ url: string }>(`/provisioning/${serviceId.value}/cpanel-sso`)
    window.open(result.url, '_blank')
  } catch {
    saveError.value = 'Failed to generate control panel login URL.'
  } finally {
    ssoLoading.value = false
  }
}

/**
 * Provisions a pending service on the hosting server.
 *
 * @returns Promise that resolves when provisioning completes.
 */
async function handleProvision(): Promise<void> {
  commandLoading.value = true
  try {
    await request(`/provisioning/${serviceId.value}/provision`, { method: 'POST' })
    await fetchService()
  } catch {
    saveError.value = 'Failed to provision service.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Changes the hosting account password via the provisioning server.
 *
 * @returns Promise that resolves when the password is changed.
 */
async function handleChangePassword(): Promise<void> {
  if (!newPassword.value.trim()) return
  commandLoading.value = true
  try {
    await request(`/provisioning/${serviceId.value}/change-password`, {
      method: 'POST',
      body: JSON.stringify({ newPassword: newPassword.value }),
    })
    showChangePasswordModal.value = false
    newPassword.value = ''
    await fetchService()
  } catch {
    saveError.value = 'Failed to change password.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Changes the hosting package via the provisioning server.
 *
 * @returns Promise that resolves when the package is changed.
 */
async function handleChangePackage(): Promise<void> {
  if (!newPackage.value.trim()) return
  commandLoading.value = true
  try {
    await request(`/provisioning/${serviceId.value}/change-package`, {
      method: 'POST',
      body: JSON.stringify({ newPackage: newPackage.value }),
    })
    showChangePackageModal.value = false
    newPackage.value = ''
    await fetchService()
  } catch {
    saveError.value = 'Failed to change package.'
  } finally {
    commandLoading.value = false
  }
}

/**
 * Suspends the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleSuspend(): Promise<void> {
  showSuspendModal.value = false
  commandLoading.value = true
  try {
    await request(`/provisioning/${serviceId.value}/suspend`, { method: 'POST', body: JSON.stringify({ reason: 'Admin suspended' }) })
  } catch {
    saveError.value = 'Failed to suspend service.'
    return
  } finally {
    commandLoading.value = false
  }
  try { await fetchService() } catch { /* refresh may lag — ignore */ }
}

/**
 * Unsuspends the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleUnsuspend(): Promise<void> {
  commandLoading.value = true
  try {
    await request(`/provisioning/${serviceId.value}/unsuspend`, { method: 'POST' })
  } catch {
    saveError.value = 'Failed to unsuspend service.'
    return
  } finally {
    commandLoading.value = false
  }
  try { await fetchService() } catch { /* refresh may lag — ignore */ }
}

/**
 * Terminates the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
async function handleTerminate(): Promise<void> {
  showTerminateModal.value = false
  commandLoading.value = true
  try {
    await request(`/provisioning/${serviceId.value}/terminate`, { method: 'POST', body: JSON.stringify({ reason: 'Admin terminated' }) })
  } catch {
    saveError.value = 'Failed to terminate service.'
    return
  } finally {
    commandLoading.value = false
  }
  try { await fetchService() } catch { /* refresh may lag — ignore */ }
}

// Populate form when service data changes
watch(service, () => populateForm())

// Re-fetch service when navigating via the service switcher
watch(serviceId, () => {
  fetchService()
})

onMounted(() => {
  fetchService()
  fetchClientServices()
  fetchServers()
  fetchProducts()
})
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
            <AppSelect
              v-if="clientServices.length > 1"
              :model-value="Number(serviceId)"
              :options="serviceSwitcherOptions"
              searchable
              class="min-w-[200px]"
              @update:model-value="(val: number) => router.push(`/clients/${clientId}/services/${val}`)"
            />
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
              :disabled="ssoLoading || !service.provisioningRef"
              class="px-4 py-2 text-[0.84rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-[10px] hover:bg-primary-500/20 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-1.5"
              @click="handleLoginToPanel"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" /><polyline points="15 3 21 3 21 9" /><line x1="10" y1="14" x2="21" y2="3" /></svg>
              {{ ssoLoading ? 'Loading...' : 'Login to Control Panel' }}
            </button>
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
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Order #</label>
                    <span class="text-[0.82rem] text-text-secondary font-mono">{{ service.id }}</span>
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Product/Service</label>
                    <AppSelect
                      v-model="productIdRef"
                      :options="productOptions"
                      searchable
                    />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Server</label>
                    <AppSelect
                      :model-value="serverId ?? 0"
                      :options="serverOptions"
                      searchable
                      @update:model-value="(val: number) => serverId = val === 0 ? null : val"
                    />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Domain</label>
                    <input v-model="domain" type="text" placeholder="example.com"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Provisioning Ref</label>
                    <input v-model="provisioningRef" type="text" placeholder="cpanel-account-name"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Dedicated IP</label>
                    <input v-model="dedicatedIp" type="text" placeholder="192.168.1.1"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Username</label>
                    <input v-model="username" type="text" placeholder="cpanel_user"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Password</label>
                    <input v-model="password" type="password" placeholder="********"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
                  <AppSelect v-model="status" :options="statusOptions" />
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
              <div v-else class="flex items-center gap-2.5 flex-wrap">
                <!-- Pending: Create + Terminate -->
                <template v-if="service.status === 'Pending'">
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-green bg-status-green/10 border border-status-green/20 rounded-[10px] hover:bg-status-green/20 transition-colors disabled:opacity-50"
                    @click="handleProvision"
                  >
                    {{ commandLoading ? 'Processing...' : 'Create' }}
                  </button>
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-[10px] hover:bg-status-red/20 transition-colors disabled:opacity-50"
                    @click="showTerminateModal = true"
                  >
                    {{ commandLoading ? 'Processing...' : 'Terminate' }}
                  </button>
                </template>

                <!-- Active: Suspend + Terminate + Change Package + Change Password -->
                <template v-else-if="service.status === 'Active'">
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-yellow bg-status-yellow/10 border border-status-yellow/20 rounded-[10px] hover:bg-status-yellow/20 transition-colors disabled:opacity-50"
                    @click="showSuspendModal = true"
                  >
                    {{ commandLoading ? 'Processing...' : 'Suspend' }}
                  </button>
                  <button
                    type="button"
                    :disabled="commandLoading"
                    class="px-4 py-2 text-[0.84rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-[10px] hover:bg-status-red/20 transition-colors disabled:opacity-50"
                    @click="showTerminateModal = true"
                  >
                    {{ commandLoading ? 'Processing...' : 'Terminate' }}
                  </button>
                  <button
                    type="button"
                    :disabled="commandLoading || !service.provisioningRef"
                    class="px-4 py-2 text-[0.84rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-[10px] hover:bg-primary-500/20 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    @click="showChangePackageModal = true"
                  >
                    {{ commandLoading ? 'Processing...' : 'Change Package' }}
                  </button>
                  <button
                    type="button"
                    :disabled="commandLoading || !service.provisioningRef"
                    class="px-4 py-2 text-[0.84rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-[10px] hover:bg-primary-500/20 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    @click="showChangePasswordModal = true"
                  >
                    {{ commandLoading ? 'Processing...' : 'Change Password' }}
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
                    @click="showTerminateModal = true"
                  >
                    {{ commandLoading ? 'Processing...' : 'Terminate' }}
                  </button>
                </template>
              </div>
            </div>

            <!-- Addons -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Addons</h2>
              <div class="flex items-center gap-2 text-[0.82rem] text-text-muted">
                <span>No addons found.</span>
              </div>
            </div>

          </div>

          <!-- RIGHT COLUMN -->
          <div class="flex flex-col gap-5">

            <!-- Billing -->
            <div class="bg-surface-card border border-border rounded-2xl p-5">
              <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Billing</h2>

              <div class="flex flex-col gap-3">
                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Quantity</label>
                  <AppNumberInput v-model="quantity" :min="1" />
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Registration Date</label>
                    <AppDatePicker v-model="createdAt" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">First Payment Amount</label>
                    <AppNumberInput v-model="firstPaymentAmount" :step="0.01" :min="0" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Recurring Amount</label>
                    <AppNumberInput v-model="recurringAmount" :step="0.01" :min="0" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Next Due Date</label>
                    <AppDatePicker v-model="nextDueDate" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Termination Date</label>
                    <AppDatePicker v-model="terminatedAt" />
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
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                  <div>
                    <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Promotion Code</label>
                    <input v-model="promotionCode" type="text" placeholder="PROMO2026"
                      class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                  </div>
                </div>

                <div>
                  <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Subscription ID</label>
                  <input v-model="subscriptionId" type="text" placeholder="sub_1234567890"
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
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
                  <AppDatePicker v-model="suspendUntil" />
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
                    class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
                </div>
              </div>
            </div>

          </div>

        </div>

        <!-- Admin Notes (full width) -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 mt-5">
          <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Admin Notes</h2>
          <textarea v-model="adminNotes" placeholder="Internal notes visible only to admins..."
            class="w-full min-h-[8rem] bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none" />
        </div>

      </form>

      <!-- Change Password Modal -->
      <Teleport to="body">
        <div
          v-if="showChangePasswordModal"
          class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
          @click.self="showChangePasswordModal = false"
        >
          <div class="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-sm p-6 space-y-4">
            <div class="flex items-center justify-between">
              <h2 class="text-white font-semibold text-lg">Change Password</h2>
              <button class="text-zinc-400 hover:text-white transition" @click="showChangePasswordModal = false">&#x2715;</button>
            </div>
            <p class="text-zinc-400 text-sm">Enter a new password for the hosting account.</p>
            <div>
              <label class="block text-zinc-400 text-sm mb-1">New Password</label>
              <input
                v-model="newPassword"
                type="password"
                placeholder="Enter new password"
                class="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div class="flex justify-end gap-2">
              <button
                class="px-4 py-2 bg-zinc-700 hover:bg-zinc-600 text-white text-sm rounded-lg transition"
                @click="showChangePasswordModal = false"
              >Cancel</button>
              <button
                :disabled="commandLoading || !newPassword.trim()"
                class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-sm rounded-lg transition disabled:opacity-50"
                @click="handleChangePassword"
              >{{ commandLoading ? 'Processing...' : 'Confirm' }}</button>
            </div>
          </div>
        </div>
      </Teleport>

      <!-- Change Package Modal -->
      <Teleport to="body">
        <div
          v-if="showChangePackageModal"
          class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
          @click.self="showChangePackageModal = false"
        >
          <div class="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-sm p-6 space-y-4">
            <div class="flex items-center justify-between">
              <h2 class="text-white font-semibold text-lg">Change Package</h2>
              <button class="text-zinc-400 hover:text-white transition" @click="showChangePackageModal = false">&#x2715;</button>
            </div>
            <p class="text-zinc-400 text-sm">Enter the new hosting package name.</p>
            <div>
              <label class="block text-zinc-400 text-sm mb-1">Package Name</label>
              <input
                v-model="newPackage"
                type="text"
                placeholder="e.g. starter_plan"
                class="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div class="flex justify-end gap-2">
              <button
                class="px-4 py-2 bg-zinc-700 hover:bg-zinc-600 text-white text-sm rounded-lg transition"
                @click="showChangePackageModal = false"
              >Cancel</button>
              <button
                :disabled="commandLoading || !newPackage.trim()"
                class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-sm rounded-lg transition disabled:opacity-50"
                @click="handleChangePackage"
              >{{ commandLoading ? 'Processing...' : 'Confirm' }}</button>
            </div>
          </div>
        </div>
      </Teleport>

      <!-- Suspend Confirmation Modal -->
      <ConfirmModal
        v-if="showSuspendModal"
        title="Suspend Service"
        message="Are you sure you want to suspend this service? The client will lose access until the service is unsuspended."
        confirm-label="Suspend"
        loading-label="Processing..."
        :loading="commandLoading"
        variant="warning"
        @confirm="handleSuspend"
        @close="showSuspendModal = false"
      />

      <!-- Terminate Confirmation Modal -->
      <ConfirmModal
        v-if="showTerminateModal"
        title="Terminate Service"
        message="This action cannot be undone. The service will be permanently terminated."
        confirm-label="Terminate"
        loading-label="Processing..."
        :loading="commandLoading"
        variant="danger"
        @confirm="handleTerminate"
        @close="showTerminateModal = false"
      />

    </template>
  </div>
</template>
