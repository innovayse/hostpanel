<script setup lang="ts">
/**
 * Manage Users — list all Identity users with edit, password reset, and delete actions.
 */
import { onMounted, ref, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useUsersStore, type UserDetail } from '../stores/usersStore'
import UserFormModal from '../components/UserFormModal.vue'
import ChangePasswordModal from '../components/ChangePasswordModal.vue'

const router = useRouter()
const store = useUsersStore()

/** Search input value. */
const search = ref('')

/** User being edited in the modal. */
const editingUser = ref<UserDetail | null>(null)

/** True while saving a user edit. */
const savingUser = ref(false)

/** User ID for the change password modal. */
const changingPasswordFor = ref<string | null>(null)

/** True while saving a password change. */
const savingPassword = ref(false)

/** ID of user whose action dropdown is open. */
const openDropdownId = ref<string | null>(null)

/** Ref for outside-click detection on dropdowns. */
const dropdownRefs = ref<Map<string, HTMLElement>>(new Map())

/**
 * Applies the search filter.
 */
function applySearch(): void {
  store.page = 1
  store.fetchAll(search.value || undefined)
}

/**
 * Navigates to a page.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  const total = Math.max(1, Math.ceil(store.totalCount / store.pageSize))
  if (p < 1 || p > total) return
  store.page = p
  store.fetchAll(search.value || undefined)
}

/**
 * Opens the manage user modal for a user.
 *
 * @param userId - The user's Identity ID.
 */
async function openManageModal(userId: string): Promise<void> {
  openDropdownId.value = null
  await store.fetchById(userId)
  if (store.current) editingUser.value = { ...store.current }
}

/**
 * Saves user profile changes.
 *
 * @param data - Updated profile fields.
 */
async function handleSaveUser(data: { firstName: string; lastName: string; email: string; language: string | null }): Promise<void> {
  if (!editingUser.value) return
  savingUser.value = true
  try {
    await store.updateUser(editingUser.value.id, data)
    editingUser.value = null
    await store.fetchAll(search.value || undefined)
  } catch {
    alert('Failed to save user.')
  } finally {
    savingUser.value = false
  }
}

/**
 * Deletes the user being edited.
 */
async function handleDeleteUser(): Promise<void> {
  if (!editingUser.value) return
  try {
    await store.deleteUser(editingUser.value.id)
    editingUser.value = null
    await store.fetchAll(search.value || undefined)
  } catch {
    alert('Failed to delete user.')
  }
}

/**
 * Sends a password reset email.
 *
 * @param userId - The user's Identity ID.
 */
async function handleSendPasswordReset(userId: string): Promise<void> {
  openDropdownId.value = null
  try {
    await store.sendPasswordReset(userId)
    alert('Password reset email sent.')
  } catch {
    alert('Failed to send password reset email.')
  }
}

/**
 * Opens the change password modal.
 *
 * @param userId - The user's Identity ID.
 */
function openChangePasswordModal(userId: string): void {
  openDropdownId.value = null
  changingPasswordFor.value = userId
}

/**
 * Saves the new password.
 *
 * @param password - The new password.
 */
async function handleChangePassword(password: string): Promise<void> {
  if (!changingPasswordFor.value) return
  savingPassword.value = true
  try {
    await store.changePassword(changingPasswordFor.value, password)
    changingPasswordFor.value = null
  } catch {
    alert('Failed to change password.')
  } finally {
    savingPassword.value = false
  }
}

/**
 * Toggles the action dropdown for a user row.
 *
 * @param userId - The user's Identity ID.
 */
function toggleDropdown(userId: string): void {
  openDropdownId.value = openDropdownId.value === userId ? null : userId
}

/**
 * Closes dropdown on outside click.
 *
 * @param e - Mouse event.
 */
function onOutsideClick(e: MouseEvent): void {
  if (!openDropdownId.value) return
  const el = dropdownRefs.value.get(openDropdownId.value)
  if (el && !el.contains(e.target as Node)) {
    openDropdownId.value = null
  }
}

/**
 * Formats a date string as locale date+time.
 *
 * @param dateStr - ISO date string or null.
 * @returns Formatted string or "Never".
 */
