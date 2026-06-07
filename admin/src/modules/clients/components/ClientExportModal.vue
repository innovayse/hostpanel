<script setup lang="ts">
import { ref, computed } from 'vue'
import { useApi } from '../../../composables/useApi'

const props = defineProps<{ clientId: number; clientName: string }>()
const emit = defineEmits<{ close: [] }>()

const { request } = useApi()
const downloading = ref(false)

const FIELDS = [
  { key: 'profileData',     label: 'Profile Data' },
  { key: 'payMethods',      label: 'Pay Methods' },
  { key: 'contacts',        label: 'Contacts' },
  { key: 'productsServices',label: 'Products/Services' },
  { key: 'domains',         label: 'Domains' },
  { key: 'billableItems',   label: 'Billable Items' },
  { key: 'invoices',        label: 'Invoices' },
  { key: 'quotes',          label: 'Quotes' },
  { key: 'transactions',    label: 'Transactions' },
  { key: 'tickets',         label: 'Tickets' },
  { key: 'emails',          label: 'Emails' },
  { key: 'notes',           label: 'Notes' },
  { key: 'consentHistory',  label: 'Consent History' },
  { key: 'activityLog',     label: 'Activity Log' },
]

const selected = ref<Record<string, boolean>>(
  Object.fromEntries(FIELDS.map(f => [f.key, true]))
)

const allSelected = computed(() => FIELDS.every(f => selected.value[f.key]))

function toggleAll() {
  const val = !allSelected.value
  FIELDS.forEach(f => { selected.value[f.key] = val })
}

async function download() {
  downloading.value = true
  try {
    const fields = FIELDS.filter(f => selected.value[f.key]).map(f => f.key)
    const params = new URLSearchParams()
    fields.forEach(f => params.append('fields', f))
    const data = await request<object>(`/clients/${props.clientId}/export?${params}`)
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `client-${props.clientId}-export.json`
    a.click()
    URL.revokeObjectURL(url)
    emit('close')
  } catch {
    // ignore
  } finally {
    downloading.value = false
  }
}
</script>

<template>
  <Teleport to="body">
    <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
      <!-- Backdrop -->
      <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')" />

      <!-- Modal -->
      <div class="relative bg-surface-card border border-border rounded-2xl shadow-2xl w-full max-w-md">
        <!-- Header -->
        <div class="flex items-center justify-between px-5 py-4 border-b border-border">
          <div>
            <h2 class="text-[0.95rem] font-bold text-text-primary">Export Data</h2>
            <p class="text-[0.75rem] text-text-muted mt-0.5">{{ clientName }}</p>
          </div>
          <button @click="emit('close')" class="w-8 h-8 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors">
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </button>
        </div>

        <!-- Body -->
        <div class="px-5 py-4">
          <p class="text-[0.78rem] text-text-muted mb-4">Choose which data points to include in the export.</p>

          <!-- Select All -->
          <label class="flex items-center gap-2.5 mb-3 cursor-pointer select-none">
            <button
              type="button"
              role="checkbox"
              :aria-checked="allSelected"
              class="w-4 h-4 rounded-[4px] border flex items-center justify-center shrink-0 transition-colors focus:outline-none"
              :class="allSelected ? 'bg-primary-500 border-primary-500' : 'bg-white/[0.04] border-border hover:border-text-muted'"
              @click="toggleAll"
            >
              <svg v-if="allSelected" class="w-2.5 h-2.5 text-white" viewBox="0 0 12 10" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="1 5.5 4 8.5 11 1.5" />
              </svg>
            </button>
            <span class="text-[0.82rem] font-semibold text-text-primary">Select All</span>
          </label>

          <div class="border-t border-border pt-3 grid grid-cols-2 gap-y-2.5 gap-x-4">
            <label v-for="field in FIELDS" :key="field.key" class="flex items-center gap-2.5 cursor-pointer select-none">
              <button
                type="button"
                role="checkbox"
                :aria-checked="selected[field.key]"
                class="w-4 h-4 rounded-[4px] border flex items-center justify-center shrink-0 transition-colors focus:outline-none"
                :class="selected[field.key] ? 'bg-primary-500 border-primary-500' : 'bg-white/[0.04] border-border hover:border-text-muted'"
                @click="selected[field.key] = !selected[field.key]"
              >
                <svg v-if="selected[field.key]" class="w-2.5 h-2.5 text-white" viewBox="0 0 12 10" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <polyline points="1 5.5 4 8.5 11 1.5" />
                </svg>
              </button>
              <span class="text-[0.82rem] text-text-secondary">{{ field.label }}</span>
            </label>
          </div>
        </div>

        <!-- Footer -->
        <div class="flex items-center justify-between px-5 py-4 border-t border-border">
          <p class="text-[0.72rem] text-text-muted italic">Generating an export for a client with a substantial amount of history may take a while.</p>
          <button
            @click="download"
            :disabled="downloading || !FIELDS.some(f => selected[f.key])"
            class="flex items-center gap-1.5 px-4 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed shrink-0 ml-3"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
              <polyline points="7 10 12 15 17 10"/>
              <line x1="12" y1="15" x2="12" y2="3"/>
            </svg>
            {{ downloading ? 'Generating…' : 'Download' }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
