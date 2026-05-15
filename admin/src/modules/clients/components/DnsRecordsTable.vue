<script setup lang="ts">
/**
 * Inline CRUD table for managing DNS records on a domain.
 * Supports adding, editing, and deleting individual DNS records.
 */
import { ref, computed } from 'vue'
import { useApi } from '../../../composables/useApi'
import { DNS_RECORD_TYPE_OPTIONS } from '../../../utils/constants'
import AppSelect from '../../../components/AppSelect.vue'
import type { DnsRecordItem } from '../../../types/models'

const props = defineProps<{
  /** Domain ID to manage DNS records for. */
  domainId: number
  /** Current list of DNS records. */
  records: DnsRecordItem[]
}>()

const emit = defineEmits<{
  /** Emitted after a successful add, edit, or delete to reload domain data. */
  refresh: []
}>()

const { request } = useApi()

/** True while an API call is in flight. */
const saving = ref(false)

/** Whether the inline add row is visible. */
const showAddRow = ref(false)

/** ID of the record currently being edited inline, or null. */
const editingId = ref<number | null>(null)

// --- Add form fields ---

/** New record type. */
const addType = ref('A')

/** New record host. */
const addHost = ref('')

/** New record value. */
const addValue = ref('')

/** New record TTL. */
const addTtl = ref('3600')

/** New record priority (MX/SRV only). */
const addPriority = ref('')

/** Whether the add form type requires a priority field. */
const addNeedsPriority = computed(() => addType.value === 'MX' || addType.value === 'SRV')

// --- Edit form fields ---

/** Edit record type. */
const editType = ref('A')

/** Edit record host. */
const editHost = ref('')

/** Edit record value. */
const editValue = ref('')

/** Edit record TTL. */
const editTtl = ref('3600')

/** Edit record priority. */
const editPriority = ref('')

/** Whether the edit form type requires a priority field. */
const editNeedsPriority = computed(() => editType.value === 'MX' || editType.value === 'SRV')

/**
 * Opens the inline add row with default values.
 */
function startAdd(): void {
  editingId.value = null
  addType.value = 'A'
  addHost.value = ''
  addValue.value = ''
  addTtl.value = '3600'
  addPriority.value = ''
  showAddRow.value = true
}

/**
 * Cancels the inline add row.
 */
function cancelAdd(): void {
  showAddRow.value = false
}

/**
 * Saves the new DNS record via the API and emits refresh.
 *
 * @returns Promise that resolves when the record is created.
 */
async function saveAdd(): Promise<void> {
  if (!addHost.value.trim() || !addValue.value.trim()) return
  saving.value = true
  try {
    await request(`/domains/${props.domainId}/dns`, {
      method: 'POST',
      body: JSON.stringify({
        type: addType.value,
        host: addHost.value.trim(),
        value: addValue.value.trim(),
        ttl: parseInt(addTtl.value) || 3600,
        priority: addNeedsPriority.value && addPriority.value ? parseInt(addPriority.value) : null,
      }),
    })
    showAddRow.value = false
    emit('refresh')
  } catch {
    // Error is handled silently; the user can retry
  } finally {
    saving.value = false
  }
}

/**
 * Opens the inline edit for a record, populating fields.
 *
 * @param record - The DNS record to edit.
 */
function startEdit(record: DnsRecordItem): void {
  showAddRow.value = false
  editingId.value = record.id
  editType.value = record.type
  editHost.value = record.host
  editValue.value = record.value
  editTtl.value = String(record.ttl)
  editPriority.value = record.priority != null ? String(record.priority) : ''
}

/**
 * Cancels inline editing.
 */
function cancelEdit(): void {
  editingId.value = null
}

/**
 * Saves the edited DNS record via the API and emits refresh.
 *
 * @returns Promise that resolves when the record is updated.
 */
async function saveEdit(): Promise<void> {
  if (editingId.value === null || !editHost.value.trim() || !editValue.value.trim()) return
  saving.value = true
  try {
    await request(`/domains/${props.domainId}/dns/${editingId.value}`, {
      method: 'PUT',
      body: JSON.stringify({
        type: editType.value,
        host: editHost.value.trim(),
        value: editValue.value.trim(),
        ttl: parseInt(editTtl.value) || 3600,
        priority: editNeedsPriority.value && editPriority.value ? parseInt(editPriority.value) : null,
      }),
    })
    editingId.value = null
    emit('refresh')
  } catch {
    // Error is handled silently; the user can retry
  } finally {
    saving.value = false
  }
}

/**
 * Deletes a DNS record after confirmation.
 *
 * @param recordId - The ID of the record to delete.
 * @returns Promise that resolves when the record is deleted.
 */