function formatDate(dateStr: string | null): string {
  if (!dateStr) return 'Never'
  return new Date(dateStr).toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

onMounted(() => {
  store.fetchAll()
  document.addEventListener('mousedown', onOutsideClick)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', onOutsideClick)
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-start justify-between mb-5">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Manage Users</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">View and manage user accounts</p>
      </div>
      <button
        class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90 shrink-0"
        @click="router.push('/clients/add')"
      >
        <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        Add Client
      </button>
    </div>

    <!-- Search bar -->
    <div class="bg-surface-card border border-border rounded-2xl p-4 mb-4">
      <div class="flex flex-col sm:flex-row items-stretch sm:items-end gap-3">
        <div class="flex-1 min-w-0">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Search</label>
          <input
            v-model="search"
            type="text"
            placeholder="Search by name or email…"
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
            @keydown.enter="applySearch"
          />
        </div>
        <div class="flex items-end gap-2 shrink-0">
          <button
            class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90"
            @click="applySearch"
          >
            <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
            </svg>
            Search
          </button>
          <button
            v-if="search"
            class="px-3 py-2 text-[0.78rem] text-text-muted hover:text-text-primary transition-colors"
            @click="search = ''; applySearch()"
          >
            Clear
          </button>
        </div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.users.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading users…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Table -->
    <div v-else-if="store.users.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[0.5fr_1.5fr_1.5fr_2.5fr_1.5fr_1fr] gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">First Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Last Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Email</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Last Login</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-center">Actions</span>
      </div>

      <!-- Rows -->
      <div
        v-for="user in store.users"
        :key="user.id"
        class="grid grid-cols-1 sm:grid-cols-[0.5fr_1.5fr_1.5fr_2.5fr_1.5fr_1fr] gap-2 sm:gap-4 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
      >
        <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ user.clientId ?? '—' }}</span>
        <span class="text-[0.82rem] text-text-primary font-medium">{{ user.firstName }}</span>
        <span class="text-[0.82rem] text-text-primary">{{ user.lastName }}</span>
        <span class="text-[0.82rem] text-text-secondary truncate">{{ user.email }}</span>
        <span class="text-[0.82rem] text-text-muted">{{ formatDate(user.lastLoginAt) }}</span>

        <!-- Split button -->
        <div
          :ref="(el) => { if (el) dropdownRefs.set(user.id, el as HTMLElement) }"
          class="relative flex items-center justify-center"
        >
          <button
            class="flex items-center gap-1.5 px-3 py-1.5 text-[0.78rem] font-medium text-text-secondary bg-white/[0.05] border border-border rounded-l-[8px] hover:text-text-primary hover:bg-white/[0.08] transition-colors"
            @click="openManageModal(user.id)"
          >
            Manage User
          </button>
          <button
            class="flex items-center justify-center w-9 self-stretch text-text-muted bg-white/[0.05] border border-l-0 border-border rounded-r-[8px] hover:text-text-primary hover:bg-white/[0.08] transition-colors"
            @click="toggleDropdown(user.id)"
          >
            <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="6 9 12 15 18 9"/>
            </svg>
          </button>

          <!-- Dropdown -->
          <Transition
            enter-active-class="transition duration-100 ease-out"
            enter-from-class="opacity-0 scale-95 -translate-y-1"
            enter-to-class="opacity-100 scale-100 translate-y-0"
            leave-active-class="transition duration-75 ease-in"
            leave-from-class="opacity-100 scale-100 translate-y-0"
            leave-to-class="opacity-0 scale-95 -translate-y-1"
          >
            <div
              v-if="openDropdownId === user.id"
              class="absolute right-0 top-full z-50 mt-1.5 w-56 bg-surface-card border border-border rounded-[10px] shadow-xl overflow-hidden"
            >
              <button
                class="w-full text-left px-3.5 py-2.5 text-[0.82rem] text-text-secondary hover:text-text-primary hover:bg-white/[0.05] transition-colors"
                @click="handleSendPasswordReset(user.id)"
              >
                Send Password Reset Email
              </button>
              <button
                class="w-full text-left px-3.5 py-2.5 text-[0.82rem] text-text-secondary hover:text-text-primary hover:bg-white/[0.05] transition-colors"
                @click="openChangePasswordModal(user.id)"
              >
                Change Password
              </button>
            </div>
          </Transition>
        </div>
      </div>

      <!-- Pagination -->
      <div v-if="Math.ceil(store.totalCount / store.pageSize) > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
        <span class="text-[0.75rem] text-text-muted">
          {{ store.totalCount }} user{{ store.totalCount !== 1 ? 's' : '' }}
        </span>
        <div class="flex items-center gap-1">
          <button
            :disabled="store.page <= 1"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(store.page - 1)"
          >
            Prev
          </button>
          <span class="text-[0.75rem] text-text-muted px-2">{{ store.page }} / {{ Math.ceil(store.totalCount / store.pageSize) }}</span>
          <button
            :disabled="store.page >= Math.ceil(store.totalCount / store.pageSize)"
            class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
            @click="goToPage(store.page + 1)"
          >
            Next
          </button>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3">
      <div class="w-12 h-12 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center">
        <svg class="w-6 h-6 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M16 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="8.5" cy="7" r="4"/><path d="M20 8v6"/><path d="M23 11h-6"/>
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">No users found</p>
    </div>

    <!-- Manage User Modal -->
    <Teleport to="body">
      <UserFormModal
        v-if="editingUser"
        :user="editingUser"
        :saving="savingUser"
        @save="handleSaveUser"
        @delete="handleDeleteUser"
        @close="editingUser = null"
      />
    </Teleport>

    <!-- Change Password Modal -->
    <Teleport to="body">
      <ChangePasswordModal
        v-if="changingPasswordFor"
        :saving="savingPassword"
        @save="handleChangePassword"
        @close="changingPasswordFor = null"
      />
    </Teleport>

  </div>
</template>
