<script setup lang="ts">
/**
 * Client users sub-page — lists all users (owner + additional) linked to this client.
 * Supports adding users with permissions, managing user profiles, and removing non-owner users.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useApi } from '../../../composables/useApi'
import { useUsersStore, type UserDetail } from '../stores/usersStore'
import { PERMISSION_LABELS, ClientPermission, type ClientUserItem } from '../../../types/models'
import { formatDate as sharedFormatDate } from '../../../utils/format'
import UserFormModal from '../components/UserFormModal.vue'

const route = useRoute()
const { request } = useApi()
const usersStore = useUsersStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Loaded client users list. */
const users = ref<ClientUserItem[]>([])

/** True while the initial fetch is in flight. */
const loading = ref(false)

/** Error message, null when no error. */
const error = ref<string | null>(null)

/** Feedback message after a successful action. */
const successMessage = ref<string | null>(null)

/** Whether the "Add User" section is visible. */
const showAddUser = ref(false)

/** The user currently being managed in the modal, null when modal is closed. */
const editingUser = ref<UserDetail | null>(null)

/** Permissions of the editing user for this client. */
const editingUserPermissions = ref<number>(ClientPermission.All)

/** Whether the editing user is the account owner. */
const editingUserIsOwner = ref(false)

/** True while saving user changes in the modal. */
const savingUser = ref(false)

/** Whether the manage modal is open. */
const showManageModal = ref(false)

// --- Add User state ---

/** Search term for finding existing Identity users. */
const addUserSearchTerm = ref('')

/** Results from user search. */
const addUserSearchResults = ref<{ id: string; email: string; firstName: string; lastName: string }[]>([])

/** True while user search is in flight. */
const addUserSearchLoading = ref(false)

/** Selected user to add, null until one is picked. */
const selectedUserToAdd = ref<{ id: string; email: string; firstName: string; lastName: string } | null>(null)

/** Permissions bit-flags for the user being added. */
const addUserPermissions = ref<number>(ClientPermission.All)

/** True while adding a user. */
const addingUser = ref(false)

// --- Invite by Email state ---

/** Active mode for the Add User panel: search existing users or invite by email. */
const inviteMode = ref<'search' | 'invite'>('search')

/** Email address for the invitation. */
const inviteEmail = ref('')

/** First name for the invited user. */
const inviteFirstName = ref('')

/** Last name for the invited user. */
const inviteLastName = ref('')

/** Permissions bit-flags for the invited user. */
const invitePermissions = ref<number>(ClientPermission.All)

/** Success message after sending an invitation. */
const inviteSuccess = ref<string | null>(null)

/** True while the invite request is in flight. */
const inviteSending = ref(false)

// --- User search debounce ---

/** Timer handle for debounced user search. */
let searchTimer: ReturnType<typeof setTimeout> | null = null

watch(addUserSearchTerm, (term) => {
  if (searchTimer) clearTimeout(searchTimer)
  if (!term.trim()) {
    addUserSearchResults.value = []
    return
  }
  searchTimer = setTimeout(async () => {
    addUserSearchLoading.value = true
    try {
      const params = new URLSearchParams({ search: term.trim(), pageSize: '5', page: '1' })
      const result = await request<{ items: { id: string; email: string; firstName: string; lastName: string }[] }>(`/admin/users?${params}`)
      addUserSearchResults.value = result.items
    } catch {
      addUserSearchResults.value = []
    } finally {
      addUserSearchLoading.value = false
    }
  }, 300)
})

/**
 * Fetches all users linked to this client.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function fetchUsers(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    users.value = await request<ClientUserItem[]>(`/clients/${clientId.value}/users`)
  } catch {
    error.value = 'Failed to load client users.'
  } finally {
    loading.value = false
  }
}

/**
 * Formats an ISO date string for display, using 'Never' as fallback.
 *
 * @param iso - ISO 8601 date string or null.
 * @returns Formatted date string or "Never" if null/invalid.
 */
function formatDate(iso: string | null): string {
  return sharedFormatDate(iso, 'Never')
}

/**
 * Returns initials from first and last name.
 *
 * @param first - First name.
 * @param last - Last name.
 * @returns Two-letter initials string.
 */
function getInitials(first: string, last: string): string {
  return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase()
}

/**
 * Selects a user from the search results to add.
 *
 * @param user - The user to select.
 */
