<script setup lang="ts">
/**
 * Full-featured modal for creating or editing a provisioning server.
 *
 * Organized in three sections: General, Nameservers, Server Details.
 * Emits `saved` with the payload on submit, `close` on cancel.
 */
import { ref, watch, computed } from 'vue'
import type { ServerDto, ServerPayload, ServerModule } from '../types/server.types'
import { MODULE_LABELS } from '../types/server.types'
import AppSelect from '../../../components/AppSelect.vue'

/** Props for ServerFormModal. */
const props = defineProps<{
  /** Server to edit, or null when creating a new one. */
  server: ServerDto | null
  /** True while the save request is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the user submits a valid form. */
  saved: [payload: ServerPayload]
  /** Emitted when the user closes/cancels the modal. */
  close: []
}>()

const moduleOptions = (Object.keys(MODULE_LABELS) as ServerModule[]).map(k => ({ value: k, label: MODULE_LABELS[k] }))

// --- General ---
const name = ref('')
const hostname = ref('')
const ipAddress = ref('')
const assignedIpAddresses = ref('')
const monthlyCost = ref(0)
const datacenter = ref('')
const maxAccounts = ref<number | null>(null)
const serverStatusAddress = ref('')
const isDisabled = ref(false)
const isDefault = ref(false)

// --- Nameservers ---
const ns1Hostname = ref('')
const ns1Ip = ref('')
const ns2Hostname = ref('')
const ns2Ip = ref('')
const ns3Hostname = ref('')
const ns3Ip = ref('')
const ns4Hostname = ref('')
const ns4Ip = ref('')
const ns5Hostname = ref('')
const ns5Ip = ref('')

// --- Server Details ---
const module = ref<ServerModule>('Cwp7')
const username = ref('')
const password = ref('')
const apiToken = ref('')
const accessHash = ref('')
const useSSL = ref(true)

watch(() => props.server, (s) => {
  if (s) {
    name.value = s.name
    hostname.value = s.hostname
    ipAddress.value = s.ipAddress ?? ''
    assignedIpAddresses.value = s.assignedIpAddresses ?? ''
    monthlyCost.value = s.monthlyCost
    datacenter.value = s.datacenter ?? ''
    maxAccounts.value = s.maxAccounts
    serverStatusAddress.value = s.serverStatusAddress ?? ''
    isDisabled.value = s.isDisabled
    isDefault.value = s.isDefault
    ns1Hostname.value = s.ns1Hostname ?? ''
    ns1Ip.value = s.ns1Ip ?? ''
    ns2Hostname.value = s.ns2Hostname ?? ''
    ns2Ip.value = s.ns2Ip ?? ''
    ns3Hostname.value = s.ns3Hostname ?? ''
    ns3Ip.value = s.ns3Ip ?? ''
    ns4Hostname.value = s.ns4Hostname ?? ''
    ns4Ip.value = s.ns4Ip ?? ''
    ns5Hostname.value = s.ns5Hostname ?? ''
    ns5Ip.value = s.ns5Ip ?? ''
    module.value = s.module
    username.value = s.username
    password.value = ''
    apiToken.value = ''
    accessHash.value = ''
    useSSL.value = s.useSSL
  } else {
    name.value = ''
    hostname.value = ''
    ipAddress.value = ''
    assignedIpAddresses.value = ''
    monthlyCost.value = 0
    datacenter.value = ''
    maxAccounts.value = null
    serverStatusAddress.value = ''
    isDisabled.value = false
    isDefault.value = false
    ns1Hostname.value = ''
    ns1Ip.value = ''
    ns2Hostname.value = ''
    ns2Ip.value = ''
    ns3Hostname.value = ''
    ns3Ip.value = ''
    ns4Hostname.value = ''
    ns4Ip.value = ''
    ns5Hostname.value = ''
    ns5Ip.value = ''
    module.value = 'Cwp7'
    username.value = ''
    password.value = ''
    apiToken.value = ''
    accessHash.value = ''
    useSSL.value = true
  }
}, { immediate: true })

