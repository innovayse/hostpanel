<script setup lang="ts">
/**
 * Manage Users: list all Identity users with edit, password reset, and delete actions.
 *
 * The search text and the page live in the address (`useListQuery`), so a reload or a shared link restores the same
 * list, which the API narrows before it pages. The list and every refusal sentence come from the users store; this
 * screen only renders them and routes the row actions (manage, reset email, change password). Every action reports
 * its outcome as a toast (success, or the API's sentence), which is fixed on the window so it is seen wherever the page
 * is scrolled and above an open dialog; a refused load shows
 * the sentence with a Retry button. Where the sign-on service owns the passwords, the two password actions are not
 * offered and a note says where passwords are managed.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useUsersStore } from '@/stores/clientAccounts'
import { AuthMode } from '@/types/auth'
import type { UserDetail, UserListItem } from '@/types/useraccount'
import UserFormModal from '@/components/clients/UserFormModal.vue'
import ChangePasswordModal from '@/components/clients/ChangePasswordModal.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ManageUsersView.schema'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { EMPTY_VALUE, formatMediumDateTime } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiGap } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'

/** Translation function and the active locale (for date formatting). */
const { t, locale } = useI18n()

/** Users store: the paged list, the detail and the refusal sentences. */
const store = useUsersStore()

/** The search text and the page of the list, kept in the address; the list has no filter fields. */
const { search, appliedSearch, page, setPage, narrowed, loadOnChange, stepBackWhenEmpty } = useListQuery([])

/** User being edited in the modal. */
const editingUser = ref<UserDetail | null>(null)

/** True while saving a user edit. */
const savingUser = ref(false)

/** User ID for the change password modal. */
const changingPasswordFor = ref<string | null>(null)

/** True while saving a password change. */
const savingPassword = ref(false)

/** The outcome of the last action, reported as a toast, which stays in view wherever the page is scrolled. */
const banner = useOutcomeToast()

/** Auth store: it knows whether the sign-on service owns the accounts, and with them their passwords. */
const auth = useAuthStore()

/**
 * Whether the sign-on service owns the accounts' passwords. The API refuses every password action then, so the screen
 * does not offer them and says where passwords are managed instead.
 */
const passwordsManagedBySso = computed<boolean>(() => auth.mode === AuthMode.Sso)

/**
 * Reports the outcome of a store action: the success sentence as a toast, or the API's own sentence of a refusal.
 *
 * @param succeeded - Whether the API accepted the action; when false the store holds the refusal in `actionError`.
 * @param successText - The sentence a success is reported with.
 * @returns The same flag, so a caller can continue only after a success.
 */
const report = (succeeded: boolean, successText: string): boolean => {
  if (succeeded) {
    banner.show(successText, false)
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
  return succeeded
}

/**
 * Loads the page the address names with the search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const reload = (): Promise<boolean> => store.fetchAll(page.value, { search: appliedSearch.value })

// Every change of the search or the page loads the list the address names.
loadOnChange(reload)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && store.users.length === 0)

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
  const saved = report(await store.updateUser(editingUser.value.id, data), t('clients.manageUsers.userSaved'))
  savingUser.value = false
  if (saved) {
    editingUser.value = null
    await reload()
  }
}

/** Deletes the user being edited, then reads the list again; when that emptied the last page, steps back one page. */
const handleDeleteUser = async (): Promise<void> => {
  if (!editingUser.value) {
    return
  }
  if (report(await store.deleteUser(editingUser.value.id), t('clients.manageUsers.userDeleted'))) {
    editingUser.value = null
    await reload()
    await stepBackWhenEmpty(store.users.length === 0)
  }
}

/**
 * Sends a password reset email.
 *
 * @param userId - The user's Identity ID.
 */
const handleSendPasswordReset = async (userId: string): Promise<void> => {
  report(await store.sendPasswordReset(userId), t('clients.manageUsers.passwordResetEmailSent'))
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
  if (report(await store.changePassword(changingPasswordFor.value, password), t('clients.manageUsers.passwordChanged'))) {
    changingPasswordFor.value = null
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
  return formatMediumDateTime(new Date(dateStr), locale.value)
}

/**
 * Row actions for a managed user.
 *
 * @param user - The user whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const manageUserActions = (user: UserListItem): UiMenuAction[] => {
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
  const manage: UiMenuAction = { label: t('shared.edit'), icon: Icon.Settings, testid: `users-manage-${user.id}`, onSelect: onManage }
  if (passwordsManagedBySso.value) {
    return [manage]
  }
  return [
    manage,
    { label: t('clients.manageUsers.sendPasswordResetEmail'), icon: Icon.Mail, testid: `users-reset-${user.id}`, onSelect: onReset },
    { label: t('shared.changePassword'), icon: Icon.Key, testid: `users-password-${user.id}`, onSelect: onPassword },
  ]
}
</script>

<template>
  <UiStack :gap="UiGap.Md">
    <!-- Where passwords are managed, when this screen cannot set them -->
    <UiAlert v-if="passwordsManagedBySso" :variant="UiTone.Info" data-testid="users-passwords-managed-by-sso">
      {{ t('clients.manageUsers.passwordsManagedBySso') }}
    </UiAlert>

    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <!-- Search box: the search is the only thing the identity provider can narrow the list by -->
    <UiListToolbar
      v-model:search="search"
      search-testid="users-search"
      :search-placeholder="t('clients.manageUsers.searchByNameOrEmail')"
      :loading="initialLoading"
    />

    <!-- List load failed: replaces the table, with a Retry -->
    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="users-error"
      @retry="reload"
    />

    <!-- Table card: the rows, the loading and empty states and the pager -->
    <UiDataTable
      v-else
      :loading="initialLoading"
      :empty="!initialLoading && store.users.length === 0"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      pagination-testid="users-pagination"
      data-testid="users-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow v-for="user in store.users" :key="user.id" hoverable :data-testid="`users-row-${user.id}`">
          <UiTableCell muted>{{ user.clientId ?? EMPTY_VALUE }}</UiTableCell>
          <UiTableCell>{{ user.firstName }}</UiTableCell>
          <UiTableCell>{{ user.lastName }}</UiTableCell>
          <UiTableCell muted>{{ user.email }}</UiTableCell>
          <UiTableCell muted>{{ formatDate(user.lastLoginAt) }}</UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="manageUserActions(user)" :testid="`users-actions-${user.id}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState :title="narrowed ? t('clients.manageUsers.noUsersMatchYourSearch') : t('clients.manageUsers.noUsersFound')" />
      </template>
    </UiDataTable>

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