function selectUserToAdd(user: { id: string; email: string; firstName: string; lastName: string }): void {
  selectedUserToAdd.value = user
  addUserSearchTerm.value = ''
  addUserSearchResults.value = []
  addUserPermissions.value = ClientPermission.All
}

/**
 * Clears the selected user to add.
 */
function clearSelectedUserToAdd(): void {
  selectedUserToAdd.value = null
  addUserPermissions.value = ClientPermission.All
}

/**
 * Checks whether a permission flag is set in the add-user permissions value.
 *
 * @param flag - Permission bit flag.
 * @returns True if the flag is set.
 */
function hasAddPermission(flag: number): boolean {
  return (addUserPermissions.value & flag) !== 0
}

/**
 * Toggles a permission flag in the add-user permissions value.
 *
 * @param flag - Permission bit flag to toggle.
 */
function toggleAddPermission(flag: number): void {
  addUserPermissions.value ^= flag
}

/**
 * Submits the add-user request to the backend.
 *
 * @returns Promise that resolves when the user is added.
 */
async function handleAddUser(): Promise<void> {
  if (!selectedUserToAdd.value) return
  addingUser.value = true
  error.value = null
  try {
    await request(`/clients/${clientId.value}/users`, {
      method: 'POST',
      body: JSON.stringify({
        userId: selectedUserToAdd.value.id,
        permissions: addUserPermissions.value,
      }),
    })
    successMessage.value = 'User added successfully.'
    setTimeout(() => { successMessage.value = null }, 3000)
    showAddUser.value = false
    selectedUserToAdd.value = null
    addUserSearchTerm.value = ''
    addUserPermissions.value = ClientPermission.All
    await fetchUsers()
  } catch {
    error.value = 'Failed to add user.'
  } finally {
    addingUser.value = false
  }
}

/**
 * Checks whether a permission flag is set in the invite permissions value.
 *
 * @param flag - Permission bit flag.
 * @returns True if the flag is set.
 */
function hasInvitePermission(flag: number): boolean {
  return (invitePermissions.value & flag) !== 0
}

/**
 * Toggles a permission flag in the invite permissions value.
 *
 * @param flag - Permission bit flag to toggle.
 */
function toggleInvitePermission(flag: number): void {
  invitePermissions.value ^= flag
}

/**
 * Sends an invitation email to a new user, creating their account and linking them to this client.
 *
 * @returns Promise that resolves when the invitation is sent.
 */
async function handleSendInvite(): Promise<void> {
  inviteSending.value = true
  inviteSuccess.value = null
  error.value = null
  try {
    await request(`/clients/${clientId.value}/users/invite`, {
      method: 'POST',
      body: JSON.stringify({
        email: inviteEmail.value.trim(),
        firstName: inviteFirstName.value.trim(),
        lastName: inviteLastName.value.trim(),
        permissions: invitePermissions.value,
      }),
    })
    inviteSuccess.value = `Invitation sent to ${inviteEmail.value}`
    inviteEmail.value = ''
    inviteFirstName.value = ''
    inviteLastName.value = ''
    invitePermissions.value = ClientPermission.All
    showAddUser.value = false
    setTimeout(() => { inviteSuccess.value = null }, 4000)
    await fetchUsers()
  } catch {
    error.value = 'Failed to send invitation.'
  } finally {
    inviteSending.value = false
  }
}

/**
 * Opens the manage user modal for a given client user.
 *
 * @param clientUser - The client user item to manage.
 */
async function openManageModal(clientUser: ClientUserItem): Promise<void> {
  await usersStore.fetchById(clientUser.userId)
  if (usersStore.current) {
    editingUser.value = { ...usersStore.current }
    editingUserPermissions.value = clientUser.permissions
    editingUserIsOwner.value = clientUser.isOwner
    showManageModal.value = true
  }
}

/**
 * Saves user profile changes from the manage modal.
 *
 * @param data - Updated profile fields.
 */
async function handleSaveUser(data: { firstName: string; lastName: string; email: string; language: string | null }): Promise<void> {
  if (!editingUser.value) return
  savingUser.value = true
  try {
    await usersStore.updateUser(editingUser.value.id, data)
    showManageModal.value = false
    await fetchUsers()
    successMessage.value = 'User updated successfully.'
    setTimeout(() => { successMessage.value = null }, 3000)
  } catch {
    error.value = 'Failed to save user.'
  } finally {
    savingUser.value = false
  }
}

