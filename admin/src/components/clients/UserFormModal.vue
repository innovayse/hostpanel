<script setup lang="ts">
/**
 * Manage User modal: edit profile fields, view linked accounts, manage permissions, transfer
 * ownership and delete the user.
 *
 * Presentational: the parent owns every request and reacts to the emitted events.
 */
import { ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import type { UserDetail } from '@/types/useraccount'
import { ClientPermission, PERMISSION_LABELS } from '@/types/clientuser'
import { LANGUAGE_OPTIONS } from '@/lib/constants'
import { UiTableDensity } from '@/utils/ui/injection'
import { EMPTY_VALUE } from '@/lib/format'

const { t } = useI18n()

/** Props for UserFormModal. */
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

/** Language options for UiSelect. */
const languageOptions = LANGUAGE_OPTIONS

/** True while the delete confirmation prompt is showing; nothing is staged when false. */
const showDeleteConfirm = ref(false)

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
const hasPermission = (flag: number): boolean => {
  return (permissionValues.value & flag) !== 0
}

/**
 * Toggles a permission flag on or off.
 *
 * @param flag - The permission bit flag to toggle.
 */
const togglePermission = (flag: number): void => {
  permissionValues.value ^= flag
}

/**
 * Emits save with the current form values, plus permissions and ownership if applicable.
 */
const handleSave = (): void => {
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
 * Stages the delete confirmation. Only opens the prompt; {@link confirmDelete} does the work,
 * so the question is asked by the app's own modal instead of the browser's blocking dialog.
 */
const handleDelete = (): void => {
  showDeleteConfirm.value = true
}

/**
 * Dismisses the delete confirmation without deleting.
 */
const cancelDelete = (): void => {
  showDeleteConfirm.value = false
}

/**
 * Emits the delete event and dismisses the prompt.
 *
 * There is no in-flight flag: the parent owns the request and unmounts this modal as soon as it
 * accepts the event, so this component never observes the outcome.
 */
const confirmDelete = (): void => {
  showDeleteConfirm.value = false
  emit('delete')
}
</script>

<template>
  <UiDialog
    :title="t('clients.userForm.manageUserEmail', { email: user.email })"
    size="lg"
    data-testid="clients-userform-dialog"
    @close="emit('close')"
  >
    <UiStack gap="md">
      <UiField :label="t('shared.firstName')">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="firstName"
            type="text"
            :placeholder="t('clients.userForm.firstNamePlaceholder')"
            data-testid="clients-userform-first-name"
          />
        </template>
      </UiField>

      <UiField :label="t('shared.lastName')">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="lastName"
            type="text"
            :placeholder="t('clients.userForm.lastNamePlaceholder')"
            data-testid="clients-userform-last-name"
          />
        </template>
      </UiField>

      <UiField :label="t('shared.emailAddress')">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="email"
            type="email"
            :placeholder="t('clients.userForm.emailPlaceholder')"
            data-testid="clients-userform-email"
          />
        </template>
      </UiField>

      <UiField :label="t('shared.language')">
        <UiSelect
          v-model="language"
          :options="languageOptions"
          :placeholder="t('shared.default')"
          data-testid="clients-userform-language"
        />
      </UiField>

      <!-- Permissions section (only when clientId is provided) -->
      <UiStack v-if="props.clientId !== undefined" gap="sm">
        <UiHeading :level="3" variant="card">{{ t('clients.userForm.permissions') }}</UiHeading>

        <UiAlert v-if="props.isOwner" variant="info">
          {{ t('clients.userForm.thisUserIsTheAccount') }}
        </UiAlert>

        <UiGrid :columns="2" gap="sm">
          <UiLabel v-for="perm in PERMISSION_LABELS" :key="perm.flag" variant="inline">
            <UiCheckbox
              :model-value="props.isOwner ? true : hasPermission(perm.flag)"
              :disabled="props.isOwner"
              :data-testid="`clients-userform-permission-${perm.flag}`"
              @update:model-value="togglePermission(perm.flag)"
            />
            {{ perm.label }}
          </UiLabel>
        </UiGrid>

        <!-- Make Owner (only for non-owners) -->
        <UiLabel v-if="!props.isOwner" variant="inline">
          <UiCheckbox v-model="makeOwnerChecked" data-testid="clients-userform-make-owner" />
          {{ t('clients.userForm.transferOwnershipToThisUser') }}
        </UiLabel>
      </UiStack>

      <!-- Accounts section -->
      <UiStack gap="sm">
        <UiHeading :level="3" variant="card">{{ t('shared.accounts') }}</UiHeading>

        <UiTable v-if="user.accounts.length > 0" :density="UiTableDensity.Compact" data-testid="clients-userform-accounts">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.clientName') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.companyName') }}</UiTableHeader>
              <UiTableHeader>{{ t('clients.userForm.owner') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody>
            <UiTableRow v-for="acc in user.accounts" :key="acc.clientId">
              <UiTableCell muted>{{ acc.clientId }}</UiTableCell>
              <UiTableCell>{{ acc.firstName }} {{ acc.lastName }}</UiTableCell>
              <UiTableCell muted>{{ acc.companyName || EMPTY_VALUE }}</UiTableCell>
              <UiTableCell :muted="!acc.isOwner">{{ acc.isOwner ? '✓' : EMPTY_VALUE }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>

        <UiText v-else tone="muted" size="sm">{{ t('clients.userForm.noAccountsLinked') }}</UiText>
      </UiStack>
    </UiStack>

    <template #footer>
      <UiStack direction="row" justify="between" align="center" class="w-full">
        <UiButton variant="danger" data-testid="clients-userform-delete" @click="handleDelete">
          {{ t('clients.userForm.permanentlyDelete') }}
        </UiButton>

        <UiStack direction="row" gap="sm" align="center">
          <UiButton variant="secondary" data-testid="clients-userform-close" @click="emit('close')">
            {{ t('shared.close') }}
          </UiButton>
          <UiButton
            variant="primary"
            :loading="saving"
            data-testid="clients-userform-save"
            @click="handleSave"
          >
            {{ t('shared.save') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </template>
  </UiDialog>

  <!-- Delete confirmation -->
  <UiDialog
    v-if="showDeleteConfirm"
    variant="confirm"
    :title="t('clients.userForm.deleteUser')"
    :message="t('clients.userForm.deleteMessage', { email })"
    :confirm-label="t('shared.delete')"
    tone="danger"
    data-testid="clients-userform-delete-confirm"
    @confirm="confirmDelete"
    @close="cancelDelete"
  />
</template>