async function handleDelete(recordId: number): Promise<void> {
  if (!confirm('Delete this DNS record?')) return
  saving.value = true
  try {
    await request(`/domains/${props.domainId}/dns/${recordId}`, { method: 'DELETE' })
    emit('refresh')
  } catch {
    // Error is handled silently; the user can retry
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl p-5">

    <!-- Section header -->
    <div class="flex items-center justify-between mb-4">
      <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted">DNS Records</h2>
      <button
        type="button"
        class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
        @click="startAdd"
      >
        <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
        </svg>
      </button>
    </div>

    <!-- Table -->
    <div class="overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[0.6fr_1.2fr_1.5fr_0.6fr_0.6fr_0.5fr] gap-3 px-3 py-2 border-b border-border bg-white/[0.02] rounded-t-xl">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Type</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Host</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Value</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">TTL</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Priority</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
      </div>

      <!-- Inline add row -->
      <div v-if="showAddRow" class="grid grid-cols-1 sm:grid-cols-[0.6fr_1.2fr_1.5fr_0.6fr_0.6fr_0.5fr] gap-2 sm:gap-3 px-3 py-2.5 border-b border-border bg-primary-500/[0.03]">
        <div>
          <AppSelect v-model="addType" :options="DNS_RECORD_TYPE_OPTIONS" />
        </div>
        <div>
          <input
            v-model="addHost"
            type="text"
            placeholder="@"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>
        <div>
          <input
            v-model="addValue"
            type="text"
            placeholder="Value"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>
        <div>
          <input
            v-model="addTtl"
            type="text"
            placeholder="3600"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>
        <div>
          <input
            v-if="addNeedsPriority"
            v-model="addPriority"
            type="number"
            placeholder="10"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
          <span v-else class="text-[0.82rem] text-text-muted">&mdash;</span>
        </div>
        <div class="flex items-center gap-1.5">
          <button
            type="button"
            :disabled="saving"
            class="w-7 h-7 flex items-center justify-center rounded-lg text-status-green hover:bg-status-green/10 transition-colors disabled:opacity-50"
            @click="saveAdd"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </button>
          <button
            type="button"
            class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
            @click="cancelAdd"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Data rows -->
      <template v-for="record in records" :key="record.id">

        <!-- Read-only row -->
        <div
          v-if="editingId !== record.id"
          class="grid grid-cols-1 sm:grid-cols-[0.6fr_1.2fr_1.5fr_0.6fr_0.6fr_0.5fr] gap-2 sm:gap-3 px-3 py-2.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.82rem] text-text-secondary font-mono">{{ record.type }}</span>
          <span class="text-[0.82rem] text-text-primary truncate">{{ record.host }}</span>
          <span class="text-[0.82rem] text-text-secondary truncate">{{ record.value }}</span>
          <span class="text-[0.82rem] text-text-muted">{{ record.ttl }}</span>
          <span class="text-[0.82rem] text-text-muted">{{ record.priority ?? '\u2014' }}</span>
          <div class="flex items-center gap-1.5">
            <button
              type="button"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
              @click="startEdit(record)"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" /><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
            </button>
            <button
              type="button"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-status-red hover:bg-status-red/10 transition-colors"
              @click="handleDelete(record.id)"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
              </svg>
            </button>
          </div>
        </div>

        <!-- Inline edit row -->
        <div
          v-else
          class="grid grid-cols-1 sm:grid-cols-[0.6fr_1.2fr_1.5fr_0.6fr_0.6fr_0.5fr] gap-2 sm:gap-3 px-3 py-2.5 border-b border-border bg-primary-500/[0.03]"
        >
          <div>
            <AppSelect v-model="editType" :options="DNS_RECORD_TYPE_OPTIONS" />
          </div>
          <div>
            <input
              v-model="editHost"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div>
            <input
              v-model="editValue"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div>
            <input
              v-model="editTtl"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div>
            <input
              v-if="editNeedsPriority"
              v-model="editPriority"
              type="number"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
            <span v-else class="text-[0.82rem] text-text-muted">&mdash;</span>
          </div>
          <div class="flex items-center gap-1.5">
            <button
              type="button"
              :disabled="saving"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-status-green hover:bg-status-green/10 transition-colors disabled:opacity-50"
              @click="saveEdit"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="20 6 9 17 4 12" />
              </svg>
            </button>
            <button
              type="button"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
              @click="cancelEdit"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>
        </div>
      </template>

      <!-- Empty state -->
      <div v-if="records.length === 0 && !showAddRow" class="py-8 text-center">
        <p class="text-[0.82rem] text-text-muted">No DNS records found</p>
      </div>
    </div>
  </div>
</template>
