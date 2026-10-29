<script setup lang="ts">
/**
 * Client users sub-page: lists all users (owner + additional) linked to this client.
 * Supports adding users with permissions, managing user profiles, and removing non-owner users.
 * Removing, deleting and transferring ownership are confirmed first; every action reports its
 * outcome (success, or the API's sentence) at the top, and a refused load shows the sentence with
 * a Retry in place of the list.
 */
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useUsersStore } from '@/stores/clientAccounts'
import { useClientUsersStore } from '@/stores/clientUsers'
import { ClientPermission, PERMISSION_LABELS } from '@/types/clientuser'
import type { ClientUserItem, UserSearchHit } from '@/types/clientuser'
import type { UserDetail } from '@/types/useraccount'
import { formatDate as sharedFormatDate } from '@/lib/format'
import ClientUserPicker from '@/components/clients/ClientUserPicker.vue'
import UserFormModal from '@/components/clients/UserFormModal.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiAlign } from '@/utils/ui/table'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiLabelVariant, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import type { UiTabItem } from '@/utils/ui/tabs'
import { MS_PER_SECOND, NOTICE_VISIBLE_MS } from '@/lib/limits'

const { t } = useI18n()

/** Modes of the "Add user" panel. */
const AddUserMode = {
  Search: 'search',
  Invite: 'invite',
} as const

/** Current route, source of the client id. */
const route = useRoute()

/** Accounts store, owner of the user detail, profile update and delete. */
const usersStore = useUsersStore()

/** Client users store, the only caller of the client-user endpoints. */
const store = useClientUsersStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Loaded client users list. */
const users = ref<ClientUserItem[]>([])

/** True while the initial fetch is in flight (owned by the store). */
const loading = computed(() => store.loading)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** Sentence of a refused action, null when none (owned by the client-users and users stores). */
const error = computed<string | null>(() => (loadError.value === null ? store.error ?? usersStore.actionError : null))

/** Feedback message after a successful action. */
const successMessage = ref<string | null>(null)

/** Timer that clears the success message. */
let successTimer: ReturnType<typeof setTimeout> | undefined

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

/** Active mode for the Add User panel: search existing users or invite by email. */
const addMode = ref<string>(AddUserMode.Search)

/** Mode tabs of the Add User panel. */
const modeTabs = computed<UiTabItem[]>(() => [
  { value: AddUserMode.Search, label: t('clients.clientUsers.searchExisting'), testid: 'client-users-mode-search' },
  { value: AddUserMode.Invite, label: t('clients.clientUsers.inviteByEmail'), testid: 'client-users-mode-invite' },
])

/** Selected user to add, null until one is picked. */
const selectedUserToAdd = ref<UserSearchHit | null>(null)

/** Permissions bit-flags for the user being added or invited. */
const newUserPermissions = ref<number>(ClientPermission.All)

/** True while adding a user. */
const addingUser = ref(false)

/** Email address for the invitation. */
const inviteEmail = ref('')

/** First name for the invited user. */
const inviteFirstName = ref('')

/** Last name for the invited user. */
const inviteLastName = ref('')

/** True while the invite request is in flight. */
const inviteSending = ref(false)

/** True when the invite form is complete enough to send. */
const inviteReady = computed<boolean>(() =>
  !!inviteEmail.value.trim() && !!inviteFirstName.value.trim() && !!inviteLastName.value.trim(),
)

/**
 * Shows a temporary success message.
 *
 * @param message - The message to display.
 * @param ms - How long to keep it, in milliseconds.
 * @returns Nothing.
 */
const flashSuccess = (message: string, ms: number = NOTICE_VISIBLE_MS): void => {
  successMessage.value = message
  clearTimeout(successTimer)
  successTimer = setTimeout(() => {
    successMessage.value = null
  }, ms)
}

/**
 * Fetches all users linked to this client.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchUsers = async (): Promise<void> => {
  const result = await store.fetchUsers(clientId.value)
  loadError.value = result ? null : store.error
  if (result) {
    users.value = result
  }
}

/**
 * Formats an ISO date string for display, using the "never" caption as fallback.
 *
 * @param iso - ISO 8601 date string or null.
 * @returns Formatted date string or the "never" caption if null/invalid.
 */
const formatDate = (iso: string | null): string => sharedFormatDate(iso, t('clients.clientUsers.never'))

/**
 * Returns initials from first and last name.
 *
 * @param first - First name.
 * @param last - Last name.
 * @returns Two-letter initials string.
 */
const getInitials = (first: string, last: string): string => `${first.charAt(0)}${last.charAt(0)}`.toUpperCase()

