<script setup lang="ts">
/**
 * Manage Users: list all Identity users with edit, password reset, and delete actions.
 *
 * The list, the paging and every refusal sentence come from the users store; this screen only
 * renders them and routes the row actions (manage, reset email, change password). Every action
 * reports its outcome above the table (success, or the API's sentence); a refused load shows the
 * sentence with a Retry button.
 */
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useUsersStore } from '@/stores/clientAccounts'
import type { UserDetail, UserListItem } from '@/types/useraccount'
import UserFormModal from '@/components/clients/UserFormModal.vue'
import ChangePasswordModal from '@/components/clients/ChangePasswordModal.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { NOTICE_VISIBLE_MS } from '@/lib/limits'
import { AppRoute } from '@/lib/routes'

/** Translation function and the active locale (for date formatting). */
const { t, locale } = useI18n()

/** Router, for the "Add Client" shortcut. */
const router = useRouter()

/** Users store: the paged list, the detail and the refusal sentences. */
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

/**
 * Message reporting a failed action, null when there is nothing to report (owned by the store).
 *
 * Kept separate from `store.error`, which reports a failed *list load* and is rendered in place
 * of the table; an action failure must not hide the table the user is still working in.
 */
const actionError = computed(() => store.actionError)

/** Message reporting a completed action, null when there is nothing to report. */
const actionSuccess = ref<string | null>(null)

/** Timer that clears the success message. */
const successTimer = ref<ReturnType<typeof setTimeout> | null>(null)

/**
 * Shows a success message and clears it after a few seconds.
 *
 * Auto-clearing matches the sibling client screens (ClientUsersView, ClientContactsView): a
 * success needs acknowledging, not dismissing, which is why only this half expires.
 *
 * @param message - The message to display.
 */
const showSuccess = (message: string): void => {
  store.actionError = null
  actionSuccess.value = message
  if (successTimer.value !== null) {
    clearTimeout(successTimer.value)
  }
  successTimer.value = setTimeout(() => {
    actionSuccess.value = null
  }, NOTICE_VISIBLE_MS)
}

/**
 * Loads the current page with the current search.
 *
 * @returns Resolves when the list has settled.
 */
const reload = (): Promise<void> => store.fetchAll(search.value || undefined)

/** Applies the search filter, starting from the first page. */
const applySearch = (): void => {
  store.page = 1
  void reload()
}

/** Clears the search box and reloads the unfiltered list. */
const clearSearch = (): void => {
  search.value = ''
  applySearch()
}

/**
 * Navigates to a page.
 *
 * @param p - Target page number.
 */
const goToPage = (p: number): void => {
  store.page = p
  void reload()
}

/**
 * Opens the manage user modal for a user.
 *
 * @param userId - The user's Identity ID.
 */
const openManageModal = async (userId: string): Promise<void> => {
  await store.fetchById(userId)
  if (store.current) {
    editingUser.value = { ...store.current }
  }
}

/**
 * Saves user profile changes.
 *
 * @param data - Updated profile fields.
 * @param data.firstName - Given name.
 * @param data.lastName - Family name.
 * @param data.email - E-mail address.
 * @param data.language - Preferred language code, or null for the default.
 */
const handleSaveUser = async (
  data: { firstName: string; lastName: string; email: string; language: string | null },
): Promise<void> => {
  if (!editingUser.value) {
    return
  }
  savingUser.value = true
  actionSuccess.value = null
  const saved = await store.updateUser(editingUser.value.id, data)
  savingUser.value = false
  if (saved) {
    editingUser.value = null
    showSuccess(t('clients.manageUsers.userSaved'))
    await reload()
  }
}

/** Deletes the user being edited. */
const handleDeleteUser = async (): Promise<void> => {
  if (!editingUser.value) {
    return
  }
  actionSuccess.value = null
  if (await store.deleteUser(editingUser.value.id)) {
    editingUser.value = null
    showSuccess(t('clients.manageUsers.userDeleted'))
    await reload()
  }
}

/**
 * Sends a password reset email.
 *
 * @param userId - The user's Identity ID.
 */
const handleSendPasswordReset = async (userId: string): Promise<void> => {
  if (await store.sendPasswordReset(userId)) {
    showSuccess(t('clients.manageUsers.passwordResetEmailSent'))
  }
}

/**
 * Opens the change password modal.
 *
 * @param userId - The user's Identity ID.
 */
const openChangePasswordModal = (userId: string): void => {
  changingPasswordFor.value = userId
}

/**
 * Saves the new password.
 *
 * @param password - The new password.
 */
const handleChangePassword = async (password: string): Promise<void> => {
  if (!changingPasswordFor.value) {
    return
  }
  savingPassword.value = true
  actionSuccess.value = null
  if (await store.changePassword(changingPasswordFor.value, password)) {
    changingPasswordFor.value = null
    showSuccess(t('clients.manageUsers.passwordChanged'))
  }
  savingPassword.value = false
}

/**
 * Formats a date string as locale date+time.
 *
 * @param dateStr - ISO date string or null.
 * @returns Formatted string, or the translated "Never".
 */