/**
 * Updates permissions for a non-owner user.
 *
 * @param permissions - New permissions bit-flags value.
 */
async function handleUpdatePermissions(permissions: number): Promise<void> {
  if (!editingUser.value || editingUserIsOwner.value) return
  try {
    await request(`/clients/${clientId.value}/users/${editingUser.value.id}/permissions`, {
      method: 'PUT',
      body: JSON.stringify({ permissions }),
    })
    await fetchUsers()
    successMessage.value = 'Permissions updated.'
    setTimeout(() => { successMessage.value = null }, 3000)
  } catch {
    error.value = 'Failed to update permissions.'
  }
}

/**
 * Transfers ownership to the editing user.
 */
async function handleMakeOwner(): Promise<void> {
  if (!editingUser.value) return
  if (!confirm(`Transfer ownership to "${editingUser.value.email}"? The current owner will become a regular user.`)) return
  try {
    await request(`/clients/${clientId.value}/users/${editingUser.value.id}/make-owner`, {
      method: 'POST',
    })
    showManageModal.value = false
    await fetchUsers()
    successMessage.value = 'Ownership transferred successfully.'
    setTimeout(() => { successMessage.value = null }, 3000)
  } catch {
    error.value = 'Failed to transfer ownership.'
  }
}

/**
 * Deletes the user (from the manage modal).
 */
async function handleDeleteUser(): Promise<void> {
  if (!editingUser.value) return
  try {
    await usersStore.deleteUser(editingUser.value.id)
    showManageModal.value = false
    await fetchUsers()
    successMessage.value = 'User deleted.'
    setTimeout(() => { successMessage.value = null }, 3000)
  } catch {
    error.value = 'Failed to delete user.'
  }
}

/**
 * Removes a non-owner user from this client.
 *
 * @param clientUser - The user to remove.
 */
async function handleRemoveUser(clientUser: ClientUserItem): Promise<void> {
  if (!confirm(`Remove "${clientUser.email}" from this client? They will lose access to this account.`)) return
  try {
    await request(`/clients/${clientId.value}/users/${clientUser.userId}`, {
      method: 'DELETE',
    })
    await fetchUsers()
    successMessage.value = 'User removed.'
    setTimeout(() => { successMessage.value = null }, 3000)
  } catch {
    error.value = 'Failed to remove user.'
  }
}

