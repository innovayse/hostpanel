<script setup lang="ts">
/**
 * Manage User modal — edit profile fields, view linked accounts,
 * manage permissions, transfer ownership, and delete user.
 */
import { ref, onMounted } from 'vue'
import AppSelect from '../../../components/AppSelect.vue'
import type { UserDetail } from '../stores/usersStore'
import { PERMISSION_LABELS, ClientPermission } from '../../../types/models'
import { LANGUAGE_OPTIONS } from '../../../utils/constants'

const props = defineProps<{
  /** The user to edit. */
  user: UserDetail
  /** Whether a save is in flight. */
  saving?: boolean
  /** Whether this user is the account owner. */
  isOwner?: boolean
  /** Current permissions value (bit flags). Only used for non-owner users. */
  permissions?: number
  /** Client ID for permission updates. */
  clientId?: number
}>()

const emit = defineEmits<{
  /** Emitted with updated fields when saved. */
  save: [data: { firstName: string; lastName: string; email: string; language: string | null }]
  /** Emitted when permissions are updated (non-owner only). */
  updatePermissions: [permissions: number]
  /** Emitted when ownership transfer is requested. */
  makeOwner: []
  /** Emitted when delete is confirmed. */
  delete: []
  /** Emitted when the modal is closed. */
  close: []
}>()

/** Form state: first name. */
const firstName = ref('')

/** Form state: last name. */
const lastName = ref('')

/** Form state: email. */
const email = ref('')

/** Form state: language. */
const language = ref('')

/** Permissions value for the current user. */
const permissionValues = ref<number>(props.permissions ?? ClientPermission.All)

/** Whether the "transfer ownership" checkbox is ticked. */
const makeOwnerChecked = ref(false)

/** Language options for AppSelect. */
const languageOptions = LANGUAGE_OPTIONS

onMounted(() => {
  firstName.value = props.user.firstName
  lastName.value = props.user.lastName
  email.value = props.user.email
  language.value = props.user.language ?? ''
})

/**
 * Checks whether a permission flag is set.
 *
 * @param flag - The permission bit flag to check.
 * @returns True if the flag is set.
 */
function hasPermission(flag: number): boolean {
  return (permissionValues.value & flag) !== 0
}

/**
 * Toggles a permission flag on or off.
 *
 * @param flag - The permission bit flag to toggle.
 */
function togglePermission(flag: number): void {
  permissionValues.value ^= flag
}

/**
 * Emits save with the current form values, plus permissions and ownership if applicable.
 */
function handleSave(): void {
  emit('save', {
    firstName: firstName.value,
    lastName: lastName.value,
    email: email.value,
    language: language.value || null,
  })
  if (props.clientId !== undefined && !props.isOwner) {
    emit('updatePermissions', permissionValues.value)
  }
  if (makeOwnerChecked.value) {
    emit('makeOwner')
  }
}

/**
 * Confirms and emits delete.
 */
function handleDelete(): void {
  if (confirm(`Permanently delete user "${email.value}"? This cannot be undone.`)) {
    emit('delete')
  }
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center">
    <!-- Backdrop -->
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')" />

    <!-- Modal -->
    <div class="relative bg-surface-card border border-border rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">

      <!-- Header -->
      <div class="px-6 pt-5 pb-4 border-b border-border">
        <h2 class="font-display font-bold text-[1.1rem] text-text-primary">
          Manage User: {{ user.email }}
        </h2>
      </div>

      <!-- Body -->
      <div class="px-6 py-5 flex flex-col gap-4">

        <!-- First Name -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-1.5">First Name</label>
          <input
            v-model="firstName"
            type="text"
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
          />
        </div>

        <!-- Last Name -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-1.5">Last Name</label>
          <input
            v-model="lastName"
            type="text"
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
          />
        </div>

        <!-- Email -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-1.5">Email Address</label>
          <input
            v-model="email"
            type="email"
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
          />
        </div>

        <!-- Language -->
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-1.5">Language</label>
          <AppSelect
            v-model="language"
            :options="languageOptions"
            placeholder="Default"
          />
        </div>

        <!-- Permissions section (only when clientId is provided) -->
        <div v-if="props.clientId !== undefined" class="mt-2">
          <h3 class="text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-3">Permissions</h3>

          <!-- Owner banner -->
          <div v-if="props.isOwner" class="mb-3 px-3 py-2.5 text-[0.82rem] text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-[10px]">
            This user is the account owner and therefore has all permissions.
          </div>

          <!-- Permission checkboxes (2-column grid) -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <label v-for="perm in PERMISSION_LABELS" :key="perm.flag" class="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                :checked="props.isOwner ? true : hasPermission(perm.flag)"
                :disabled="props.isOwner"
                class="w-4 h-4 accent-primary-500 rounded"
                @change="togglePermission(perm.flag)"
              />
              <span class="text-[0.78rem] text-text-secondary">{{ perm.label }}</span>
            </label>
          </div>

          <!-- Make Owner (only for non-owners) -->
          <div v-if="!props.isOwner" class="mt-4 pt-3 border-t border-border">
            <label class="flex items-center gap-2.5 cursor-pointer">
              <input type="checkbox" v-model="makeOwnerChecked" class="w-4 h-4 accent-primary-500" />
              <span class="text-[0.82rem] text-text-secondary">Transfer ownership to this user</span>
            </label>
          </div>
        </div>

        <!-- Accounts section -->
        <div class="mt-2">
          <h3 class="text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-3">Accounts</h3>

          <div v-if="user.accounts.length > 0" class="bg-white/[0.02] border border-border rounded-xl overflow-hidden">
            <!-- Header -->
            <div class="grid grid-cols-[0.5fr_2fr_2fr_0.7fr] gap-3 px-4 py-2 border-b border-border">
              <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
              <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</span>
              <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Company Name</span>
              <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Owner</span>
            </div>
            <!-- Rows -->
            <div
              v-for="acc in user.accounts"
              :key="acc.clientId"
              class="grid grid-cols-[0.5fr_2fr_2fr_0.7fr] gap-3 px-4 py-2.5 border-b border-border last:border-0"
            >
              <span class="text-[0.78rem] text-text-muted font-mono">{{ acc.clientId }}</span>
              <span class="text-[0.78rem] text-text-primary">{{ acc.firstName }} {{ acc.lastName }}</span>
              <span class="text-[0.78rem] text-text-secondary">{{ acc.companyName || '—' }}</span>
              <span v-if="acc.isOwner" class="text-status-green text-[0.82rem]">✓</span>
              <span v-else class="text-text-muted text-[0.78rem]">—</span>
            </div>
          </div>

          <p v-else class="text-[0.78rem] text-text-muted">No accounts linked</p>
        </div>
      </div>

      <!-- Footer -->
      <div class="flex items-center justify-between px-6 py-4 border-t border-border">
        <button
          class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-red/80 rounded-[9px] hover:bg-status-red transition-colors"
          @click="handleDelete"
        >
          Permanently Delete
        </button>

        <div class="flex items-center gap-2">
          <button
            class="px-4 py-2 text-[0.82rem] font-medium text-text-secondary bg-white/[0.05] border border-border rounded-[9px] hover:text-text-primary hover:border-border/80 transition-colors"
            @click="emit('close')"
          >
            Close
          </button>
          <button
            :disabled="saving"
            class="gradient-brand px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90 disabled:opacity-50"
            @click="handleSave"
          >
            {{ saving ? 'Saving…' : 'Save' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