const formatDate = (dateStr: string | null): string => {
  if (!dateStr) {
    return t('clients.manageUsers.never')
  }
  return new Date(dateStr).toLocaleString(locale.value, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

/** Loads the first page of users. */
onMounted(() => {
  void store.fetchAll()
})

/** Stops the pending success-message timer. */
onUnmounted(() => {
  if (successTimer.value !== null) {
    clearTimeout(successTimer.value)
  }
})

/**
 * Row actions for a managed user.
 *
 * @param user - The user whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const manageUserActions = (user: UserListItem): UiRowAction[] => {
  /**
   * Opens the manage dialog for this user.
   * @returns Nothing.
   */
  const onManage = (): Promise<void> => openManageModal(user.id)
  /**
   * Sends a password reset email to this user.
   * @returns Nothing.
   */
  const onReset = (): Promise<void> => handleSendPasswordReset(user.id)
  /**
   * Opens the change-password dialog for this user.
   * @returns Nothing.
   */
  const onPassword = (): void => openChangePasswordModal(user.id)
  return [
    { label: t('clients.manageUsers.manageUser'), testid: `users-manage-${user.id}`, onSelect: onManage },
    { label: t('clients.manageUsers.sendPasswordResetEmail'), testid: `users-reset-${user.id}`, onSelect: onReset },
    { label: t('shared.changePassword'), testid: `users-password-${user.id}`, onSelect: onPassword },
  ]
}
</script>

<template>
  <UiStack gap="md" class="w-full">
    <!-- Action feedback: sits above the table so a failed action never replaces it -->
    <UiAlert v-if="actionSuccess" variant="success" data-testid="users-action-success">
      {{ actionSuccess }}
    </UiAlert>
    <UiAlert v-if="actionError" variant="error" data-testid="users-action-error">
      {{ actionError }}
    </UiAlert>

    <UiPageHeader
      :title="t('clients.manageUsers.manageUsers')"
      :subtitle="t('clients.manageUsers.viewAndManageUserAccounts')"
    >
      <template #actions>
        <UiButton variant="primary" data-testid="users-add-client" @click="router.push(AppRoute.ClientsAdd)">
          {{ t('clients.manageUsers.addClient') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <!-- Search bar -->
    <UiFilterCard>
      <UiStack direction="row" align="end" gap="sm" wrap>
        <UiInput
          v-model="search"
          type="text"
          :label="t('shared.search2')"
          :placeholder="t('clients.manageUsers.searchByNameOrEmail')"
          class="min-w-0 flex-1"
          data-testid="users-search"
          @keydown.enter="applySearch"
        />
        <UiButton variant="primary" data-testid="users-search-submit" @click="applySearch">
          {{ t('shared.search2') }}
        </UiButton>
        <UiButton v-if="search" variant="ghost" data-testid="users-search-clear" @click="clearSearch">
          {{ t('shared.clear') }}
        </UiButton>
      </UiStack>
    </UiFilterCard>

    <!-- List load failed: replaces the table, with a Retry -->
    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="users-error"
      @retry="reload"
    />

    <!-- Table (also draws the loading and the empty state) -->
    <UiTable
      v-else
      density="comfortable"
      :loading="store.loading && store.users.length === 0"
      :empty="!store.loading && store.users.length === 0"
      :colspan="6"
      data-testid="users-table"
    >
      <UiTableHead>
        <UiTableRow>
          <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
          <UiTableHeader>{{ t('shared.firstName') }}</UiTableHeader>
          <UiTableHeader>{{ t('shared.lastName') }}</UiTableHeader>
          <UiTableHeader>{{ t('shared.email') }}</UiTableHeader>
          <UiTableHeader>{{ t('clients.manageUsers.lastLogin') }}</UiTableHeader>
          <UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>
        </UiTableRow>
      </UiTableHead>
      <UiTableBody divided>
        <UiTableRow v-for="user in store.users" :key="user.id" hoverable :data-testid="`users-row-${user.id}`">
          <UiTableCell muted>{{ user.clientId ?? '—' }}</UiTableCell>
          <UiTableCell>{{ user.firstName }}</UiTableCell>
          <UiTableCell>{{ user.lastName }}</UiTableCell>
          <UiTableCell muted>{{ user.email }}</UiTableCell>
          <UiTableCell muted>{{ formatDate(user.lastLoginAt) }}</UiTableCell>
          <UiTableCell align="right">
            <UiRowActions :actions="manageUserActions(user)" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState :title="t('clients.manageUsers.noUsersFound')" />
      </template>
    </UiTable>

    <!-- Pagination -->
    <UiStack
      v-if="!store.error && store.totalCount > store.pageSize"
      direction="row"
      align="center"
      justify="between"
    >
      <UiText tone="muted" size="sm">
        {{ t('clients.manageUsers.totalcountUserValue', { totalCount: store.totalCount }) }}
      </UiText>
      <UiPagination
        :page="store.page"
        :total="store.totalCount"
        :page-size="store.pageSize"
        testid="users-pagination"
        @update:page="goToPage"
      />
    </UiStack>

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
  </UiStack>
</template>