/**
 * Checks whether a permission flag is set in the new-user permissions value.
 *
 * @param flag - Permission bit flag.
 * @returns True if the flag is set.
 */
const hasPermission = (flag: number): boolean => (newUserPermissions.value & flag) !== 0

/**
 * Toggles a permission flag in the new-user permissions value.
 *
 * @param flag - Permission bit flag to toggle.
 * @returns Nothing.
 */
const togglePermission = (flag: number): void => {
  newUserPermissions.value ^= flag
}

/**
 * Submits the add-user request to the backend.
 *
 * @returns Promise that resolves when the user is added.
 */
const handleAddUser = async (): Promise<void> => {
  if (!selectedUserToAdd.value) {
    return
  }
  addingUser.value = true
  const added = await store.addUser(clientId.value, selectedUserToAdd.value.id, newUserPermissions.value)
  addingUser.value = false
  if (added) {
    flashSuccess(t('clients.clientUsers.userAddedSuccessfully'))
    showAddUser.value = false
    selectedUserToAdd.value = null
    newUserPermissions.value = ClientPermission.All
    await fetchUsers()
  }
}

/**
 * Sends an invitation email to a new user, creating their account and linking them to this client.
 *
 * @returns Promise that resolves when the invitation is sent.
 */
const handleSendInvite = async (): Promise<void> => {
  inviteSending.value = true
  const sentTo = inviteEmail.value.trim()
  const sent = await store.inviteUser(clientId.value, {
    email: sentTo,
    firstName: inviteFirstName.value.trim(),
    lastName: inviteLastName.value.trim(),
    permissions: newUserPermissions.value,
  })
  inviteSending.value = false
  if (sent) {
    flashSuccess(t('clients.clientUsers.invitationSentTo', { email: sentTo }), NOTICE_VISIBLE_MS + MS_PER_SECOND)
    inviteEmail.value = ''
    inviteFirstName.value = ''
    inviteLastName.value = ''
    newUserPermissions.value = ClientPermission.All
    showAddUser.value = false
    await fetchUsers()
  }
}

/**
 * Opens the manage user modal for a given client user.
 *
 * @param clientUser - The client user item to manage.
 * @returns Promise that resolves when the user is loaded and the modal is open.
 */