/** Builds and emits the save payload after validation passes. */
function handleSubmit(): void {
  if (!validate()) return
  emit('saved', {
    name: name.value,
    hostname: hostname.value,
    ipAddress: ipAddress.value || null,
    assignedIpAddresses: assignedIpAddresses.value || null,
    module: module.value,
    username: username.value,
    password: password.value || null,
    apiToken: apiToken.value || null,
    accessHash: accessHash.value || null,
    useSSL: useSSL.value,
    maxAccounts: maxAccounts.value,
    isDefault: isDefault.value,
    isDisabled: isDisabled.value,
    monthlyCost: monthlyCost.value,
    datacenter: datacenter.value || null,
    serverStatusAddress: serverStatusAddress.value || null,
    ns1Hostname: ns1Hostname.value || null,
    ns1Ip: ns1Ip.value || null,
    ns2Hostname: ns2Hostname.value || null,
    ns2Ip: ns2Ip.value || null,
    ns3Hostname: ns3Hostname.value || null,
    ns3Ip: ns3Ip.value || null,
    ns4Hostname: ns4Hostname.value || null,
    ns4Ip: ns4Ip.value || null,
    ns5Hostname: ns5Hostname.value || null,
    ns5Ip: ns5Ip.value || null,
  })
}

/** Active tab index. */
const tab = ref<'general' | 'nameservers' | 'details'>('general')

/** Field-level validation errors, cleared on each submit attempt. */
const errors = ref({ name: '', hostname: '', username: '' })

/** True when any validation error is present. */
const hasErrors = computed(() => Object.values(errors.value).some(Boolean))

/**
 * Validates required fields and sets error messages.
 * Automatically switches to the tab containing the first error.
 *
 * @returns True when all required fields are filled.
 */