onMounted(() => fetchUsers())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Success feedback -->
    <div v-if="successMessage" class="mb-4 px-4 py-2.5 text-[0.82rem] text-status-green bg-status-green/10 border border-status-green/20 rounded-xl">
      {{ successMessage }}
    </div>

    <!-- Error feedback (non-blocking) -->
    <div v-if="error" class="mb-4 px-4 py-2.5 text-[0.82rem] text-status-red bg-status-red/10 border border-status-red/20 rounded-xl">
      {{ error }}
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading users...
    </div>

    <template v-else>

      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <h2 class="font-display font-bold text-[1.1rem] text-text-primary">
          Users ({{ users.length }})
        </h2>
        <button
          class="gradient-brand px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[10px] transition-opacity hover:opacity-90"
          @click="showAddUser = !showAddUser"
        >
          {{ showAddUser ? 'Cancel' : '+ Add User' }}
        </button>
      </div>

      <!-- Add User Section -->
      <div v-if="showAddUser" class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
        <h3 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Add User to Account</h3>

        <!-- Invite success banner -->
        <div v-if="inviteSuccess" class="mb-4 px-4 py-2.5 text-[0.82rem] text-status-green bg-status-green/10 border border-status-green/20 rounded-xl">
          {{ inviteSuccess }}
        </div>

        <!-- Mode toggle tabs -->
        <div class="flex gap-1 mb-4">
          <button
            type="button"
            class="px-3 py-1.5 text-[0.78rem] font-medium rounded-lg transition-colors"
            :class="inviteMode === 'search' ? 'bg-primary-500/15 text-primary-400' : 'text-text-muted hover:text-text-secondary'"
            @click="inviteMode = 'search'"
          >
            Search Existing
          </button>
          <button
            type="button"
            class="px-3 py-1.5 text-[0.78rem] font-medium rounded-lg transition-colors"
            :class="inviteMode === 'invite' ? 'bg-primary-500/15 text-primary-400' : 'text-text-muted hover:text-text-secondary'"
            @click="inviteMode = 'invite'"
          >
            Invite by Email
          </button>
        </div>

        <!-- ── Search Existing mode ──────────────────────────────────────────── -->
        <template v-if="inviteMode === 'search'">
          <!-- Search or selected user -->
          <div v-if="selectedUserToAdd" class="flex items-center gap-2.5 bg-primary-500/10 border border-primary-500/20 rounded-[10px] px-3 py-2.5 mb-4">
            <div class="flex items-center justify-center w-7 h-7 rounded-full bg-primary-500/20 text-primary-400 text-[0.65rem] font-bold shrink-0">
              {{ selectedUserToAdd.firstName.charAt(0) }}{{ selectedUserToAdd.lastName.charAt(0) }}
            </div>
            <div class="flex-1 min-w-0">
              <p class="text-[0.82rem] text-text-primary font-medium truncate">{{ selectedUserToAdd.firstName }} {{ selectedUserToAdd.lastName }}</p>
              <p class="text-[0.68rem] text-text-muted truncate">{{ selectedUserToAdd.email }}</p>
            </div>
            <button
              type="button"
              class="w-6 h-6 flex items-center justify-center rounded-md text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
              @click="clearSelectedUserToAdd"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
            </button>
          </div>

          <template v-else>
            <div class="relative mb-4">
              <input
                v-model="addUserSearchTerm"
                type="text"
                placeholder="Search by name or email..."
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
              <span v-if="addUserSearchLoading" class="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
            </div>
            <div v-if="addUserSearchResults.length > 0" class="mb-4 bg-surface-card border border-border rounded-[10px] overflow-hidden shadow-lg">
              <button
                v-for="u in addUserSearchResults"
                :key="u.id"
                type="button"
                class="w-full flex items-center gap-2.5 px-3 py-2.5 text-left hover:bg-white/[0.05] transition-colors border-b border-border last:border-0"
                @click="selectUserToAdd(u)"
              >
                <div class="flex items-center justify-center w-7 h-7 rounded-full bg-primary-500/10 text-primary-400 text-[0.65rem] font-bold shrink-0">
                  {{ u.firstName.charAt(0) }}{{ u.lastName.charAt(0) }}
                </div>
                <div class="min-w-0">
                  <p class="text-[0.82rem] text-text-primary font-medium truncate">{{ u.firstName }} {{ u.lastName }}</p>
                  <p class="text-[0.68rem] text-text-muted truncate">{{ u.email }}</p>
                </div>
              </button>
            </div>
          </template>

          <!-- Permissions checkboxes (only after selecting a user) -->
          <div v-if="selectedUserToAdd" class="mb-4">
            <h4 class="text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-3">Permissions</h4>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
              <label v-for="perm in PERMISSION_LABELS" :key="perm.flag" class="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  :checked="hasAddPermission(perm.flag)"
                  class="w-4 h-4 accent-primary-500 rounded"
                  @change="toggleAddPermission(perm.flag)"
                />
                <span class="text-[0.78rem] text-text-secondary">{{ perm.label }}</span>
              </label>
            </div>
          </div>

          <!-- Add button -->
          <div v-if="selectedUserToAdd" class="flex justify-end">
            <button
              :disabled="addingUser"
              class="gradient-brand px-5 py-2 text-[0.82rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
              @click="handleAddUser"
            >
              {{ addingUser ? 'Adding...' : 'Add User' }}
            </button>
          </div>
        </template>

        <!-- ── Invite by Email mode ──────────────────────────────────────────── -->
        <template v-if="inviteMode === 'invite'">
          <div class="space-y-3 mb-4">
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-1.5">Email</label>
              <input
                v-model="inviteEmail"
                type="email"
                placeholder="user@example.com"
                required
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-1.5">First Name</label>
                <input
                  v-model="inviteFirstName"
                  type="text"
                  placeholder="John"
                  required
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                />
              </div>
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-1.5">Last Name</label>
                <input
                  v-model="inviteLastName"
                  type="text"
                  placeholder="Doe"
                  required
                  class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.875rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                />
              </div>
            </div>
          </div>

          <!-- Permissions checkboxes -->
          <div class="mb-4">
            <h4 class="text-[0.72rem] font-semibold uppercase tracking-[0.07em] text-text-muted mb-3">Permissions</h4>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
              <label v-for="perm in PERMISSION_LABELS" :key="perm.flag" class="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  :checked="hasInvitePermission(perm.flag)"
                  class="w-4 h-4 accent-primary-500 rounded"
                  @change="toggleInvitePermission(perm.flag)"
                />
                <span class="text-[0.78rem] text-text-secondary">{{ perm.label }}</span>
              </label>
            </div>
          </div>

          <!-- Send Invite button -->
          <div class="flex justify-end">
            <button
              :disabled="inviteSending || !inviteEmail.trim() || !inviteFirstName.trim() || !inviteLastName.trim()"
              class="gradient-brand px-5 py-2 text-[0.82rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
              @click="handleSendInvite"
            >
              {{ inviteSending ? 'Sending...' : 'Send Invite' }}
            </button>
          </div>
        </template>
      </div>

      <!-- Users table -->
      <div v-if="users.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- Table header -->
        <div class="hidden sm:grid grid-cols-[2fr_2fr_1fr_1.2fr_1fr] gap-3 px-5 py-3 border-b border-border">
          <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Name</span>
          <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Email</span>
          <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Role</span>
          <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Last Login</span>
          <span class="text-[0.65rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-right">Actions</span>
        </div>

        <!-- Table rows -->
        <div
          v-for="u in users"
          :key="u.userId"
          class="grid grid-cols-1 sm:grid-cols-[2fr_2fr_1fr_1.2fr_1fr] gap-2 sm:gap-3 px-5 py-3 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <!-- Name -->
          <div class="flex items-center gap-2.5">
            <div class="flex items-center justify-center w-8 h-8 rounded-full bg-primary-500/10 text-primary-400 text-[0.65rem] font-bold shrink-0">
              {{ getInitials(u.firstName, u.lastName) }}
            </div>
            <span class="text-[0.82rem] text-text-primary font-medium truncate">{{ u.firstName }} {{ u.lastName }}</span>
          </div>

          <!-- Email -->
          <div class="flex items-center">
            <span class="text-[0.78rem] text-text-secondary truncate">{{ u.email }}</span>
          </div>

          <!-- Role -->
          <div class="flex items-center">
            <span
              v-if="u.isOwner"
              class="inline-flex px-2 py-0.5 rounded-full text-[0.65rem] font-semibold border text-status-green bg-status-green/10 border-status-green/20"
            >
              Owner
            </span>
            <span v-else class="text-[0.78rem] text-text-secondary">User</span>
          </div>

          <!-- Last Login -->
          <div class="flex items-center">
            <span class="text-[0.78rem] text-text-secondary">{{ formatDate(u.lastLoginAt) }}</span>
          </div>

          <!-- Actions -->
          <div class="flex items-center justify-end gap-2">
            <button
              class="px-3 py-1.5 text-[0.75rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-lg hover:bg-primary-500/15 transition-colors"
              @click="openManageModal(u)"
            >
              Manage
            </button>
            <button
              v-if="!u.isOwner"
              class="px-3 py-1.5 text-[0.75rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-lg hover:bg-status-red/20 transition-colors"
              @click="handleRemoveUser(u)"
            >
              Remove
            </button>
          </div>
        </div>
      </div>

      <!-- Empty state -->
      <div v-else-if="!loading" class="bg-surface-card border border-border rounded-2xl p-8 flex flex-col items-center gap-3">
        <div class="w-12 h-12 rounded-2xl bg-status-yellow/10 border border-status-yellow/20 flex items-center justify-center">
          <svg class="w-6 h-6 text-status-yellow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" />
            <line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
        </div>
        <p class="text-[0.875rem] font-medium text-text-secondary">No users linked to this client.</p>
      </div>

    </template>

    <!-- Manage User Modal -->
    <Teleport to="body">
      <UserFormModal
        v-if="showManageModal && editingUser"
        :user="editingUser"
        :saving="savingUser"
        :is-owner="editingUserIsOwner"
        :permissions="editingUserPermissions"
        :client-id="Number(clientId)"
        @save="handleSaveUser"
        @update-permissions="handleUpdatePermissions"
        @make-owner="handleMakeOwner"
        @delete="handleDeleteUser"
        @close="showManageModal = false"
      />
    </Teleport>
  </div>
</template>
