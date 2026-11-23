<script setup lang="ts">
/**
 * Client groups screen: the named segments staff put clients into, each with a discount and a
 * colour. Lists the groups with their member counts; creates and edits through a dialog, sets or
 * clears the default group, and deletes after a confirmation. A refused load offers a retry; a
 * refused action shows the API's sentence above the table.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ClientGroupBadge from '@/components/clients/ClientGroupBadge.vue'
import ClientGroupFormDialog from '@/components/clients/ClientGroupFormDialog.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ClientGroupsView.schema'
import { useClientGroupsStore } from '@/stores/clientGroups'
import type { ClientGroup, ClientGroupPayload } from '@/types/clientgroup'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'
import { EMPTY_VALUE } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('clients.clientGroups.add'), icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: 'client-groups-add', onSelect: openCreate },
])

/** Client groups store, the only caller of the group endpoints. */
const store = useClientGroupsStore()

/** True while the first load is in flight and there are no groups yet; the table then draws its placeholder rows. */
const initialLoading = computed<boolean>(() => store.loading && store.groups.length === 0)

/** Whether the create/edit dialog is open. */
const formOpen = ref(false)

/** The group the dialog edits; null while it creates a new one. */
const editing = ref<ClientGroup | null>(null)

/** The group waiting for the delete confirmation, null when no prompt is open. */
const deleteTarget = ref<ClientGroup | null>(null)

/** True while the confirmed delete is running. */
const deleting = ref(false)

/** The question shown for the staged group. */
const deleteMessage = computed<string>(() =>
  t('clients.clientGroups.deleteMessage', { name: deleteTarget.value?.name ?? '' }),
)

/**
 * Opens the dialog empty, to create a group.
 *
 * @returns Nothing.
 */
const openCreate = (): void => {
  store.resetSaveError()
  editing.value = null
  formOpen.value = true
}

/**
 * Opens the dialog filled with a group, to edit it.
 *
 * @param group - The group to edit.
 * @returns Nothing.
 */
const openEdit = (group: ClientGroup): void => {
  store.resetSaveError()
  editing.value = group
  formOpen.value = true
}

/**
 * Saves the dialog's payload as a new group or as the edited one; the dialog closes on success
 * and stays open with the refusal on failure.
 *
 * @param payload - The finished form.
 * @returns Promise that resolves when the save has finished.
 */
const save = async (payload: ClientGroupPayload): Promise<void> => {
  const saved = editing.value
    ? await store.update(editing.value.id, payload)
    : await store.create(payload)
  if (saved) {
    formOpen.value = false
  }
}

/**
 * Makes a group the default for new clients, or clears the flag when it already is the default.
 *
 * @param group - The group to toggle.
 * @returns Promise that resolves when the call has finished.
 */
const toggleDefault = async (group: ClientGroup): Promise<void> => {
  await store.setDefault(group.id, !group.isDefault)
}

/**
 * Deletes the staged group and closes the prompt.
 *
 * @returns Promise that resolves when the delete has finished.
 */
const confirmDelete = async (): Promise<void> => {
  if (!deleteTarget.value) {
    return
  }
  deleting.value = true
  await store.remove(deleteTarget.value.id)
  deleting.value = false
  deleteTarget.value = null
}

/**
 * Stages a group for the delete confirmation.
 *
 * @param group - The group to delete once confirmed.
 * @returns Nothing.
 */
const stageDelete = (group: ClientGroup): void => {
  store.clearActionError()
  deleteTarget.value = group
}

/**
 * The actions of one row's menu: edit, set or clear the default, delete.
 *
 * @param group - The row's group.
 * @returns The menu actions.
 */
const rowActions = (group: ClientGroup): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    icon: Icon.Edit,
    /**
     * Opens the edit dialog for this row's group.
     *
     * @returns Nothing the menu reads.
     */
    onSelect: () => openEdit(group),
    testid: `client-group-edit-${group.id}`,
  },
  {
    label: group.isDefault ? t('clients.clientGroups.clearDefault') : t('clients.clientGroups.setDefault'),
    icon: Icon.Star,
    /**
     * Toggles whether this row's group is the default.
     *
     * @returns Nothing the menu reads.
     */
    onSelect: () => toggleDefault(group),
    testid: `client-group-default-${group.id}`,
  },
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    danger: true,
    /**
     * Stages this row's group for the delete confirmation.
     *
     * @returns Nothing the menu reads.
     */
    onSelect: () => stageDelete(group),
    testid: `client-group-delete-${group.id}`,
  },
]

onMounted(async () => {
  // A refusal of another visit must not show on this list.
  store.clearActionError()
  await store.fetchAll()
})
</script>

<template>
  <UiStack>
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>

    <UiAlert v-if="store.actionError" :variant="UiTone.Danger" data-testid="client-groups-action-error">
      {{ store.actionError }}
    </UiAlert>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="client-groups-error"
      @retry="store.fetchAll()"
    />

    <UiDataTable
      v-else
      :density="UiTableDensity.Comfortable"
      :loading="initialLoading"
      :empty="!initialLoading && store.groups.length === 0"
      :empty-text="t('clients.clientGroups.empty')"
      data-testid="client-groups-table"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow v-for="group in store.groups" :key="group.id" data-testid="client-group-row">
          <UiTableCell><ClientGroupBadge :name="group.name" :color="group.color" /></UiTableCell>
          <UiTableCell muted>{{ group.description || EMPTY_VALUE }}</UiTableCell>
          <UiTableCell>{{ group.discountPercent }}%</UiTableCell>
          <UiTableCell muted>{{ group.clientCount }}</UiTableCell>
          <UiTableCell>
            <UiBadge v-if="group.isDefault" :tone="UiTone.Success" small>{{ t('shared.default') }}</UiBadge>
          </UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="rowActions(group)" :testid="`client-group-actions-${group.id}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
    </UiDataTable>

    <ClientGroupFormDialog
      :open="formOpen"
      :group="editing"
      :saving="store.saving"
      :error="store.saveError"
      :field-errors="store.fieldErrors"
      @save="save"
      @close="formOpen = false"
    />

    <UiDialog
      v-if="deleteTarget"
      :variant="UiDialogVariant.Confirm"
      :title="t('clients.clientGroups.deleteTitle')"
      :message="deleteMessage"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      data-testid="client-group-delete-confirm"
      @confirm="confirmDelete"
      @close="deleteTarget = null"
    />
  </UiStack>
</template>