const openManageModal = async (clientUser: ClientUserItem): Promise<void> => {
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
 * @param data.firstName - Given name.
 * @param data.lastName - Family name.
 * @param data.email - E-mail address.
 * @param data.language - Preferred language code, or null for the default.
 * @returns Promise that resolves when the save attempt has finished.
 */
const handleSaveUser = async (data: { firstName: string; lastName: string; email: string; language: string | null }): Promise<void> => {
  if (!editingUser.value) {
    return
  }
  savingUser.value = true
  const saved = await usersStore.updateUser(editingUser.value.id, data)
  savingUser.value = false
  if (saved) {
    showManageModal.value = false
    await fetchUsers()
    flashSuccess(t('clients.clientUsers.userUpdatedSuccessfully'))
  }
}

/**
 * Updates permissions for a non-owner user.
 *
 * @param permissions - New permissions bit-flags value.
 * @returns Promise that resolves when the update attempt has finished.
 */
const handleUpdatePermissions = async (permissions: number): Promise<void> => {
  if (!editingUser.value || editingUserIsOwner.value) {
    return
  }
  if (await store.updatePermissions(clientId.value, editingUser.value.id, permissions)) {
    await fetchUsers()
    flashSuccess(t('clients.clientUsers.permissionsUpdated'))
  }
}

/** The user awaiting ownership-transfer confirmation, null when nothing is staged. */
const makeOwnerTarget = ref<UserDetail | null>(null)

/** True while the staged ownership transfer is in flight. */
const transferringOwnership = ref(false)

/**
 * Stages the ownership transfer to the editing user. Only opens the prompt:
 * {@link confirmMakeOwner} does the work, so the question is asked by the app's own modal.
 *
 * The user is captured here rather than read again on confirm: the manage modal stays open
 * behind the prompt and could be pointed at somebody else by the time it is answered.
 *
 * @returns Nothing.
 */
const handleMakeOwner = (): void => {
  if (!editingUser.value) {
    return
  }
  makeOwnerTarget.value = editingUser.value
}

/**
 * Transfers ownership to the staged user and clears the prompt.
 *
 * @returns Promise that resolves when the transfer attempt has finished.
 */
const confirmMakeOwner = async (): Promise<void> => {
  const target = makeOwnerTarget.value
  if (!target) {
    return
  }
  transferringOwnership.value = true
  const transferred = await store.makeOwner(clientId.value, target.id)
  transferringOwnership.value = false
  makeOwnerTarget.value = null
  if (transferred) {
    showManageModal.value = false
    await fetchUsers()
    flashSuccess(t('clients.clientUsers.ownershipTransferredSuccessfully'))
  }
}

/**
 * Deletes the user (from the manage modal).
 *
 * @returns Promise that resolves when the delete attempt has finished.
 */
const handleDeleteUser = async (): Promise<void> => {
  if (!editingUser.value) {
    return
  }
  if (await usersStore.deleteUser(editingUser.value.id)) {
    showManageModal.value = false
    await fetchUsers()
    flashSuccess(t('clients.clientUsers.userDeleted'))
  }
}

/** The user awaiting removal confirmation, null when nothing is staged. */
const removeUserTarget = ref<ClientUserItem | null>(null)

/** True while the staged user is being removed. */
const removingUser = ref(false)

/**
 * Stages a non-owner user for removal from this client. Only opens the prompt:
 * {@link confirmRemoveUser} does the work, so the question is asked by the app's own modal.
 *
 * @param clientUser - The user to remove.
 * @returns Nothing.
 */
const handleRemoveUser = (clientUser: ClientUserItem): void => {
  removeUserTarget.value = clientUser
}

/**
 * Removes the staged user from this client and clears the prompt.
 *
 * @returns Promise that resolves when the removal attempt has finished.
 */
const confirmRemoveUser = async (): Promise<void> => {
  const target = removeUserTarget.value
  if (!target) {
    return
  }
  removingUser.value = true
  const removed = await store.removeUser(clientId.value, target.userId)
  removingUser.value = false
  removeUserTarget.value = null
  if (removed) {
    await fetchUsers()
    flashSuccess(t('clients.clientUsers.userRemoved'))
  }
}

/** Loads the users. */
onMounted(() => fetchUsers())

/** Stops the pending success-message timer. */
onBeforeUnmount(() => {
  clearTimeout(successTimer)
})
</script>

<template>
  <UiStack>
    <UiAlert v-if="successMessage" variant="success" data-testid="client-users-success">
      {{ successMessage }}
    </UiAlert>
    <UiAlert v-if="error" variant="error" data-testid="client-users-error">
      {{ error }}
    </UiAlert>

    <UiStack v-if="loading" data-testid="client-users-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientUsers.loadingUsers') }}</UiText>
    </UiStack>

    <UiErrorState
      v-else-if="loadError !== null"
      :message="loadError"
      :loading="loading"
      testid="client-users-load-error"
      @retry="fetchUsers()"
    />

    <template v-else>
      <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
        <UiHeading :level="2" variant="section">
          {{ t('clients.clientUsers.usersLength', { length: users.length }) }}
        </UiHeading>
        <UiButton
          :variant="UiButtonVariant.Primary"
          data-testid="client-users-toggle-add"
          @click="showAddUser = !showAddUser"
        >
          {{ showAddUser ? t('shared.cancel') : t('clients.clientUsers.addUser') }}
        </UiButton>
      </UiStack>

      <UiCard v-if="showAddUser" padding="md" data-testid="client-users-add-panel">
        <UiStack>
          <UiHeading :level="3" variant="card">{{ t('clients.clientUsers.addUserToAccount') }}</UiHeading>
          <UiTabs v-model="addMode" :items="modeTabs" data-testid="client-users-add-mode" />

          <UiField v-if="addMode === AddUserMode.Search" :label="t('clients.clientUsers.searchExisting')">
            <template #default="{ id }">
              <ClientUserPicker
                v-model="selectedUserToAdd"
                :search="store.searchUsers"
                :input-id="id"
                testid="client-users-picker"
              />
            </template>
          </UiField>

          <UiStack v-else>
            <UiField :label="t('shared.email')" required>
              <template #default="{ id }">
                <UiInput
                  :id="id"
                  v-model="inviteEmail"
                  type="email"
                  :placeholder="t('shared.examples.email')"
                  data-testid="client-users-invite-email"
                />
              </template>
            </UiField>
            <UiGrid :columns="2" gap="sm">
              <UiField :label="t('shared.firstName')" required>
                <template #default="{ id }">
                  <UiInput
                    :id="id"
                    v-model="inviteFirstName"
                    type="text"
                    :placeholder="t('shared.john')"
                    data-testid="client-users-invite-first-name"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.lastName')" required>
                <template #default="{ id }">
                  <UiInput
                    :id="id"
                    v-model="inviteLastName"
                    type="text"
                    :placeholder="t('shared.doe')"
                    data-testid="client-users-invite-last-name"
                  />
                </template>
              </UiField>
            </UiGrid>
          </UiStack>

          <UiStack v-if="addMode === AddUserMode.Invite || selectedUserToAdd">
            <UiHeading :level="4" variant="card">{{ t('clients.clientUsers.permissions') }}</UiHeading>
            <UiGrid :columns="2" gap="sm">
              <UiLabel v-for="perm in PERMISSION_LABELS" :key="perm.flag" :variant="UiLabelVariant.Inline">
                <UiCheckbox
                  :model-value="hasPermission(perm.flag)"
                  :aria-label="perm.label"
                  :data-testid="`client-users-permission-${perm.flag}`"
                  @update:model-value="togglePermission(perm.flag)"
                />
                {{ perm.label }}
              </UiLabel>
            </UiGrid>
            <UiStack direction="row" :justify="UiJustify.End">
              <UiButton
                v-if="addMode === AddUserMode.Search"
                :variant="UiButtonVariant.Primary"
                :disabled="addingUser"
                data-testid="client-users-add-submit"
                @click="handleAddUser"
              >
                {{ addingUser ? t('clients.clientUsers.adding') : t('clients.clientUsers.addUser2') }}
              </UiButton>
              <UiButton
                v-else
                :variant="UiButtonVariant.Primary"
                :disabled="inviteSending || !inviteReady"
                data-testid="client-users-invite-submit"
                @click="handleSendInvite"
              >
                {{ inviteSending ? t('shared.sending') : t('clients.clientUsers.sendInvite') }}
              </UiButton>
            </UiStack>
          </UiStack>
        </UiStack>
      </UiCard>

      <UiCard v-if="users.length > 0">
        <UiTable density="comfortable" data-testid="client-users-table">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.email') }}</UiTableHeader>
              <UiTableHeader>{{ t('clients.clientUsers.role') }}</UiTableHeader>
              <UiTableHeader>{{ t('clients.clientUsers.lastLogin') }}</UiTableHeader>
              <UiTableHeader :align="UiAlign.Right">{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="u in users" :key="u.userId" hoverable>
              <UiTableCell>
                <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
                  <UiBadge :tone="UiTone.Primary" small>{{ getInitials(u.firstName, u.lastName) }}</UiBadge>
                  {{ u.firstName }} {{ u.lastName }}
                </UiStack>
              </UiTableCell>
              <UiTableCell muted>{{ u.email }}</UiTableCell>
              <UiTableCell>
                <UiBadge v-if="u.isOwner" :tone="UiTone.Success" small>{{ t('clients.clientUsers.owner') }}</UiBadge>
                <UiText v-else :tone="UiTextTone.Secondary">{{ t('clients.clientUsers.user') }}</UiText>
              </UiTableCell>
              <UiTableCell muted>{{ formatDate(u.lastLoginAt) }}</UiTableCell>
              <UiTableCell :align="UiAlign.Right">
                <UiRowActions
                  :actions="[
                    { label: t('clients.clientUsers.manage'), testid: `client-users-manage-${u.userId}`, onSelect: () => openManageModal(u) },
                    ...(!u.isOwner ? [{ label: t('clients.clientUsers.remove'), testid: `client-users-remove-${u.userId}`, onSelect: () => handleRemoveUser(u), danger: true }] : []),
                  ]"
                />
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiCard>

      <UiCard v-else>
        <UiEmptyState :title="t('clients.clientUsers.noUsersLinkedToThis')" />
      </UiCard>
    </template>

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

    <UiDialog
      v-if="makeOwnerTarget"
      variant="confirm"
      :title="t('clients.clientUsers.transferOwnership')"
      :message="t('clients.clientUsers.confirmTransferOwnership', { email: makeOwnerTarget.email })"
      :confirm-label="t('clients.clientUsers.transfer')"
      :loading-label="t('shared.transferring')"
      :loading="transferringOwnership"
      tone="warning"
      @confirm="confirmMakeOwner"
      @close="makeOwnerTarget = null"
    />

    <UiDialog
      v-if="removeUserTarget"
      variant="confirm"
      :title="t('clients.clientUsers.removeUser')"
      :message="t('clients.clientUsers.confirmRemoveUser', { email: removeUserTarget.email })"
      :confirm-label="t('clients.clientUsers.remove')"
      :loading-label="t('shared.removing')"
      :loading="removingUser"
      tone="danger"
      @confirm="confirmRemoveUser"
      @close="removeUserTarget = null"
    />
  </UiStack>
</template>