function validate(): boolean {
  errors.value = { name: '', hostname: '', username: '' }
  if (!name.value.trim()) errors.value.name = 'Name is required'
  if (!hostname.value.trim()) errors.value.hostname = 'Hostname is required'
  if (!username.value.trim()) errors.value.username = 'Username is required'
  if (errors.value.name || errors.value.hostname) { tab.value = 'general'; return false }
  if (errors.value.username) { tab.value = 'details'; return false }
  return true
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')" />

    <div class="relative bg-surface-card border border-border rounded-2xl w-full max-w-2xl max-h-[92vh] flex flex-col shadow-2xl">

      <!-- Header -->
      <div class="flex items-center justify-between px-4 sm:px-6 py-4 border-b border-border shrink-0">
        <h2 class="font-display font-bold text-[1rem] text-text-primary">
          {{ props.server ? 'Edit Server' : 'Add New Server' }}
        </h2>
        <button
          class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
          @click="emit('close')"
        >
          <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
          </svg>
        </button>
      </div>

      <!-- Tabs -->
      <div class="flex items-center gap-0.5 px-3 sm:px-6 pt-3 shrink-0 border-b border-border pb-0 overflow-x-auto scrollbar-none">
        <button
          v-for="t in ([['general','General'],['nameservers','Nameservers'],['details','Details']] as const)"
          :key="t[0]"
          class="shrink-0 px-2.5 sm:px-3 py-2 text-[0.75rem] sm:text-[0.8rem] font-medium border-b-2 whitespace-nowrap transition-colors"
          :class="tab === t[0]
            ? 'border-primary-500 text-primary-400'
            : 'border-transparent text-text-muted hover:text-text-secondary'"
          @click="tab = t[0]"
        >
          {{ t[1] }}
        </button>
      </div>

      <!-- Scrollable form body -->
      <form class="overflow-y-auto flex-1 px-4 sm:px-6 py-5" @submit.prevent="handleSubmit">

        <!-- ── General tab ── -->
        <div v-show="tab === 'general'" class="flex flex-col gap-4">

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Name <span class="text-status-red">*</span></label>
              <input v-model="name" type="text" placeholder="My Production Server"
                :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                  errors.name ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                @input="errors.name = ''" />
              <p v-if="errors.name" class="text-[0.68rem] text-status-red mt-1">{{ errors.name }}</p>
              <p v-else class="text-[0.68rem] text-text-muted mt-1">A friendly label shown in the panel.</p>
            </div>
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Hostname <span class="text-status-red">*</span></label>
              <input v-model="hostname" type="text" placeholder="host3.innovayse.com"
                :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                  errors.hostname ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
                @input="errors.hostname = ''" />
              <p v-if="errors.hostname" class="text-[0.68rem] text-status-red mt-1">{{ errors.hostname }}</p>
              <p v-else class="text-[0.68rem] text-text-muted mt-1">Used for API connections to this server.</p>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">IP Address</label>
              <input v-model="ipAddress" type="text" placeholder="207.180.221.80" class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
            </div>
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Monthly Cost (USD)</label>
              <input v-model.number="monthlyCost" type="number" min="0" step="0.01" placeholder="0.00" class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
            </div>
          </div>

          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Assigned IP Addresses <span class="text-text-muted normal-case font-normal">(one per line)</span></label>
            <textarea
              v-model="assignedIpAddresses"
              rows="3"
              placeholder="203.0.113.1&#10;203.0.113.2"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none"
            />
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Datacenter / NOC</label>
              <input v-model="datacenter" type="text" placeholder="Hetzner, OVH…" class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
            </div>
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Max Accounts</label>
              <input v-model.number="maxAccounts" type="number" min="1" placeholder="Unlimited" class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
            </div>
          </div>

          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Server Status Address</label>
            <input v-model="serverStatusAddress" type="text" placeholder="https://host.example.com/status/" class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
            <p class="text-[0.68rem] text-text-muted mt-1">Full path to server status folder for the status page monitor.</p>
          </div>

          <!-- Toggles row -->
          <div class="flex flex-wrap gap-5">
            <label class="flex items-center gap-2.5 cursor-pointer">
              <div
                class="w-9 h-5 rounded-full transition-colors duration-200 flex items-center px-0.5 shrink-0"
                :class="isDefault ? 'bg-primary-500' : 'bg-border'"
                @click="isDefault = !isDefault"
              >
                <div class="w-4 h-4 rounded-full bg-white shadow transition-transform duration-200" :class="isDefault ? 'translate-x-4' : 'translate-x-0'" />
              </div>
              <span class="text-[0.82rem] text-text-secondary">Default server</span>
            </label>
            <label class="flex items-center gap-2.5 cursor-pointer">
              <div
                class="w-9 h-5 rounded-full transition-colors duration-200 flex items-center px-0.5 shrink-0"
                :class="isDisabled ? 'bg-status-red' : 'bg-border'"
                @click="isDisabled = !isDisabled"
              >
                <div class="w-4 h-4 rounded-full bg-white shadow transition-transform duration-200" :class="isDisabled ? 'translate-x-4' : 'translate-x-0'" />
              </div>
              <span class="text-[0.82rem] text-text-secondary">Disable this server</span>
            </label>
          </div>

        </div>

        <!-- ── Nameservers tab ── -->
        <div v-show="tab === 'nameservers'" class="flex flex-col gap-3">
          <p class="text-[0.75rem] text-text-muted">Enter nameservers that will be shown when provisioning new accounts on this server.</p>

          <template v-for="(ns, i) in [
            { label: 'Primary', h: ns1Hostname, hKey: 'ns1Hostname', ip: ns1Ip, ipKey: 'ns1Ip' },
            { label: 'Secondary', h: ns2Hostname, hKey: 'ns2Hostname', ip: ns2Ip, ipKey: 'ns2Ip' },
            { label: 'Third', h: ns3Hostname, hKey: 'ns3Hostname', ip: ns3Ip, ipKey: 'ns3Ip' },
            { label: 'Fourth', h: ns4Hostname, hKey: 'ns4Hostname', ip: ns4Ip, ipKey: 'ns4Ip' },
            { label: 'Fifth', h: ns5Hostname, hKey: 'ns5Hostname', ip: ns5Ip, ipKey: 'ns5Ip' },
          ]" :key="i">
            <div class="grid grid-cols-1 sm:grid-cols-[1fr_auto_140px] gap-2 items-end">
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">{{ ns.label }} Nameserver</label>
                <input
                  :value="ns.h.value"
                  type="text"
                  :placeholder="`ns${i+1}.innovayse.com`"
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                  @input="(e) => (ns.h.value = (e.target as HTMLInputElement).value)"
                />
              </div>
              <span class="text-[0.72rem] text-text-muted pb-2.5">IP</span>
              <div>
                <label class="field-label sr-only">IP Address</label>
                <input
                  :value="ns.ip.value"
                  type="text"
                  placeholder="0.0.0.0"
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                  @input="(e) => (ns.ip.value = (e.target as HTMLInputElement).value)"
                />
              </div>
            </div>
          </template>
        </div>

        <!-- ── Server Details tab ── -->
        <div v-show="tab === 'details'" class="flex flex-col gap-4">

          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Module</label>
            <AppSelect v-model="module" :options="moduleOptions" />
          </div>

          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Username <span class="text-status-red">*</span></label>
            <input v-model="username" type="text" placeholder="root"
              :class="['w-full bg-white/[0.04] border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:ring-1 transition-colors',
                errors.username ? 'border-status-red focus:border-status-red focus:ring-status-red/10' : 'border-border focus:border-primary-500/50 focus:ring-primary-500/10']"
              @input="errors.username = ''" />
            <p v-if="errors.username" class="text-[0.68rem] text-status-red mt-1">{{ errors.username }}</p>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
                Password {{ props.server ? '(blank = keep)' : '' }}
              </label>
              <input v-model="password" type="password" placeholder="••••••••" class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
            </div>
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
                API Token {{ props.server ? '(blank = keep)' : '' }}
              </label>
              <input v-model="apiToken" type="password" placeholder="••••••••" class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors" />
            </div>
          </div>

          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Access Hash {{ props.server ? '(blank = keep)' : '' }}
            </label>
            <textarea
              v-model="accessHash"
              rows="4"
              placeholder="tHcFDLX8pGkv…"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.78rem] text-text-primary placeholder-text-muted font-mono focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none"
            />
            <p class="text-[0.68rem] text-text-muted mt-1">Used by cPanel/WHM and CWP for token-based authentication.</p>
          </div>

          <label class="flex items-center gap-2.5 cursor-pointer">
            <div
              class="w-9 h-5 rounded-full transition-colors duration-200 flex items-center px-0.5 shrink-0"
              :class="useSSL ? 'bg-primary-500' : 'bg-border'"
              @click="useSSL = !useSSL"
            >
              <div class="w-4 h-4 rounded-full bg-white shadow transition-transform duration-200" :class="useSSL ? 'translate-x-4' : 'translate-x-0'" />
            </div>
            <span class="text-[0.82rem] text-text-secondary">Use SSL for connections</span>
          </label>

        </div>

      </form>

      <!-- Footer actions — outside scrollable area -->
      <div class="flex items-center justify-end gap-2.5 px-4 sm:px-6 py-4 border-t border-border shrink-0">
        <button
          type="button"
          class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
          @click="emit('close')"
        >
          Cancel
        </button>
        <button
          type="button"
          :disabled="props.saving"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="handleSubmit"
        >
          {{ props.saving ? 'Saving…' : props.server ? 'Save Changes' : 'Add Server' }}
        </button>
      </div>

    </div>
  </div>
</template>

