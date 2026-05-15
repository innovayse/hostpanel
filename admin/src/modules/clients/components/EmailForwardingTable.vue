<script setup lang="ts">
/**
 * Inline CRUD table for managing email forwarding rules on a domain.
 * Supports adding, editing, and deleting individual forwarding rules.
 */
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import ToggleSwitch from '../../../components/ToggleSwitch.vue'
import type { EmailForwardingRuleItem } from '../../../types/models'

const props = defineProps<{
  /** Domain ID to manage email forwarding for. */
  domainId: number
  /** Current list of email forwarding rules. */
  rules: EmailForwardingRuleItem[]
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

/** ID of the rule currently being edited inline, or null. */
const editingId = ref<number | null>(null)

// --- Add form fields ---

/** New rule source alias. */
const addSource = ref('')

/** New rule destination email. */
const addDestination = ref('')

// --- Edit form fields ---

/** Edit rule source alias. */
const editSource = ref('')

/** Edit rule destination email. */
const editDestination = ref('')

/**
 * Opens the inline add row with default values.
 */
function startAdd(): void {
  editingId.value = null
  addSource.value = ''
  addDestination.value = ''
  showAddRow.value = true
}

/**
 * Cancels the inline add row.
 */
function cancelAdd(): void {
  showAddRow.value = false
}

/**
 * Saves the new email forwarding rule via the API and emits refresh.
 *
 * @returns Promise that resolves when the rule is created.
 */
async function saveAdd(): Promise<void> {
  if (!addSource.value.trim() || !addDestination.value.trim()) return
  saving.value = true
  try {
    await request(`/domains/${props.domainId}/email-forwarding`, {
      method: 'POST',
      body: JSON.stringify({
        source: addSource.value.trim(),
        destination: addDestination.value.trim(),
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
 * Opens inline edit for a rule, populating fields.
 *
 * @param rule - The email forwarding rule to edit.
 */
function startEdit(rule: EmailForwardingRuleItem): void {
  showAddRow.value = false
  editingId.value = rule.id
  editSource.value = rule.source
  editDestination.value = rule.destination
}

/**
 * Cancels inline editing.
 */
function cancelEdit(): void {
  editingId.value = null
}

/**
 * Saves the edited email forwarding rule via the API and emits refresh.
 *
 * @returns Promise that resolves when the rule is updated.
 */
async function saveEdit(): Promise<void> {
  if (editingId.value === null || !editSource.value.trim() || !editDestination.value.trim()) return
  saving.value = true
  try {
    await request(`/domains/${props.domainId}/email-forwarding/${editingId.value}`, {
      method: 'PUT',
      body: JSON.stringify({
        source: editSource.value.trim(),
        destination: editDestination.value.trim(),
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
 * Toggles the active state of a rule via the API.
 *
 * @param rule - The rule to toggle.
 * @returns Promise that resolves when the toggle completes.
 */
async function toggleActive(rule: EmailForwardingRuleItem): Promise<void> {
  saving.value = true
  try {
    await request(`/domains/${props.domainId}/email-forwarding/${rule.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        source: rule.source,
        destination: rule.destination,
        isActive: !rule.isActive,
      }),
    })
    emit('refresh')
  } catch {
    // Error is handled silently; the user can retry
  } finally {
    saving.value = false
  }
}

/**
 * Deletes an email forwarding rule after confirmation.
 *
 * @param ruleId - The ID of the rule to delete.
 * @returns Promise that resolves when the rule is deleted.
 */
async function handleDelete(ruleId: number): Promise<void> {
  if (!confirm('Delete this email forwarding rule?')) return
  saving.value = true
  try {
    await request(`/domains/${props.domainId}/email-forwarding/${ruleId}`, { method: 'DELETE' })
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
      <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Email Forwarding Rules</h2>
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
      <div class="hidden sm:grid grid-cols-[1.2fr_1.5fr_0.6fr_0.5fr] gap-3 px-3 py-2 border-b border-border bg-white/[0.02] rounded-t-xl">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Source</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Destination</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Active</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
      </div>

      <!-- Inline add row -->
      <div v-if="showAddRow" class="grid grid-cols-1 sm:grid-cols-[1.2fr_1.5fr_0.6fr_0.5fr] gap-2 sm:gap-3 px-3 py-2.5 border-b border-border bg-primary-500/[0.03]">
        <div>
          <input
            v-model="addSource"
            type="text"
            placeholder="info@"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>
        <div>
          <input
            v-model="addDestination"
            type="text"
            placeholder="user@example.com"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>
        <div class="flex items-center">
          <span class="text-[0.82rem] text-text-muted">&mdash;</span>
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
      <template v-for="rule in rules" :key="rule.id">

        <!-- Read-only row -->
        <div
          v-if="editingId !== rule.id"
          class="grid grid-cols-1 sm:grid-cols-[1.2fr_1.5fr_0.6fr_0.5fr] gap-2 sm:gap-3 px-3 py-2.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.82rem] text-text-primary truncate">{{ rule.source }}</span>
          <span class="text-[0.82rem] text-text-secondary truncate">{{ rule.destination }}</span>
          <div class="flex items-center">
            <ToggleSwitch :model-value="rule.isActive" @update:model-value="toggleActive(rule)" />
          </div>
          <div class="flex items-center gap-1.5">
            <button
              type="button"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
              @click="startEdit(rule)"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" /><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
            </button>
            <button
              type="button"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-status-red hover:bg-status-red/10 transition-colors"
              @click="handleDelete(rule.id)"
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
          class="grid grid-cols-1 sm:grid-cols-[1.2fr_1.5fr_0.6fr_0.5fr] gap-2 sm:gap-3 px-3 py-2.5 border-b border-border bg-primary-500/[0.03]"
        >
          <div>
            <input
              v-model="editSource"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div>
            <input
              v-model="editDestination"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div class="flex items-center">
            <span class="text-[0.82rem] text-text-muted">&mdash;</span>
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
      <div v-if="rules.length === 0 && !showAddRow" class="py-8 text-center">
        <p class="text-[0.82rem] text-text-muted">No email forwarding rules found</p>
      </div>
    </div>
  </div>
</template>
